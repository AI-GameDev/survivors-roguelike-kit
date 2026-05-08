using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace RGame.ScriptableCoreKit
{
    public class StateMachineGraphContainer : GraphView
    {
        private const float BLACKBOARD_WIDTH = 500f;
        private const float ANIMATION_DURATION = 0.3f;

        private VisualElement _blackboardPanel;
        private Button _blackboardToggleButton;
        private BlackboardView _blackboardView;
        private ContentDragger _contentDragger;
        private ContentZoomer _contentZoomer;
        private IVisualElementScheduledItem _currentAnimation;
        private bool _isBlackboardVisible;
        private bool _isGraphMode;
        private bool _manipulatorsAdded;
        private VisualElement _placeholderContainer;
        private RectangleSelector _rectangleSelector;
        private SelectionDragger _selectionDragger;
        private StateMachineSO _stateMachine;

        public Action<StateNodeView> OnStateSelected;

        private StateMachineInspectorView _inspectorView;
        
        public StateMachineGraphContainer()
        {
            // Try different stylesheet paths
            var stylePaths = new[]
            {
                "Assets/Roofen/ScriptableCoreKitPro/StateMachine/Scripts/Editor/StateMachineWindow.uss",
                "Assets/Roofen/ScriptableCoreKitPro/BehaviourTree/Scripts/Editor/BehaviourTreeWindow.uss"
            };
            
            foreach (var path in stylePaths)
            {
                var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                if (styleSheet != null)
                {
                    styleSheets.Add(styleSheet);
                    break;
                }
            }

            SetupGraphView();
            SetupBlackboardUI();
            SetupPlaceholderContent();
            AddConnectionInstructions();
        }

        public void SetInspectorView(StateMachineInspectorView inspectorView)
        {
            _inspectorView = inspectorView;
        }
        
        /// <summary>
        /// Loads the state machine from the currently selected GameObject and displays it in the graph
        /// </summary>
        public void LoadStateMachine()
        {
            if (Selection.gameObjects != null && Selection.gameObjects.Length > 0)
            {
                var stateMachine = Selection.gameObjects[0].GetComponentInChildren<StateMachine>();

                if (stateMachine != null && stateMachine.MyStateMachineSO != null)
                {
                    _stateMachine = stateMachine.MyStateMachineSO;

                    SetBlackboardTable(_stateMachine.blackboardTable);

                    // Clear existing graph and reload
                    graphViewChanged -= OnGraphViewChanged;
                    DeleteElements(graphElements);
                    graphViewChanged += OnGraphViewChanged;

                    SetGraphMode(true);

                    // Clean up and validate states list first
                    CleanupStatesAndReferences();

                    // Only create entry state if it truly doesn't exist
                    EnsureEntryStateExists();

                    // Create view for each state
                    if (_stateMachine.States != null)
                    {
                        _stateMachine.States.ToList().ForEach(CreateStateView);
                    }

                    CreateConnectionsDelayed();
                    schedule.Execute(LoadViewState).ExecuteLater(100);
                }
            }
        }

        /// <summary>
        /// Adds visual instructions for the new connection system
        /// </summary>
        private void AddConnectionInstructions()
        {
            var instructionsContainer = new VisualElement();
            instructionsContainer.style.position = Position.Absolute;
            instructionsContainer.style.top = 50;
            instructionsContainer.style.right = 10;
            instructionsContainer.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.9f);
            instructionsContainer.style.borderTopLeftRadius = 6;
            instructionsContainer.style.borderTopRightRadius = 6;
            instructionsContainer.style.borderBottomLeftRadius = 6;
            instructionsContainer.style.borderBottomRightRadius = 6;
            instructionsContainer.style.paddingTop = 8;
            instructionsContainer.style.paddingBottom = 8;
            instructionsContainer.style.paddingLeft = 12;
            instructionsContainer.style.paddingRight = 12;
            instructionsContainer.style.borderTopWidth = 1;
            instructionsContainer.style.borderBottomWidth = 1;
            instructionsContainer.style.borderLeftWidth = 1;
            instructionsContainer.style.borderRightWidth = 1;
            instructionsContainer.style.borderTopColor = new Color(0.4f, 0.4f, 0.4f);
            instructionsContainer.style.borderBottomColor = new Color(0.4f, 0.4f, 0.4f);
            instructionsContainer.style.borderLeftColor = new Color(0.4f, 0.4f, 0.4f);
            instructionsContainer.style.borderRightColor = new Color(0.4f, 0.4f, 0.4f);

            var titleLabel = new Label("Connection Guide");
            titleLabel.style.fontSize = 10;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = new Color(0.8f, 0.8f, 0.8f);
            titleLabel.style.marginBottom = 4;

            var instructionLabel1 = new Label("• Right-click + drag to connect");
            instructionLabel1.style.fontSize = 9;
            instructionLabel1.style.color = new Color(0.7f, 0.7f, 0.7f);
            instructionLabel1.style.marginBottom = 2;

            var instructionLabel2 = new Label("• Click edge to edit transitions");
            instructionLabel2.style.fontSize = 9;
            instructionLabel2.style.color = new Color(0.7f, 0.7f, 0.7f);

            instructionsContainer.Add(titleLabel);
            instructionsContainer.Add(instructionLabel1);
            instructionsContainer.Add(instructionLabel2);

            Add(instructionsContainer);
        }

        /// <summary>
        /// Creates the default entry state for the state machine
        /// </summary>
        private void CreateEntryState()
        {
            var entryState = ScriptableObject.CreateInstance<EntryStateNode>();
            entryState.name = "Entry";
            entryState.guid = System.Guid.NewGuid().ToString();
            entryState.position = new Vector2(100, 100);
            
            _stateMachine.States.Add(entryState);
            _stateMachine.EntryState = entryState;
            
            EditorUtility.SetDirty(_stateMachine);
            AssetDatabase.SaveAssets();
        }

        private void CreateConnectionsDelayed()
        {
            schedule.Execute(() =>
            {
                nodes.ForEach(node =>
                {
                    if (node is StateNodeView stateView)
                    {
                        stateView.MarkDirtyRepaint();
                        if (stateView.inputPortTop != null) stateView.inputPortTop.MarkDirtyRepaint();
                        if (stateView.inputPortBottom != null) stateView.inputPortBottom.MarkDirtyRepaint();
                        if (stateView.outputPortLeft != null) stateView.outputPortLeft.MarkDirtyRepaint();
                        if (stateView.outputPortRight != null) stateView.outputPortRight.MarkDirtyRepaint();
                    }
                });

                schedule.Execute(() =>
                {
                    CreateConnections();
                    RefreshConnectionPositions();
                }).ExecuteLater(16);
            }).ExecuteLater(16);
        }

        private void CreateConnections()
        {
            foreach (var state in _stateMachine.States)
            {
                var fromView = FindStateView(state);
                if (fromView == null) continue;

                foreach (var transition in state.StateTransitions)
                {
                    var toView = FindStateView(transition.TargetState);
                    if (toView != null)
                    {
                        var fromCenter = GetNodeWorldCenter(fromView);
                        var toCenter = GetNodeWorldCenter(toView);
                
                        var bestOutputPort = fromView.GetClosestOutputPort(toCenter);
                        var bestInputPort = toView.GetClosestInputPort(fromCenter);
                
                        if (bestOutputPort != null && bestInputPort != null)
                        {
                            bool connectionExists = bestOutputPort.connections
                                .Any(conn => conn.input?.node == toView);
                    
                            if (!connectionExists)
                            {
                                var edge = new StateMachineEdge();
                                edge.output = bestOutputPort;
                                edge.input = bestInputPort;
                                edge.SetStateTransition(transition, state);
                                edge.OnEdgeClicked = OnEdgeClicked;
                                
                                bestOutputPort.Connect(edge);
                                bestInputPort.Connect(edge);
                        
                                AddElement(edge);
                            }
                        }
                    }
                }
            }
        }
        
        public void NotifyNodeMoved(StateNodeView movedNode)
        {
            schedule.Execute(() => RecalculateConnectionsForNode(movedNode)).ExecuteLater(100);
        }
        
        private void RecalculateConnectionsForNode(StateNodeView nodeView)
        {
            var allEdges = new List<StateMachineEdge>();
            
            if (nodeView.outputPortLeft != null)
                allEdges.AddRange(nodeView.outputPortLeft.connections.Cast<StateMachineEdge>());
            if (nodeView.outputPortRight != null)
                allEdges.AddRange(nodeView.outputPortRight.connections.Cast<StateMachineEdge>());
            if (nodeView.inputPortTop != null)
                allEdges.AddRange(nodeView.inputPortTop.connections.Cast<StateMachineEdge>());
            if (nodeView.inputPortBottom != null)
                allEdges.AddRange(nodeView.inputPortBottom.connections.Cast<StateMachineEdge>());
            
            foreach (var edge in allEdges.Distinct())
            {
                if (edge.output?.node is StateNodeView sourceNode && 
                    edge.input?.node is StateNodeView targetNode)
                {
                    var sourceCenter = GetNodeWorldCenter(sourceNode);
                    var targetCenter = GetNodeWorldCenter(targetNode);
                    
                    var bestOutputPort = sourceNode.GetClosestOutputPort(targetCenter);
                    var bestInputPort = targetNode.GetClosestInputPort(sourceCenter);
                    
                    if (bestOutputPort != null && bestInputPort != null &&
                        (edge.output != bestOutputPort || edge.input != bestInputPort))
                    {
                        edge.output?.Disconnect(edge);
                        edge.input?.Disconnect(edge);
                        
                        edge.output = bestOutputPort;
                        edge.input = bestInputPort;
                        bestOutputPort.Connect(edge);
                        bestInputPort.Connect(edge);
                        
                        schedule.Execute(() =>
                        {
                            edge.UpdateEdgeControl();
                            edge.MarkDirtyRepaint();
                        }).ExecuteLater(10);
                    }
                }
            }
        }
        
        public void OnEdgeClicked(StateMachineEdge edge)
        {
            _inspectorView?.ShowTransitionEditor(edge);
        }
        
        private void RefreshConnectionPositions()
        {
            edges.ForEach(edge =>
            {
                if (edge != null)
                {
                    edge.UpdateEdgeControl();
                    edge.MarkDirtyRepaint();
                }
            });

            schedule.Execute(() => { MarkDirtyRepaint(); }).ExecuteLater(1);
        }

        private StateNodeView FindStateView(ActionStateNode state)
        {
            return GetNodeByGuid(state.guid) as StateNodeView;
        }

        /// <summary>
        /// Checks if a connection already exists between two nodes
        /// </summary>
        private bool DoesConnectionExistBetweenNodes(StateNodeView fromNode, StateNodeView toNode)
        {
            if (fromNode == null || toNode == null) return false;
            
            // Check all output ports of the source node
            var outputPorts = new List<Port>();
            if (fromNode.outputPortLeft != null) outputPorts.Add(fromNode.outputPortLeft);
            if (fromNode.outputPortRight != null) outputPorts.Add(fromNode.outputPortRight);
            
            foreach (var outputPort in outputPorts)
            {
                foreach (var connection in outputPort.connections)
                {
                    if (connection.input?.node == toNode)
                    {
                        return true;
                    }
                }
            }
            
            return false;
        }

        /// <summary>
        /// Optimizes port connection by selecting the closest ports between two nodes
        /// </summary>
        private Edge OptimizePortConnection(Edge originalEdge, StateNodeView fromNode, StateNodeView toNode)
        {
            if (fromNode == null || toNode == null) return originalEdge;
            
            // Get world positions of both nodes
            var fromCenter = GetNodeWorldCenter(fromNode);
            var toCenter = GetNodeWorldCenter(toNode);
            
            // Find the best output port from source node
            var bestOutputPort = fromNode.GetClosestOutputPort(toCenter);
            
            // Find the best input port on target node
            var bestInputPort = toNode.GetClosestInputPort(fromCenter);
            
            if (bestOutputPort != null && bestInputPort != null)
            {
                // Create optimized edge
                var optimizedEdge = new StateMachineEdge();
                optimizedEdge.output = bestOutputPort;
                optimizedEdge.input = bestInputPort;
                return optimizedEdge;
            }
            
            return originalEdge;
        }

        /// <summary>
        /// Handles graph view changes including element deletion and edge creation
        /// Prevents deletion of EntryStateNode and manages state transitions
        /// Enhanced to work with Animator-style connections
        /// </summary>
        private GraphViewChange OnGraphViewChanged(GraphViewChange graphViewChange)
        {
            bool hasDeleted = false;

            if (graphViewChange.elementsToRemove != null)
            {
                var elementsToRemoveFiltered = new List<GraphElement>();

                foreach (var elem in graphViewChange.elementsToRemove)
                {
                    if (elem is StateNodeView stateView)
                    {
                        if (stateView.state is EntryStateNode)
                        {
                            EditorUtility.DisplayDialog("Cannot Delete", "Entry state cannot be deleted!", "OK");
                            continue;
                        }
                        
                        DeleteAllEdgesConnectedToNode(stateView);
                        
                        if (_stateMachine.States.Contains(stateView.state))
                        {
                            _stateMachine.States.Remove(stateView.state);
                        }
                        
                        if (_stateMachine.EntryState == stateView.state)
                        {
                            _stateMachine.EntryState = null;
                        }
                        
                        RemoveTransitionsToState(stateView.state);
                        
                        if (AssetDatabase.Contains(stateView.state))
                        {
                            AssetDatabase.RemoveObjectFromAsset(stateView.state);
                        }

                        EditorUtility.SetDirty(_stateMachine);
                        elementsToRemoveFiltered.Add(elem);
                        hasDeleted = true;
                    }
                    else if (elem is StateMachineEdge edge)
                    {
                        RemoveEdgeTransition(edge);
                        elementsToRemoveFiltered.Add(elem);
                        hasDeleted = true;
                    }
                    else
                    {
                        elementsToRemoveFiltered.Add(elem);
                    }
                }

                graphViewChange.elementsToRemove = elementsToRemoveFiltered;
                
                if (hasDeleted)
                {
                    if (AssetDatabase.Contains(_stateMachine))
                    {
                        AssetDatabase.SaveAssets();
                        AssetDatabase.Refresh();
                    }
                    
                    schedule.Execute(() =>
                    {
                        ValidateAndCleanupElements();
                        ForceRefresh();
                    }).ExecuteLater(100);
                }
            }

            if (graphViewChange.edgesToCreate != null)
            {
                var customEdges = new List<Edge>();

                foreach (var edge in graphViewChange.edgesToCreate)
                {
                    var fromView = edge.output?.node as StateNodeView;
                    var toView = edge.input?.node as StateNodeView;
                    if (fromView != null && toView != null)
                    {
                        bool connectionExists = DoesConnectionExistBetweenNodes(fromView, toView);

                        if (!connectionExists)
                        {
                            var optimizedEdge = OptimizePortConnection(edge, fromView, toView);

                            var transition = new StateTransition(toView.state);

                            fromView.state.AddStateTransition(transition);
                            EditorUtility.SetDirty(fromView.state);

                            StateMachineEdge customEdge;
                            if (optimizedEdge is StateMachineEdge)
                            {
                                customEdge = optimizedEdge as StateMachineEdge;
                            }
                            else
                            {
                                customEdge = new StateMachineEdge();
                                customEdge.output = optimizedEdge.output;
                                customEdge.input = optimizedEdge.input;
                            }

                            customEdge.SetStateTransition(transition, fromView.state);
                            customEdge.OnEdgeClicked = OnEdgeClicked;
                            customEdges.Add(customEdge);
                        }
                        else
                        {
                            Debug.LogWarning($"Connection from {fromView.state.name} to {toView.state.name} already exists!");
                        }
                    }
                }

                graphViewChange.edgesToCreate = customEdges;
            }

            return graphViewChange;
        }

        public void ForceRefresh()
        {
            schedule.Execute(() =>
            {
                foreach (var element in graphElements)
                {
                    element.MarkDirtyRepaint();
                }
                
                MarkDirtyRepaint();
                
                RefreshConnectionPositions();
        
            }).ExecuteLater(1);
        }

        public void ValidateAndCleanupElements()
        {
            var elementsToRemove = new List<GraphElement>();
            
            foreach (var element in graphElements)
            {
                if (element is StateNodeView stateView)
                {
                    if (!_stateMachine.States.Contains(stateView.state))
                    {
                        elementsToRemove.Add(element);
                    }
                }
                else if (element is StateMachineEdge edge)
                {
                    if (edge.output?.node == null || edge.input?.node == null)
                    {
                        elementsToRemove.Add(element);
                    }
                    else if (edge.output.node is StateNodeView fromView && 
                             edge.input.node is StateNodeView toView)
                    {
                        if (!_stateMachine.States.Contains(fromView.state) || 
                            !_stateMachine.States.Contains(toView.state))
                        {
                            elementsToRemove.Add(element);
                        }
                    }
                }
            }
            
            if (elementsToRemove.Count > 0)
            {
                DeleteElements(elementsToRemove);
                ForceRefresh();
            }
        }
        
        private void DeleteAllEdgesConnectedToNode(StateNodeView nodeView)
        {
            var edgesToDelete = new List<StateMachineEdge>();
            
            foreach (var element in graphElements)
            {
                if (element is StateMachineEdge edge)
                {
                    if ((edge.output?.node == nodeView) || (edge.input?.node == nodeView))
                    {
                        edgesToDelete.Add(edge);
                    }
                }
            }
            
            foreach (var edge in edgesToDelete)
            {
                RemoveEdgeTransition(edge);
                RemoveElement(edge);
            }
        }
        
        private void RemoveEdgeTransition(StateMachineEdge edge)
        {
            var fromView = edge.output?.node as StateNodeView;
            var toView = edge.input?.node as StateNodeView;
    
            if (fromView != null && toView != null)
            {
                var transitionToRemove = fromView.state.StateTransitions
                    .FirstOrDefault(t => t.TargetState == toView.state && t == edge.StateTransition);

                if (transitionToRemove != null)
                {
                    fromView.state.RemoveStateTransition(transitionToRemove);
                    EditorUtility.SetDirty(fromView.state);
                }
            }
            
            if (edge.output != null)
            {
                edge.output.Disconnect(edge);
            }
            if (edge.input != null)
            {
                edge.input.Disconnect(edge);
            }
        }
        
        private void RemoveTransitionsToState(ActionStateNode targetState)
        {
            int removedCount = 0;
            foreach (var state in _stateMachine.States)
            {
                if (state != null && state != targetState)
                {
                    var transitionsToRemove = state.StateTransitions
                        .Where(t => t.TargetState == targetState)
                        .ToList();
            
                    foreach (var transition in transitionsToRemove)
                    {
                        state.RemoveStateTransition(transition);
                        EditorUtility.SetDirty(state);
                        removedCount++;
                    }
                }
            }
        }
        
        private void SetupGraphView()
        {
            _contentDragger = new ContentDragger();
            _selectionDragger = new SelectionDragger();
            _rectangleSelector = new RectangleSelector();
            _contentZoomer = new ContentZoomer();

            this.AddManipulator(new ViewStateCallbackHandler(this));
        }

        private void SetupBlackboardUI()
        {
            _blackboardToggleButton = new Button(ToggleBlackboard);
            _blackboardToggleButton.text = "▤";
            _blackboardToggleButton.style.position = Position.Absolute;
            _blackboardToggleButton.style.top = 10;
            _blackboardToggleButton.style.left = 10;
            _blackboardToggleButton.style.width = 24;
            _blackboardToggleButton.style.height = 24;
            _blackboardToggleButton.style.fontSize = 14;
            _blackboardToggleButton.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 0.9f);
            _blackboardToggleButton.style.borderTopWidth = 1;
            _blackboardToggleButton.style.borderBottomWidth = 1;
            _blackboardToggleButton.style.borderLeftWidth = 1;
            _blackboardToggleButton.style.borderRightWidth = 1;
            _blackboardToggleButton.style.borderTopColor = new Color(0.5f, 0.5f, 0.5f);
            _blackboardToggleButton.style.borderBottomColor = new Color(0.5f, 0.5f, 0.5f);
            _blackboardToggleButton.style.borderLeftColor = new Color(0.5f, 0.5f, 0.5f);
            _blackboardToggleButton.style.borderRightColor = new Color(0.5f, 0.5f, 0.5f);
            _blackboardToggleButton.style.borderTopLeftRadius = 4;
            _blackboardToggleButton.style.borderTopRightRadius = 4;
            _blackboardToggleButton.style.borderBottomLeftRadius = 4;
            _blackboardToggleButton.style.borderBottomRightRadius = 4;

            _blackboardPanel = new VisualElement();
            _blackboardPanel.style.position = Position.Absolute;
            _blackboardPanel.style.top = 10;
            _blackboardPanel.style.left = -BLACKBOARD_WIDTH;
            _blackboardPanel.style.width = BLACKBOARD_WIDTH;
            _blackboardPanel.style.height = Length.Percent(80);
            _blackboardPanel.style.backgroundColor = EditorGUIUtility.isProSkin ? new Color(0.2f, 0.2f, 0.2f, 0.95f) : new Color(0.9f, 0.9f, 0.9f, 0.95f);
            _blackboardPanel.style.borderTopWidth = 2;
            _blackboardPanel.style.borderBottomWidth = 2;
            _blackboardPanel.style.borderLeftWidth = 2;
            _blackboardPanel.style.borderRightWidth = 2;
            _blackboardPanel.style.borderTopColor = EditorGUIUtility.isProSkin ? new Color(0.4f, 0.4f, 0.4f) : new Color(0.6f, 0.6f, 0.6f);
            _blackboardPanel.style.borderBottomColor = EditorGUIUtility.isProSkin ? new Color(0.4f, 0.4f, 0.4f) : new Color(0.6f, 0.6f, 0.6f);
            _blackboardPanel.style.borderLeftColor = EditorGUIUtility.isProSkin ? new Color(0.4f, 0.4f, 0.4f) : new Color(0.6f, 0.6f, 0.6f);
            _blackboardPanel.style.borderRightColor = EditorGUIUtility.isProSkin ? new Color(0.4f, 0.4f, 0.4f) : new Color(0.6f, 0.6f, 0.6f);
            _blackboardPanel.style.borderTopLeftRadius = 0;
            _blackboardPanel.style.borderTopRightRadius = 8;
            _blackboardPanel.style.borderBottomLeftRadius = 0;
            _blackboardPanel.style.borderBottomRightRadius = 8;
            _blackboardPanel.style.display = DisplayStyle.Flex;

            var blackboardHeader = new Label("Blackboard");
            blackboardHeader.style.fontSize = 14;
            blackboardHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            blackboardHeader.style.color = EditorGUIUtility.isProSkin ? new Color(0.9f, 0.9f, 0.9f) : new Color(0.1f, 0.1f, 0.1f);
            blackboardHeader.style.paddingTop = 10;
            blackboardHeader.style.paddingBottom = 8;
            blackboardHeader.style.paddingLeft = 10;
            blackboardHeader.style.paddingRight = 10;
            blackboardHeader.style.borderBottomWidth = 1;
            blackboardHeader.style.borderBottomColor = EditorGUIUtility.isProSkin ? new Color(0.3f, 0.3f, 0.3f) : new Color(0.7f, 0.7f, 0.7f);

            _blackboardView = new BlackboardView();
            _blackboardView.style.height = Length.Percent(100);
            _blackboardView.style.paddingTop = 10;
            _blackboardView.style.paddingBottom = 10;
            _blackboardView.style.paddingLeft = 10;
            _blackboardView.style.paddingRight = 10;

            _blackboardPanel.Add(blackboardHeader);
            _blackboardPanel.Add(_blackboardView);

            Add(_blackboardPanel);
            Add(_blackboardToggleButton);
        }

        private void SetupPlaceholderContent()
        {
            _placeholderContainer = new VisualElement();
            _placeholderContainer.style.position = Position.Absolute;
            _placeholderContainer.style.top = 0;
            _placeholderContainer.style.left = 0;
            _placeholderContainer.style.right = 0;
            _placeholderContainer.style.bottom = 0;
            _placeholderContainer.style.justifyContent = Justify.Center;
            _placeholderContainer.style.alignItems = Align.Center;
            _placeholderContainer.style.flexDirection = FlexDirection.Column;

            var titleLabel = new Label("State Machine Graph");
            titleLabel.style.fontSize = 18;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = EditorGUIUtility.isProSkin ? new Color(0.7f, 0.7f, 0.7f, 0.8f) : new Color(0.4f, 0.4f, 0.4f, 0.8f);
            titleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            titleLabel.style.marginBottom = 8;

            var descLabel = new Label("Animator-style state editor with\nright-click drag connections");
            descLabel.style.fontSize = 12;
            descLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            descLabel.style.color = EditorGUIUtility.isProSkin ? new Color(0.6f, 0.6f, 0.6f, 0.8f) : new Color(0.5f, 0.5f, 0.5f, 0.8f);
            descLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            descLabel.style.whiteSpace = WhiteSpace.Normal;
            descLabel.style.marginBottom = 16;

            _placeholderContainer.Add(titleLabel);
            _placeholderContainer.Add(descLabel);

            Add(_placeholderContainer);
        }

        private void SetManipulatorsEnabled(bool enabled)
        {
            if (enabled && !_manipulatorsAdded)
            {
                this.AddManipulator(_contentDragger);
                this.AddManipulator(_selectionDragger);
                this.AddManipulator(_rectangleSelector);
                this.AddManipulator(_contentZoomer);
                _manipulatorsAdded = true;
            }
            else if (!enabled && _manipulatorsAdded)
            {
                this.RemoveManipulator(_contentDragger);
                this.RemoveManipulator(_selectionDragger);
                this.RemoveManipulator(_rectangleSelector);
                this.RemoveManipulator(_contentZoomer);
                _manipulatorsAdded = false;
            }
        }

        private void ToggleBlackboard()
        {
            _isBlackboardVisible = !_isBlackboardVisible;
            AnimateBlackboardPanel();
        }

        private void AnimateBlackboardPanel()
        {
            _blackboardToggleButton.text = _isBlackboardVisible ? "◀" : "▤";

            var targetLeft = _isBlackboardVisible ? 10 : -BLACKBOARD_WIDTH;
            var currentLeft = _blackboardPanel.style.left.value.value;

            if (Mathf.Approximately(currentLeft, targetLeft))
                return;

            var startLeft = currentLeft;
            var startTime = Time.realtimeSinceStartup;

            _currentAnimation?.Pause();

            _currentAnimation = _blackboardPanel.schedule.Execute(() =>
            {
                var elapsed = Time.realtimeSinceStartup - startTime;
                var progress = Mathf.Clamp01(elapsed / ANIMATION_DURATION);

                var easedProgress = EaseOutCubic(progress);
                var newLeft = Mathf.Lerp(startLeft, targetLeft, easedProgress);

                _blackboardPanel.style.left = newLeft;

                if (_isBlackboardVisible)
                    _blackboardToggleButton.style.left = Mathf.Lerp(10, BLACKBOARD_WIDTH + 20, easedProgress);
                else
                    _blackboardToggleButton.style.left = Mathf.Lerp(BLACKBOARD_WIDTH + 20, 10, easedProgress);

                if (progress >= 1.0f)
                {
                    _blackboardPanel.style.left = targetLeft;
                    _currentAnimation?.Pause();
                }
            }).Every(16);
        }

        private float EaseOutCubic(float t)
        {
            return 1f - Mathf.Pow(1f - t, 3f);
        }

        /// <summary>
        /// Enhanced port compatibility for Animator-style connections
        /// Still uses the existing port system internally but with more flexible rules
        /// </summary>
        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var compatiblePorts = new List<Port>();

            ports.ForEach(port =>
            {
                if (startPort != port && 
                    startPort.direction != port.direction)
                {
                    if (startPort.direction == Direction.Output && 
                        port.direction == Direction.Input && 
                        startPort.node == port.node)
                    {
                        return; 
                    }
                    
                    compatiblePorts.Add(port);
                }
            });

            return compatiblePorts;
        }

        private void SaveViewState()
        {
            if (_stateMachine != null)
            {
                if (_stateMachine.viewState == null) _stateMachine.viewState = new ViewState();

                _stateMachine.viewState.position = viewTransform.position;
                _stateMachine.viewState.scale = viewTransform.scale;
                EditorUtility.SetDirty(_stateMachine);
            }
        }

        private void LoadViewState()
        {
            if (_stateMachine != null && _stateMachine.viewState != null) 
                UpdateViewTransform(_stateMachine.viewState.position, _stateMachine.viewState.scale);
        }

        public void SetGraphMode(bool isGraphMode)
        {
            _isGraphMode = isGraphMode;
            _blackboardToggleButton.visible = isGraphMode;

            if (_isGraphMode)
            {
                var gridBackground = new GridBackground();
                Insert(0, gridBackground);
              
                gridBackground.StretchToParentSize();
       
                schedule.Execute(() => {
                    gridBackground.MarkDirtyRepaint();
                    MarkDirtyRepaint();
                }).ExecuteLater(1);
                
                SetManipulatorsEnabled(true);

                if (_placeholderContainer != null) _placeholderContainer.style.display = DisplayStyle.None;
                if (_blackboardToggleButton != null) _blackboardToggleButton.style.display = DisplayStyle.Flex;
                if (_blackboardPanel != null) _blackboardPanel.style.display = DisplayStyle.Flex;
            }
            else
            {
                // Exit graph editing mode
                var gridBackground = this.Q<GridBackground>();
                if (gridBackground != null)
                {
                    Remove(gridBackground);
                }
                
                SetManipulatorsEnabled(false);

                if (_placeholderContainer != null) _placeholderContainer.style.display = DisplayStyle.Flex;
                if (_blackboardToggleButton != null) _blackboardToggleButton.style.display = DisplayStyle.Flex;
                if (_blackboardPanel != null)
                {
                    _blackboardPanel.style.display = DisplayStyle.Flex;
                    if (!_isBlackboardVisible) _blackboardPanel.style.left = -BLACKBOARD_WIDTH;
                }
            }
        }

         /// <summary>
        /// Cleans up null references and duplicate states
        /// </summary>
        private void CleanupStatesAndReferences()
        {
            if (_stateMachine.States == null)
            {
                _stateMachine.States = new List<ActionStateNode>();
                EditorUtility.SetDirty(_stateMachine);
                return;
            }

            // Remove null references
            var validStates = new List<ActionStateNode>();
            var entryStates = new List<EntryStateNode>();

            foreach (var state in _stateMachine.States)
            {
                if (state != null)
                {
                    // Ensure GUID exists
                    if (string.IsNullOrEmpty(state.guid))
                    {
                        state.guid = System.Guid.NewGuid().ToString();
                        EditorUtility.SetDirty(state);
                    }

                    validStates.Add(state);

                    // Collect entry states to handle duplicates
                    if (state is EntryStateNode entryState)
                    {
                        entryStates.Add(entryState);
                    }
                }
            }

            // Handle multiple entry states - keep only the first one or the one referenced by entryState field
            if (entryStates.Count > 1)
            {
                Debug.LogWarning($"Found {entryStates.Count} EntryStateNodes. Cleaning up duplicates...");
                
                EntryStateNode keepEntry = null;
                
                // Prioritize the one referenced by entryState field
                if (_stateMachine.EntryState != null && entryStates.Contains(_stateMachine.EntryState as EntryStateNode))
                {
                    keepEntry = _stateMachine.EntryState as EntryStateNode;
                }
                else
                {
                    // Otherwise keep the first one
                    keepEntry = entryStates[0];
                }

                // Remove duplicates from validStates
                validStates.RemoveAll(s => s is EntryStateNode && s != keepEntry);
                
                // Update entryState reference
                _stateMachine.EntryState = keepEntry;
            }
            else if (entryStates.Count == 1)
            {
                // Ensure entryState field points to the existing entry state
                _stateMachine.EntryState = entryStates[0];
            }

            // Update the states list
            _stateMachine.States = validStates;
            EditorUtility.SetDirty(_stateMachine);
        }
         
        /// <summary>
        /// Ensures an entry state exists, creating one only if necessary
        /// </summary>
        private void EnsureEntryStateExists()
        {
            // Check if we already have a valid entry state
            var existingEntry = _stateMachine.States?.OfType<EntryStateNode>().FirstOrDefault();
            
            if (existingEntry != null)
            {
                // We have an entry state, make sure it's referenced correctly
                _stateMachine.EntryState = existingEntry;
                EditorUtility.SetDirty(_stateMachine);
                return;
            }

            // No entry state exists, create one
            CreateNewEntryState();
        }

        /// <summary>
        /// Creates a new entry state (only when none exists)
        /// </summary>
        private void CreateNewEntryState()
        {
            var entryState = ScriptableObject.CreateInstance<EntryStateNode>();
            entryState.name = "Entry";
            entryState.guid = System.Guid.NewGuid().ToString();
            entryState.position = new Vector2(100, 100);
            
            // Add to asset if StateMachineSO is an asset
            if (AssetDatabase.Contains(_stateMachine))
            {
                AssetDatabase.AddObjectToAsset(entryState, _stateMachine);
            }
            
            _stateMachine.States.Add(entryState);
            _stateMachine.EntryState = entryState;
            
            EditorUtility.SetDirty(_stateMachine);
            EditorUtility.SetDirty(entryState);
            
            if (AssetDatabase.Contains(_stateMachine))
            {
                AssetDatabase.SaveAssets();
            }
            
            Debug.Log("Created new EntryStateNode");
        }
        
        private void CreateStateView(ActionStateNode state)
        {
            var stateView = new StateNodeView(state);
            stateView.OnStateSelected = OnStateSelected;
            AddElement(stateView);
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            var mousePosition = evt.localMousePosition;
            var types = TypeCache.GetTypesDerivedFrom<ActionStateNode>()
                .Where(type => !type.IsAbstract && type != typeof(EntryStateNode));
            
            var worldPosition = this.LocalToWorld(mousePosition);
            TypeSelectionPanel.Show(worldPosition, types, "Create State", type => CreateStateAtPosition(type, mousePosition));
    
            evt.StopPropagation();
        }


        private void CreateStateAtPosition(Type type, Vector2 position)
        {
            // Prevent creation of multiple EntryStateNodes
            if (type == typeof(EntryStateNode))
            {
                Debug.LogWarning("EntryStateNode cannot be created manually. Only one entry state is allowed.");
                return;
            }

            var state = ScriptableObject.CreateInstance(type) as ActionStateNode;
            if (state != null)
            {
                state.name = type.Name;
                state.guid = System.Guid.NewGuid().ToString();

                // Convert screen position to graph coordinates
                var graphPosition = contentViewContainer.WorldToLocal(position);
                state.position = graphPosition;

                // Add to asset if StateMachineSO is an asset
                if (AssetDatabase.Contains(_stateMachine))
                {
                    AssetDatabase.AddObjectToAsset(state, _stateMachine);
                }

                _stateMachine.States.Add(state);
                CreateStateView(state);

                EditorUtility.SetDirty(_stateMachine);
                EditorUtility.SetDirty(state);

                if (AssetDatabase.Contains(_stateMachine))
                {
                    AssetDatabase.SaveAssets();
                }
            }
        }
        
        private Vector2 GetNodeWorldCenter(StateNodeView nodeView)
        {
            var nodeCenter = new Vector2(
                nodeView.layout.width * 0.5f, 
                nodeView.layout.height * 0.5f
            );
            return nodeView.LocalToWorld(nodeCenter);
        }
        
        public void SetBlackboardTable(BlackboardTable blackboardTable)
        {
            _blackboardView?.SetBlackboardTable(blackboardTable);
        }

        public new class UxmlFactory : UxmlFactory<StateMachineGraphContainer, UxmlTraits>
        {
        }

        private class ViewStateCallbackHandler : Manipulator
        {
            private readonly StateMachineGraphContainer _container;

            public ViewStateCallbackHandler(StateMachineGraphContainer container)
            {
                _container = container;
            }

            protected override void RegisterCallbacksOnTarget()
            {
                target.RegisterCallback<MouseUpEvent>(OnMouseUp);
                target.RegisterCallback<WheelEvent>(OnWheel);
            }

            protected override void UnregisterCallbacksFromTarget()
            {
                target.UnregisterCallback<MouseUpEvent>(OnMouseUp);
                target.UnregisterCallback<WheelEvent>(OnWheel);
            }

            private void OnMouseUp(MouseUpEvent evt)
            {
                _container.schedule.Execute(_container.SaveViewState).ExecuteLater(50);
            }

            private void OnWheel(WheelEvent evt)
            {
                _container.schedule.Execute(_container.SaveViewState).ExecuteLater(50);
            }
        }
    }
}