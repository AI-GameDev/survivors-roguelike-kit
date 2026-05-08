#region

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using System.Linq;

#endregion

namespace RGame.ScriptableCoreKit
{
    public class StateNodeView : Node
    {
        private static readonly Dictionary<Type, StateStyleConfig> StateConfigs = new()
        {
            {
                typeof(ActionStateNode),
                new StateStyleConfig("⚙️", new Color(0.15f, 0.7f, 0.3f), new Color(0.1f, 0.5f, 0.2f), "ACTION")
            },
            {
                typeof(StateNodeDebugLog),
                new StateStyleConfig("📝", new Color(0.2f, 0.6f, 1f), new Color(0.1f, 0.4f, 0.8f), "DEBUG")
            },
            {
                typeof(EntryStateNode),
                new StateStyleConfig("🚀", new Color(1f, 0.7f, 0.2f), new Color(0.8f, 0.5f, 0.1f), "ENTRY")
            }
        };

        private readonly float _collapsedHeight = 40f;
        private VisualElement _contentContainer;
        private Label _detailsLabel;
        private VisualElement _executionGlow;
        private Label _executionTimeLabel;

        private float _expandedHeight = 100f;
        private VisualElement _headerContainer;

        private VisualElement _mainContainer;

        private VisualElement _previewContainer;

        private IVisualElementScheduledItem _pulseAnimation;
        private Label _stateIcon;
        private Label _stateTypeLabel;
        private VisualElement _statusIndicator;
        private Label _statusLabel;
        private Label _titleLabel;
    
        public Port inputPortTop;     
        public Port inputPortBottom; 
        public Port outputPortLeft;   
        public Port outputPortRight;   
      
        public Port inputPort => GetBestInputPort();
        public Port outputPort => GetBestOutputPort();
        
        private VisualElement _connectionDragArea;
        private bool _isDraggingConnection;
        private VisualElement _connectionPreview;
        private StateNodeView _currentHoverTarget;
        private Vector2 _connectionStartPos;
        private Vector2 _currentMousePos;
        
        public Action<StateNodeView> OnStateSelected;
        public ActionStateNode state;

        public StateNodeView(ActionStateNode state)
        {
            this.state = state;
            viewDataKey = state.guid;

            style.left = state.position.x;
            style.top = state.position.y;

            CreateProfessionalStructure();
            CreateMultiPorts();
            CreateConnectionDragArea();
            ApplyProfessionalStyling();
            SetupInteractions();
            
            RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }

        public bool IsCollapsed => state.isNodeCollapsed;

        private void CreateProfessionalStructure()
        {
            Clear();

            style.minWidth = 200;
            style.maxWidth = 300;
            style.backgroundColor = Color.clear;
            style.borderTopWidth = 0;
            style.borderBottomWidth = 0;
            style.borderLeftWidth = 0;
            style.borderRightWidth = 0;

            _mainContainer = new VisualElement();
            _mainContainer.name = "main-container";
            _mainContainer.style.backgroundColor = new Color(0.12f, 0.12f, 0.12f, 0.95f);
            _mainContainer.style.borderTopLeftRadius = 8;
            _mainContainer.style.borderTopRightRadius = 8;
            _mainContainer.style.borderBottomLeftRadius = 8;
            _mainContainer.style.borderBottomRightRadius = 8;
            _mainContainer.style.borderTopWidth = 2;
            _mainContainer.style.borderBottomWidth = 2;
            _mainContainer.style.borderLeftWidth = 2;
            _mainContainer.style.borderRightWidth = 2;

            _headerContainer = new VisualElement();
            _headerContainer.name = "header-container";
            _headerContainer.style.flexDirection = FlexDirection.Row;
            _headerContainer.style.alignItems = Align.Center;
            _headerContainer.style.paddingTop = 8;
            _headerContainer.style.paddingBottom = 8;
            _headerContainer.style.paddingLeft = 12;
            _headerContainer.style.paddingRight = 8;
            _headerContainer.style.borderTopLeftRadius = 6;
            _headerContainer.style.borderTopRightRadius = 6;
            _headerContainer.style.minHeight = 32;

            _stateIcon = new Label();
            _stateIcon.name = "state-icon";
            _stateIcon.style.fontSize = 16;
            _stateIcon.style.unityFontStyleAndWeight = FontStyle.Bold;
            _stateIcon.style.marginRight = 8;
            _stateIcon.style.width = 20;
            _stateIcon.style.unityTextAlign = TextAnchor.MiddleCenter;
            _stateIcon.style.color = Color.white;

            _titleLabel = new Label(GetFormattedStateName());
            _titleLabel.name = "title-label";
            _titleLabel.style.fontSize = 15;
            _titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _titleLabel.style.color = Color.white;
            _titleLabel.style.flexGrow = 1;
            _titleLabel.style.unityTextAlign = TextAnchor.MiddleLeft;

            _statusIndicator = new VisualElement();
            _statusIndicator.name = "status-indicator";
            _statusIndicator.style.width = 8;
            _statusIndicator.style.height = 8;
            _statusIndicator.style.borderTopLeftRadius = 4;
            _statusIndicator.style.borderTopRightRadius = 4;
            _statusIndicator.style.borderBottomLeftRadius = 4;
            _statusIndicator.style.borderBottomRightRadius = 4;
            _statusIndicator.style.backgroundColor = new Color(0.5f, 0.5f, 0.5f, 0.8f);
            _statusIndicator.style.marginLeft = 4;

            _headerContainer.Add(_stateIcon);
            _headerContainer.Add(_titleLabel);
            _headerContainer.Add(_statusIndicator);

            _contentContainer = new VisualElement();
            _contentContainer.name = "content-container";
            _contentContainer.style.paddingTop = 4;
            _contentContainer.style.paddingBottom = 8;
            _contentContainer.style.paddingLeft = 12;
            _contentContainer.style.paddingRight = 12;

            _stateTypeLabel = new Label();
            _stateTypeLabel.name = "state-type-label";
            _stateTypeLabel.style.fontSize = 9;
            _stateTypeLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _stateTypeLabel.style.color = new Color(0.8f, 0.8f, 0.8f, 0.7f);
            _stateTypeLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _stateTypeLabel.style.letterSpacing = 1;
            _stateTypeLabel.style.marginBottom = 4;

            _contentContainer.Add(_stateTypeLabel);
         
            CreateMiniPreview();

            _executionGlow = new VisualElement();
            _executionGlow.name = "execution-glow";
            _executionGlow.style.position = Position.Absolute;
            _executionGlow.style.top = -3;
            _executionGlow.style.left = -3;
            _executionGlow.style.right = -3;
            _executionGlow.style.bottom = -3;
            _executionGlow.style.borderTopLeftRadius = 10;
            _executionGlow.style.borderTopRightRadius = 10;
            _executionGlow.style.borderBottomLeftRadius = 10;
            _executionGlow.style.borderBottomRightRadius = 10;
            _executionGlow.style.borderTopWidth = 2;
            _executionGlow.style.borderBottomWidth = 2;
            _executionGlow.style.borderLeftWidth = 2;
            _executionGlow.style.borderRightWidth = 2;
            _executionGlow.style.opacity = 0;

            _mainContainer.Add(_headerContainer);
            _mainContainer.Add(_contentContainer);

            Add(_executionGlow);
            Add(_mainContainer);

            _expandedHeight = _mainContainer.layout.height;
        }

        private void CreateMiniPreview()
        {
            _previewContainer = new VisualElement();
            _previewContainer.name = "preview-container";
            _previewContainer.style.marginTop = 8;
            _previewContainer.style.paddingTop = 6;
            _previewContainer.style.paddingBottom = 6;
            _previewContainer.style.paddingLeft = 8;
            _previewContainer.style.paddingRight = 8;
            _previewContainer.style.backgroundColor = new Color(0, 0, 0, 0.2f);
            _previewContainer.style.borderTopLeftRadius = 4;
            _previewContainer.style.borderTopRightRadius = 4;
            _previewContainer.style.borderBottomLeftRadius = 4;
            _previewContainer.style.borderBottomRightRadius = 4;
            _previewContainer.style.borderTopWidth = 1;
            _previewContainer.style.borderBottomWidth = 1;
            _previewContainer.style.borderLeftWidth = 1;
            _previewContainer.style.borderRightWidth = 1;
            _previewContainer.style.borderTopColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            _previewContainer.style.borderBottomColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            _previewContainer.style.borderLeftColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            _previewContainer.style.borderRightColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);

            var previewHeader = new VisualElement();
            previewHeader.style.flexDirection = FlexDirection.Row;
            previewHeader.style.alignItems = Align.Center;
            previewHeader.style.marginBottom = 4;

            var previewTitle = new Label("Preview");
            previewTitle.style.fontSize = 9;
            previewTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            previewTitle.style.color = new Color(0.7f, 0.7f, 0.7f, 0.8f);
            previewTitle.style.flexGrow = 1;

            previewHeader.Add(previewTitle);

            var previewContent = new VisualElement();
            previewContent.name = "preview-content";

            _statusLabel = new Label("Status: Idle");
            _statusLabel.style.fontSize = 8;
            _statusLabel.style.color = new Color(0.8f, 0.8f, 0.8f, 0.9f);
            _statusLabel.style.marginBottom = 2;

            _executionTimeLabel = new Label("Time: 0ms");
            _executionTimeLabel.style.fontSize = 8;
            _executionTimeLabel.style.color = new Color(0.6f, 0.8f, 1f, 0.9f);
            _executionTimeLabel.style.marginBottom = 2;

            _detailsLabel = new Label("");
            _detailsLabel.style.fontSize = 8;
            _detailsLabel.style.color = new Color(0.7f, 0.7f, 0.7f, 0.8f);
            _detailsLabel.style.whiteSpace = WhiteSpace.Normal;
            _detailsLabel.style.marginBottom = 3;

            previewContent.Add(_statusLabel);
            previewContent.Add(_executionTimeLabel);
            previewContent.Add(_detailsLabel);

            _previewContainer.Add(previewHeader);
            _previewContainer.Add(previewContent);

            _contentContainer.Add(_previewContainer);

            UpdatePreviewInfo();
        }

        private void UpdatePreviewInfo()
        {
            if (_statusLabel == null) return;

            if (Application.isPlaying)
            {
                var stateMachine = state.StateMachine;
                if (stateMachine != null && stateMachine.MyStateMachineSO.CurrentState == state)
                {
                    _statusLabel.text = "Status: Active";
                    _statusLabel.style.color = new Color(0.3f, 0.8f, 0.3f, 1f);
                }
                else
                {
                    _statusLabel.text = "Status: Inactive";
                    _statusLabel.style.color = new Color(0.8f, 0.8f, 0.8f, 0.9f);
                }

                _executionTimeLabel.text = $"Time in State: {state.TimeInState:F1}s";
            }
            else
            {
                _statusLabel.text = "Status: Editor Mode";
                _statusLabel.style.color = new Color(0.6f, 0.6f, 0.6f, 0.9f);
                _executionTimeLabel.text = "Time: --";
            }
        }
        
        private void CreateMultiPorts()
        {
            if (!(state is EntryStateNode))
            {
                inputPortTop = new StatePort(Direction.Input, Port.Capacity.Multi);
                inputPortTop.style.position = Position.Absolute;
                inputPortTop.style.top = 20;
                inputPortTop.style.left = Length.Percent(60);
                inputPortTop.style.translate = new StyleTranslate(new Translate(Length.Percent(-50), Length.Percent(-50)));
                SetupHiddenPort(inputPortTop);
                Add(inputPortTop);

                inputPortBottom = new StatePort(Direction.Input, Port.Capacity.Multi);
                inputPortBottom.style.position = Position.Absolute;
                inputPortBottom.style.bottom = -24;
                inputPortBottom.style.left = Length.Percent(60);
                inputPortBottom.style.translate = new StyleTranslate(new Translate(Length.Percent(-50), Length.Percent(50)));
                SetupHiddenPort(inputPortBottom);
                Add(inputPortBottom);
            }
            
            outputPortLeft = new StatePort(Direction.Output, Port.Capacity.Multi);
            outputPortLeft.style.position = Position.Absolute;
            outputPortLeft.style.top = Length.Percent(65);
            outputPortLeft.style.left = 20;
            outputPortLeft.style.translate = new StyleTranslate(new Translate(Length.Percent(-50), Length.Percent(-50)));
            SetupHiddenPort(outputPortLeft);
            Add(outputPortLeft);
            
            outputPortRight = new StatePort(Direction.Output, Port.Capacity.Multi);
            outputPortRight.style.position = Position.Absolute;
            outputPortRight.style.top = Length.Percent(65);
            outputPortRight.style.right = -22;
            outputPortRight.style.translate = new StyleTranslate(new Translate(Length.Percent(50), Length.Percent(-50)));
            SetupHiddenPort(outputPortRight);
            Add(outputPortRight);
        }
        
        private void SetupHiddenPort(Port port)
        {
            port.style.opacity = 0;
            port.style.width = 20;
            port.style.height = 20;
            port.pickingMode = PickingMode.Ignore;
            HidePortTypeLabel(port);
        }

        private Port GetBestInputPort()
        {
            if (inputPortTop == null && inputPortBottom == null) return null;
            if (inputPortTop == null) return inputPortBottom;
            if (inputPortBottom == null) return inputPortTop;
            
            return inputPortTop;
        }

        private Port GetBestOutputPort()
        {
            if (outputPortLeft == null && outputPortRight == null) return null;
            if (outputPortLeft == null) return outputPortRight;
            if (outputPortRight == null) return outputPortLeft;
            
            return outputPortRight;
        }
        
        public Port GetClosestInputPort(Vector2 targetWorldPosition)
        {
            if (state is EntryStateNode) return null;
            
            var availablePorts = new List<Port>();
            if (inputPortTop != null) availablePorts.Add(inputPortTop);
            if (inputPortBottom != null) availablePorts.Add(inputPortBottom);
            
            if (availablePorts.Count == 0) return null;
            if (availablePorts.Count == 1) return availablePorts[0];
            
            Port closestPort = null;
            float minDistance = float.MaxValue;
            
            foreach (var port in availablePorts)
            {
                var portWorldPos = GetPortWorldPosition(port);
                var distance = Vector2.Distance(portWorldPos, targetWorldPosition);
                
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestPort = port;
                }
            }
            
            return closestPort;
        }
        
        public Port GetClosestOutputPort(Vector2 targetWorldPosition)
        {
            var availablePorts = new List<Port>();
            if (outputPortLeft != null) availablePorts.Add(outputPortLeft);
            if (outputPortRight != null) availablePorts.Add(outputPortRight);
            
            if (availablePorts.Count == 0) return null;
            if (availablePorts.Count == 1) return availablePorts[0];
            
            Port closestPort = null;
            float minDistance = float.MaxValue;
            
            foreach (var port in availablePorts)
            {
                var portWorldPos = GetPortWorldPosition(port);
                var distance = Vector2.Distance(portWorldPos, targetWorldPosition);
                
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestPort = port;
                }
            }
            
            return closestPort;
        }

        private Vector2 GetPortWorldPosition(Port port)
        {
            var portCenter = new Vector2(port.layout.width * 0.5f, port.layout.height * 0.5f);
            return port.LocalToWorld(portCenter);
        }
        
        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            schedule.Execute(() => {
                RecalculateConnections();
                
                var graphView = GetFirstAncestorOfType<StateMachineGraphContainer>();
                if (graphView != null)
                {
                    graphView.NotifyNodeMoved(this);
                }
            }).ExecuteLater(50);
        }

        private void RecalculateConnections()
        {
            var graphView = GetFirstAncestorOfType<StateMachineGraphContainer>();
            if (graphView == null) return;

            var edgesToReconnect = new List<(StateMachineEdge edge, Port newPort)>();
            
            if (inputPortTop != null && inputPortBottom != null)
            {
                var allInputConnections = new List<StateMachineEdge>();
                allInputConnections.AddRange(inputPortTop.connections.Cast<StateMachineEdge>());
                allInputConnections.AddRange(inputPortBottom.connections.Cast<StateMachineEdge>());

                foreach (var edge in allInputConnections)
                {
                    if (edge.output?.node is StateNodeView sourceNode)
                    {
                        var sourceWorldPos = GetPortWorldPosition(edge.output);
                        var bestInputPort = GetClosestInputPort(sourceWorldPos);
                        
                        if (bestInputPort != null && edge.input != bestInputPort)
                        {
                            edgesToReconnect.Add((edge, bestInputPort));
                        }
                    }
                }
            }
            
            if (outputPortLeft != null && outputPortRight != null)
            {
                var allOutputConnections = new List<StateMachineEdge>();
                allOutputConnections.AddRange(outputPortLeft.connections.Cast<StateMachineEdge>());
                allOutputConnections.AddRange(outputPortRight.connections.Cast<StateMachineEdge>());

                foreach (var edge in allOutputConnections)
                {
                    if (edge.input?.node is StateNodeView targetNode)
                    {
                        var targetWorldPos = GetPortWorldPosition(edge.input);
                        var bestOutputPort = GetClosestOutputPort(targetWorldPos);
                        
                        if (bestOutputPort != null && edge.output != bestOutputPort)
                        {
                            edgesToReconnect.Add((edge, bestOutputPort));
                        }
                    }
                }
            }
            
            foreach (var (edge, newPort) in edgesToReconnect)
            {
                ReconnectEdge(edge, newPort);
            }
            
            if (edgesToReconnect.Count > 0)
            {
                schedule.Execute(() =>
                {
                    foreach (var (edge, _) in edgesToReconnect)
                    {
                        edge.UpdateEdgeControl();
                        edge.MarkDirtyRepaint();
                    }
                    graphView.MarkDirtyRepaint();
                }).ExecuteLater(10);
            }
        }
        
        private void ReconnectEdge(StateMachineEdge edge, Port newPort)
        {
            try
            {
                if (newPort.direction == Direction.Input && edge.input != null)
                {
                    edge.input.Disconnect(edge);
                    edge.input = newPort;
                    newPort.Connect(edge);
                }
                else if (newPort.direction == Direction.Output && edge.output != null)
                {
                    edge.output.Disconnect(edge);
                    edge.output = newPort;
                    newPort.Connect(edge);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to reconnect edge: {e.Message}");
            }
        }
        
        /// <summary>
        /// Create connection drag area for right-click connection creation
        /// </summary>
        private void CreateConnectionDragArea()
        {
            _connectionDragArea = new VisualElement();
            _connectionDragArea.name = "connection-drag-area";
            _connectionDragArea.style.position = Position.Absolute;
            _connectionDragArea.style.top = 0;
            _connectionDragArea.style.left = 0;
            _connectionDragArea.style.right = 0;
            _connectionDragArea.style.bottom = 0;
            _connectionDragArea.style.backgroundColor = Color.clear;
            
            _connectionDragArea.RegisterCallback<MouseDownEvent>(OnConnectionDragStart);
            _connectionDragArea.RegisterCallback<MouseMoveEvent>(OnConnectionDragMove);
            _connectionDragArea.RegisterCallback<MouseUpEvent>(OnConnectionDragEnd);
            
            Add(_connectionDragArea);
        }

        /// <summary>
        /// Handle right-click start for connection dragging
        /// </summary>
        private void OnConnectionDragStart(MouseDownEvent evt)
        {
            if (evt.button == 1)
            {
                _isDraggingConnection = true;
                _connectionDragArea.CaptureMouse();
                
                _mainContainer.style.borderTopColor = new Color(0f, 0.7f, 1f, 1f);
                _mainContainer.style.borderBottomColor = new Color(0f, 0.7f, 1f, 1f);
                _mainContainer.style.borderLeftColor = new Color(0f, 0.7f, 1f, 1f);
                _mainContainer.style.borderRightColor = new Color(0f, 0.7f, 1f, 1f);
                _mainContainer.style.borderTopWidth = 3;
                _mainContainer.style.borderBottomWidth = 3;
                _mainContainer.style.borderLeftWidth = 3;
                _mainContainer.style.borderRightWidth = 3;
                
                _currentMousePos = this.LocalToWorld(evt.localMousePosition);
                UpdateConnectionStartPosition();
                CreateConnectionPreview(evt.localMousePosition);
                evt.StopPropagation();
                evt.PreventDefault();
            }
        }

        /// <summary>
        /// Handle mouse move during connection dragging
        /// </summary>
        private void OnConnectionDragMove(MouseMoveEvent evt)
        {
            if (_isDraggingConnection && _connectionPreview != null)
            {
                _currentMousePos = this.LocalToWorld(evt.localMousePosition);
                UpdateConnectionStartPosition();
                UpdateConnectionPreview(evt.localMousePosition);
                
                var targetNode = FindNodeUnderPosition(_currentMousePos);
                
                if (_currentHoverTarget != null && _currentHoverTarget != targetNode)
                {
                    ClearHoverHighlight(_currentHoverTarget);
                }
                
                if (targetNode != null && targetNode != _currentHoverTarget)
                {
                    SetHoverHighlight(targetNode);
                }
                
                _currentHoverTarget = targetNode;
            }
        }

        /// <summary>
        /// Update connection start position to closest output port based on mouse position
        /// </summary>
        private void UpdateConnectionStartPosition()
        {
            var bestOutputPort = GetClosestOutputPort(_currentMousePos);
            if (bestOutputPort != null)
            {
                _connectionStartPos = GetPortWorldPosition(bestOutputPort);

                _connectionStartPos += new Vector2(-20, -22);
            }
            else
            {
                var nodeCenter = new Vector2(layout.width * 0.5f, layout.height * 0.5f);
                _connectionStartPos = this.LocalToWorld(nodeCenter);
            }
        }

        /// <summary>
        /// Handle right-click end for connection completion
        /// </summary>
        private void OnConnectionDragEnd(MouseUpEvent evt)
        {
            if (_isDraggingConnection)
            {
                _isDraggingConnection = false;
                _connectionDragArea.ReleaseMouse();
                
                var targetNode = FindNodeUnderPosition(_currentMousePos);
                
                if (targetNode != null && targetNode != this)
                {
                    CreateConnectionToNode(targetNode, _currentMousePos);
                }
                
                DestroyConnectionPreview();
                evt.StopPropagation();
            }
        }

        /// <summary>
        /// Create connection preview with StateMachineEdge style rendering
        /// </summary>
        private void CreateConnectionPreview(Vector2 startPos)
        {
            var graphView = GetFirstAncestorOfType<GraphView>();
            if (graphView == null) return;

            _connectionPreview = new VisualElement();
            _connectionPreview.name = "connection-preview";
            _connectionPreview.style.position = Position.Absolute;
            _connectionPreview.style.top = 0;
            _connectionPreview.style.left = 0;
            _connectionPreview.style.right = 0;
            _connectionPreview.style.bottom = 0;
            _connectionPreview.pickingMode = PickingMode.Ignore;
            _connectionPreview.generateVisualContent += OnDrawConnectionPreview;
            
            graphView.Add(_connectionPreview);
            UpdateConnectionPreview(startPos);
        }

        /// <summary>
        /// Update connection preview position
        /// </summary>
        private void UpdateConnectionPreview(Vector2 currentMousePos)
        {
            if (_connectionPreview != null)
            {
                _connectionPreview.MarkDirtyRepaint();
            }
        }

        /// <summary>
        /// Draw connection preview using StateMachineEdge style with symmetric semicircle arc
        /// </summary>
        private void OnDrawConnectionPreview(MeshGenerationContext ctx)
        {
            if (!_isDraggingConnection) return;
            
            var graphView = GetFirstAncestorOfType<GraphView>();
            if (graphView == null) return;

            var painter = ctx.painter2D;
            
            var localStartPos = graphView.WorldToLocal(_connectionStartPos);
            var localMousePos = graphView.WorldToLocal(_currentMousePos);
            
            var direction = localMousePos - localStartPos;
            var distance = direction.magnitude;
            
            if (distance < 10f) return;
            
            var (control1, control2) = CalculateSymmetricSemicircleControlPoints(localStartPos, localMousePos);
            
            painter.strokeColor = new Color(0.8f, 0.2f, 0.2f, 0.8f);
            painter.lineWidth = 3f;
            painter.lineCap = LineCap.Round;
            painter.lineJoin = LineJoin.Round;

            painter.BeginPath();
            painter.MoveTo(localStartPos);
            painter.BezierCurveTo(control1, control2, localMousePos);
            painter.Stroke();

            DrawArrowHead(painter, localMousePos, (localMousePos - control2).normalized);
            DrawConnectionPoint(painter, localStartPos, 4f);
        }

        /// <summary>
        /// Calculate control points for symmetric semicircle arc curves (same as StateMachineEdge)
        /// </summary>
        private (Vector2, Vector2) CalculateSymmetricSemicircleControlPoints(Vector2 start, Vector2 end)
        {
            var direction = end - start;
            var distance = direction.magnitude;
            var midPoint = (start + end) * 0.5f;

            Vector2 perpendicular = new Vector2(-direction.y, direction.x).normalized;

            float arcHeight = 80f;
            arcHeight = Mathf.Lerp(40f, 120f, Mathf.Clamp01(distance / 300f));

            bool shouldArcLeft = DetermineSymmetricArcDirection(start, end);
            float arcMultiplier = shouldArcLeft ? -1f : 1f;

            Vector2 arcCenter = midPoint + perpendicular * arcHeight * arcMultiplier;

            float controlDistance = distance * 0.552f;
            Vector2 tangent = direction.normalized;

            Vector2 control1 = start + tangent * (controlDistance * 0.5f) + perpendicular * (arcHeight * 0.8f * arcMultiplier);
            Vector2 control2 = end - tangent * (controlDistance * 0.5f) + perpendicular * (arcHeight * 0.8f * arcMultiplier);

            return (control1, control2);
        }

        /// <summary>
        /// Determine symmetric arc direction for balanced visual layout
        /// </summary>
        private bool DetermineSymmetricArcDirection(Vector2 start, Vector2 end)
        {
            Vector2 nodeDirection = end - start;

            if (nodeDirection.x >= 0 && nodeDirection.y <= 0)
            {
                return false;
            }
            else if (nodeDirection.x <= 0 && nodeDirection.y <= 0)
            {
                return true;
            }
            else if (nodeDirection.x <= 0 && nodeDirection.y >= 0)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        /// <summary>
        /// Draw arrow head at the connection end
        /// </summary>
        private void DrawArrowHead(Painter2D painter, Vector2 tip, Vector2 direction)
        {
            var arrowSize = 12f;
            var arrowAngle = 20f * Mathf.Deg2Rad;

            var arrowDir1 = new Vector2(
                direction.x * Mathf.Cos(arrowAngle) - direction.y * Mathf.Sin(arrowAngle),
                direction.x * Mathf.Sin(arrowAngle) + direction.y * Mathf.Cos(arrowAngle)
            ) * arrowSize;

            var arrowDir2 = new Vector2(
                direction.x * Mathf.Cos(-arrowAngle) - direction.y * Mathf.Sin(-arrowAngle),
                direction.x * Mathf.Sin(-arrowAngle) + direction.y * Mathf.Cos(-arrowAngle)
            ) * arrowSize;

            painter.BeginPath();
            painter.MoveTo(tip);
            painter.LineTo(tip - arrowDir1);
            painter.LineTo(tip - arrowDir2);
            painter.ClosePath();
            painter.fillColor = painter.strokeColor;
            painter.Fill();
        }

        /// <summary>
        /// Draw connection point at start/end
        /// </summary>
        private void DrawConnectionPoint(Painter2D painter, Vector2 position, float radius)
        {
            painter.BeginPath();
            painter.Arc(position, radius, 0, 360);
            painter.fillColor = painter.strokeColor;
            painter.Fill();
        }

        private void SetHoverHighlight(StateNodeView targetNode)
        {
            targetNode?.SetConnectionHighlight(true);
        }

        private void ClearHoverHighlight(StateNodeView targetNode)
        {
            targetNode?.SetConnectionHighlight(false);
        }

        public void SetConnectionHighlight(bool highlight)
        {
            if (_mainContainer == null) return;

            if (highlight)
            {
                _mainContainer.style.borderTopColor = new Color(0f, 1f, 0f, 1f);
                _mainContainer.style.borderBottomColor = new Color(0f, 1f, 0f, 1f);
                _mainContainer.style.borderLeftColor = new Color(0f, 1f, 0f, 1f);
                _mainContainer.style.borderRightColor = new Color(0f, 1f, 0f, 1f);
                _mainContainer.style.borderTopWidth = 3;
                _mainContainer.style.borderBottomWidth = 3;
                _mainContainer.style.borderLeftWidth = 3;
                _mainContainer.style.borderRightWidth = 3;
            }
            else
            {
                var stateType = state.GetType();
                if (StateConfigs.TryGetValue(stateType, out var config))
                {
                    _mainContainer.style.borderTopColor = config.PrimaryColor;
                    _mainContainer.style.borderBottomColor = config.PrimaryColor;
                    _mainContainer.style.borderLeftColor = config.PrimaryColor;
                    _mainContainer.style.borderRightColor = config.PrimaryColor;
                }
                _mainContainer.style.borderTopWidth = 2;
                _mainContainer.style.borderBottomWidth = 2;
                _mainContainer.style.borderLeftWidth = 2;
                _mainContainer.style.borderRightWidth = 2;
            }
        }

        private void DestroyConnectionPreview()
        {
            if (_connectionPreview != null)
            {
                _connectionPreview.RemoveFromHierarchy();
                _connectionPreview = null;
            }
        }

        private StateNodeView FindNodeUnderPosition(Vector2 worldPos)
        {
            var graphView = GetFirstAncestorOfType<GraphView>();
            if (graphView == null) return null;

            StateNodeView bestMatch = null;
            float closestDistance = float.MaxValue;

            foreach (var element in graphView.graphElements)
            {
                if (element is StateNodeView stateNode && stateNode != this)
                {
                    var nodeWorldBounds = stateNode.worldBound;
                    var expandedBounds = new Rect(
                        nodeWorldBounds.x - 20, 
                        nodeWorldBounds.y - 20, 
                        nodeWorldBounds.width + 40, 
                        nodeWorldBounds.height + 40
                    );
                    
                    if (expandedBounds.Contains(worldPos))
                    {
                        var nodeCenter = new Vector2(
                            nodeWorldBounds.x + nodeWorldBounds.width * 0.5f,
                            nodeWorldBounds.y + nodeWorldBounds.height * 0.5f
                        );
                        var distance = Vector2.Distance(worldPos, nodeCenter);
                        
                        if (distance < closestDistance)
                        {
                            closestDistance = distance;
                            bestMatch = stateNode;
                        }
                    }
                }
            }
            
            return bestMatch;
        }

        private void CreateConnectionToNode(StateNodeView targetNode, Vector2 mouseWorldPos)
        {
            var bestOutputPort = GetClosestOutputPort(mouseWorldPos);
            var bestInputPort = targetNode.GetClosestInputPort(mouseWorldPos);
            
            if (bestOutputPort == null || bestInputPort == null)
            {
                Debug.LogError("No suitable ports found for connection");
                return;
            }

            var graphView = GetFirstAncestorOfType<StateMachineGraphContainer>();
            if (graphView == null)
            {
                Debug.LogError("GraphView not found");
                return;
            }
            
            foreach (var existingEdge in bestOutputPort.connections)
            {
                if (existingEdge.input?.node == targetNode)
                {
                    Debug.LogWarning($"Connection already exists between {state.name} and {targetNode.state.name}");
                    return;
                }
            }
            
            var edge = new StateMachineEdge();
            edge.output = bestOutputPort;
            edge.input = bestInputPort;
            
            bestOutputPort.Connect(edge);
            bestInputPort.Connect(edge);
            
            graphView.AddElement(edge);
            
            var transition = new StateTransition(targetNode.state);
            
            if (!(state is EntryStateNode))
            {
                
            }
            
            state.AddStateTransition(transition);
            EditorUtility.SetDirty(state);
            
            edge.SetStateTransition(transition, state);
            edge.OnEdgeClicked = graphView.OnEdgeClicked;
     
            graphView.schedule.Execute(() =>
            {
                edge.UpdateEdgeControl();
                edge.MarkDirtyRepaint();
                graphView.MarkDirtyRepaint();
            }).ExecuteLater(10);
            
            Debug.Log($"Successfully created connection from {state.name} to {targetNode.state.name}");
        }

        private void HidePortTypeLabel(Port port)
        {
            port.schedule.Execute(() =>
            {
                var typeLabel = port.Q<Label>();
                if (typeLabel != null) typeLabel.style.display = DisplayStyle.None;

                port.Query<Label>().ForEach(label =>
                {
                    if (label.text == "Boolean" || label.text == "bool" || label.text.ToLower().Contains("bool")) 
                        label.style.display = DisplayStyle.None;
                });
            });
        }

        private void ApplyProfessionalStyling()
        {
            var stateType = state.GetType();
            if (StateConfigs.TryGetValue(stateType, out var config))
            {
                _stateIcon.text = config.Icon;
                _stateIcon.style.color = config.AccentColor;
                _stateTypeLabel.text = config.TypeName;

                _mainContainer.style.borderTopColor = config.PrimaryColor;
                _mainContainer.style.borderBottomColor = config.PrimaryColor;
                _mainContainer.style.borderLeftColor = config.PrimaryColor;
                _mainContainer.style.borderRightColor = config.PrimaryColor;

                _headerContainer.style.backgroundColor = Color.Lerp(config.PrimaryColor, config.SecondaryColor, 0.3f);
                _contentContainer.style.backgroundColor = new Color(0, 0, 0, 0.1f);

                AddToClassList($"{stateType.Name.ToLower()}-state");
            }

            style.marginTop = 2;
            style.marginBottom = 2;
            style.marginLeft = 2;
            style.marginRight = 2;

            AddToClassList("professional-state");
        }

        private void SetupInteractions()
        {
            RegisterCallback<MouseEnterEvent>(OnMouseEnter);
            RegisterCallback<MouseLeaveEvent>(OnMouseLeave);
            RegisterCallback<MouseDownEvent>(OnMouseDown);
        }

        private void OnMouseEnter(MouseEnterEvent evt)
        {
            _mainContainer.style.scale = new StyleScale(new Vector2(1.05f, 1.05f));

            var stateType = state.GetType();
            if (StateConfigs.TryGetValue(stateType, out var config))
            {
                var brighterColor = Color.Lerp(config.PrimaryColor, Color.white, 0.2f);
                _mainContainer.style.borderTopColor = brighterColor;
                _mainContainer.style.borderBottomColor = brighterColor;
                _mainContainer.style.borderLeftColor = brighterColor;
                _mainContainer.style.borderRightColor = brighterColor;
            }
        }

        private void OnMouseLeave(MouseLeaveEvent evt)
        {
            _mainContainer.style.scale = new StyleScale(new Vector2(1f, 1f));

            var stateType = state.GetType();
            if (StateConfigs.TryGetValue(stateType, out var config))
            {
                _mainContainer.style.borderTopColor = config.PrimaryColor;
                _mainContainer.style.borderBottomColor = config.PrimaryColor;
                _mainContainer.style.borderLeftColor = config.PrimaryColor;
                _mainContainer.style.borderRightColor = config.PrimaryColor;
            }
        }

        private void OnMouseDown(MouseDownEvent evt)
        {
            if (evt.button == 0)
            {
                _mainContainer.style.translate = new StyleTranslate(new Translate(0, 1));

                schedule.Execute(() => { 
                    _mainContainer.style.translate = new StyleTranslate(new Translate(0, -1)); 
                }).StartingIn(100);
            }
        }

        private string GetFormattedStateName()
        {
            var stateName = state.name.Replace("(Clone)", "").Replace("State", "").Replace("Node", "");

            var formattedName = "";
            for (var i = 0; i < stateName.Length; i++)
            {
                if (i > 0 && char.IsUpper(stateName[i]) && !char.IsUpper(stateName[i - 1])) formattedName += " ";
                formattedName += stateName[i];
            }

            return formattedName;
        }

        public override void SetPosition(Rect newPos)
        {
            base.SetPosition(newPos);
            Undo.RecordObject(state, "State Machine (Set Position)");
            state.position.x = newPos.xMin;
            state.position.y = newPos.yMin;
            EditorUtility.SetDirty(state);
            
            schedule.Execute(() => {
                RecalculateConnections();
        
                var graphView = GetFirstAncestorOfType<StateMachineGraphContainer>();
                if (graphView != null)
                {
                    graphView.NotifyNodeMoved(this);
                }
            }).ExecuteLater(100);
        }

        public override void OnSelected()
        {
            base.OnSelected();
            OnStateSelected?.Invoke(this);

            AddToClassList("state-selected");

            _executionGlow.style.opacity = 0.6f;
            _executionGlow.style.borderTopColor = new Color(1f, 1f, 1f, 0.8f);
            _executionGlow.style.borderBottomColor = new Color(1f, 1f, 1f, 0.8f);
            _executionGlow.style.borderLeftColor = new Color(1f, 1f, 1f, 0.8f);
            _executionGlow.style.borderRightColor = new Color(1f, 1f, 1f, 0.8f);
        }

        public override void OnUnselected()
        {
            base.OnUnselected();

            RemoveFromClassList("state-selected");

            if (!Application.isPlaying) _executionGlow.style.opacity = 0;
        }

        public void UpdateState()
        {
            RemoveFromClassList("state-running");
            RemoveFromClassList("state-active");
            RemoveFromClassList("state-idle");

            if (Application.isPlaying)
            {
                var stateMachine = state.StateMachine;
                if (stateMachine != null && stateMachine.MyStateMachineSO.CurrentState == state)
                {
                    AddToClassList("state-active");
                    UpdateStatusIndicator(new Color(0.3f, 0.8f, 0.3f, 1f), true);
                    ShowExecutionGlow(new Color(0.3f, 0.8f, 0.3f, 0.8f));
                }
                else
                {
                    AddToClassList("state-idle");
                    UpdateStatusIndicator(new Color(0.5f, 0.5f, 0.5f, 0.8f), false);
                    if (!selected) _executionGlow.style.opacity = 0;
                }
            }
            else
            {
                AddToClassList("state-idle");
                UpdateStatusIndicator(new Color(0.5f, 0.5f, 0.5f, 0.8f), false);
                if (!selected) _executionGlow.style.opacity = 0;
            }

            UpdatePreviewInfo();
        }

        private void UpdateStatusIndicator(Color color, bool pulsing)
        {
            _statusIndicator.style.backgroundColor = color;

            if (pulsing)
                StartPulseAnimation();
            else
                StopPulseAnimation();
        }

        private void ShowExecutionGlow(Color glowColor)
        {
            _executionGlow.style.borderTopColor = glowColor;
            _executionGlow.style.borderBottomColor = glowColor;
            _executionGlow.style.borderLeftColor = glowColor;
            _executionGlow.style.borderRightColor = glowColor;
            _executionGlow.style.opacity = 1;
        }

        private void StartPulseAnimation()
        {
            StopPulseAnimation();

            _pulseAnimation = _statusIndicator.schedule.Execute(() =>
            {
                var time = Time.realtimeSinceStartup * 4f;
                var alpha = 0.6f + 0.4f * Mathf.Sin(time);
                var currentColor = _statusIndicator.style.backgroundColor.value;
                currentColor.a = alpha;
                _statusIndicator.style.backgroundColor = currentColor;
            }).Every(16);
        }

        private void StopPulseAnimation()
        {
            _pulseAnimation?.Pause();
            _pulseAnimation = null;
        }

        private struct StateStyleConfig
        {
            public readonly string Icon;
            public readonly Color PrimaryColor;
            public readonly Color SecondaryColor;
            public readonly Color AccentColor;
            public readonly string TypeName;

            public StateStyleConfig(string icon, Color primaryColor, Color secondaryColor, string typeName)
            {
                Icon = icon;
                PrimaryColor = primaryColor;
                SecondaryColor = secondaryColor;
                AccentColor = Color.Lerp(primaryColor, Color.white, 0.3f);
                TypeName = typeName;
            }
        }
    }
}