using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.Reflection;

namespace RGame.ScriptableCoreKit
{
    /// <summary>
    /// Enhanced edge with custom symmetric semicircle arc curves for state machine connections
    /// Unity 6 compatible with proper event handling
    /// Features symmetric arcing based on connection direction
    /// </summary>
    public class StateMachineEdge : Edge
    {
        private StateTransition _stateTransition;
        private ActionStateNode _fromState;

        public Action<StateMachineEdge> OnEdgeClicked;
        public StateTransition StateTransition => _stateTransition;
        public ActionStateNode FromState => _fromState;

        // Semicircle arc parameters
        private float _arcHeight = 80f; // Height of the semicircle arc
        private bool _adaptiveArcHeight = true;

        private VisualElement _customVisualElement;
        private bool _isSetupComplete = false;
        private bool _hasMouseCapture = false;
        private bool _isHovered = false;

        // Expanded hit detection tolerances for wider clicking area
        private float _baseHitTolerance = 15f;
        private float _hoverHitTolerance = 18f;
        private float _selectedHitTolerance = 22f;

        /// <summary>
        /// Get or set the default edge color
        /// </summary>
        public Color DefaultColor { get; set; } = new Color(0.8f, 0.2f, 0.2f);

        /// <summary>
        /// Get or set the hover edge color
        /// </summary>
        public Color HoverColor { get; set; } = new Color(0.2f, 0.6f, 1.0f);

        public StateMachineEdge()
        {
            // Critical for Unity 6: Set PickingMode on both edge and edgeControl
            this.pickingMode = PickingMode.Position;
            this.edgeControl.pickingMode = PickingMode.Position;

            // Enable selection capabilities
            capabilities |= Capabilities.Selectable | Capabilities.Deletable;

            // Setup custom rendering
            SetupCustomRendering();

            // Register for panel attachment to setup event handlers (Unity 6 requirement)
            RegisterCallback<AttachToPanelEvent>(OnAttachedToPanel);
        }

        /// <summary>
        /// Unity 6: Declare interest in specific events for performance optimization
        /// </summary>
        [EventInterest(typeof(MouseDownEvent), typeof(MouseUpEvent), typeof(MouseMoveEvent), typeof(MouseEnterEvent), typeof(MouseLeaveEvent))]
        protected override void HandleEventBubbleUp(EventBase evt)
        {
            // Handle events in the bubble up phase (Unity 6 pattern)
            switch (evt.eventTypeId)
            {
                case var id when id == MouseDownEvent.TypeId():
                    HandleMouseDown(evt as MouseDownEvent);
                    break;
                case var id when id == MouseUpEvent.TypeId():
                    HandleMouseUp(evt as MouseUpEvent);
                    break;
                case var id when id == MouseEnterEvent.TypeId():
                    HandleMouseEnter(evt as MouseEnterEvent);
                    break;
                case var id when id == MouseLeaveEvent.TypeId():
                    HandleMouseLeave(evt as MouseLeaveEvent);
                    break;
            }

            base.HandleEventBubbleUp(evt);
        }

        /// <summary>
        /// Setup event handlers after panel attachment (Unity 6 requirement)
        /// </summary>
        private void OnAttachedToPanel(AttachToPanelEvent evt)
        {
            // Register mouse callbacks with TrickleDown to intercept before other manipulators
            RegisterCallback<MouseDownEvent>(OnMouseDownCapture, TrickleDown.TrickleDown);
            RegisterCallback<MouseUpEvent>(OnMouseUpCapture, TrickleDown.TrickleDown);

            // Register hover callbacks
            RegisterCallback<MouseEnterEvent>(OnMouseEnterHandler);
            RegisterCallback<MouseLeaveEvent>(OnMouseLeaveHandler);
            RegisterCallback<MouseMoveEvent>(OnMouseMoveHandler);

            // Unregister the attachment callback
            UnregisterCallback<AttachToPanelEvent>(OnAttachedToPanel);

            // Force update after attachment
            schedule.Execute(() => UpdateEdgeControl()).ExecuteLater(10);
        }

        /// <summary>
        /// Mouse down handler in trickle down phase for capturing
        /// </summary>
        private void OnMouseDownCapture(MouseDownEvent evt)
        {
            if (evt.button == (int)MouseButton.LeftMouse)
            {
                // Check if click is on the edge path with expanded tolerance
                var localPoint = this.WorldToLocal(evt.mousePosition);
                if (IsPointOnEdgePath(localPoint, true))
                {
                    // Capture mouse to ensure we get the up event
                    _hasMouseCapture = true;
                    this.CaptureMouse();
                    evt.StopPropagation(); // Unity 6: Use StopPropagation instead of PreventDefault

                    Debug.Log("Edge clicked - mouse captured");
                }
            }
        }

        /// <summary>
        /// Mouse up handler in trickle down phase for click completion
        /// </summary>
        private void OnMouseUpCapture(MouseUpEvent evt)
        {
            if (evt.button == (int)MouseButton.LeftMouse && _hasMouseCapture && this.HasMouseCapture())
            {
                var localPoint = this.WorldToLocal(evt.mousePosition);
                if (IsPointOnEdgePath(localPoint, true))
                {
                    // Trigger the click event
                    Debug.Log("Edge click completed!");
                    OnEdgeClicked?.Invoke(this);
                }

                // Release mouse capture
                _hasMouseCapture = false;
                this.ReleaseMouse();
                evt.StopPropagation();
            }
        }

        /// <summary>
        /// Handle mouse down in bubble up phase (Unity 6 event handling)
        /// </summary>
        private void HandleMouseDown(MouseDownEvent evt)
        {
            if (evt.button == (int)MouseButton.LeftMouse)
            {
                var localPoint = this.WorldToLocal(evt.mousePosition);
                if (IsPointOnEdgePath(localPoint, true))
                {
                    // Select the edge
                    if (!selected)
                    {
                        var graphView = GetFirstAncestorOfType<GraphView>();
                        graphView?.AddToSelection(this);
                    }

                    // Force visual update
                    UpdateEdgeControl();
                    evt.StopPropagation();
                }
            }
        }

        /// <summary>
        /// Handle mouse up in bubble up phase (Unity 6 event handling)
        /// </summary>
        private void HandleMouseUp(MouseUpEvent evt)
        {
            if (evt.button == (int)MouseButton.LeftMouse)
            {
                evt.StopPropagation();
            }
        }

        /// <summary>
        /// Handle mouse enter for hover state
        /// </summary>
        private void HandleMouseEnter(MouseEnterEvent evt)
        {
            _isHovered = true;
            UpdateEdgeControl();
        }

        /// <summary>
        /// Handle mouse leave for hover state
        /// </summary>
        private void HandleMouseLeave(MouseLeaveEvent evt)
        {
            _isHovered = false;
            UpdateEdgeControl();
        }

        /// <summary>
        /// Mouse enter event handler
        /// </summary>
        private void OnMouseEnterHandler(MouseEnterEvent evt)
        {
            // Check if mouse is actually over the edge path
            var localPoint = this.WorldToLocal(evt.mousePosition);
            if (IsPointOnEdgePath(localPoint))
            {
                _isHovered = true;
                UpdateEdgeControl();
            }
        }

        /// <summary>
        /// Mouse leave event handler
        /// </summary>
        private void OnMouseLeaveHandler(MouseLeaveEvent evt)
        {
            _isHovered = false;
            UpdateEdgeControl();
        }

        /// <summary>
        /// Mouse move handler to update hover state based on edge path
        /// </summary>
        private void OnMouseMoveHandler(MouseMoveEvent evt)
        {
            var localPoint = this.WorldToLocal(evt.mousePosition);
            bool shouldHover = IsPointOnEdgePath(localPoint);

            if (shouldHover != _isHovered)
            {
                _isHovered = shouldHover;
                UpdateEdgeControl();
            }
        }

        /// <summary>
        /// Check if a point is on the edge path with configurable tolerance
        /// Enhanced with wider detection area for semicircle arc
        /// </summary>
        /// <param name="localPoint">Point to test in local coordinates</param>
        /// <param name="useExpandedTolerance">Whether to use expanded tolerance for clicking</param>
        /// <returns>True if point is within the edge detection area</returns>
        private bool IsPointOnEdgePath(Vector2 localPoint, bool useExpandedTolerance = false)
        {
            if (output?.node == null || input?.node == null)
                return false;

            var outputPos = GetPortPositionForHitTest(output,true);
            var inputPos = GetPortPositionForHitTest(input,false);

            if (outputPos == Vector2.zero || inputPos == Vector2.zero)
                return false;

            var (control1, control2) = CalculateSymmetricSemicircleControlPoints(outputPos, inputPos);

            // Use appropriate tolerance based on context
            float tolerance;
            if (useExpandedTolerance)
            {
                // Expanded tolerance for clicking - much larger detection area
                tolerance = selected ? _selectedHitTolerance + 5f : _baseHitTolerance + 8f;
            }
            else
            {
                // Normal tolerance for hover and selection
                tolerance = selected ? _selectedHitTolerance : (_isHovered ? _hoverHitTolerance : _baseHitTolerance);
            }

            return IsPointNearBezier(localPoint, outputPos, control1, control2, inputPos, tolerance);
        }

        /// <summary>
        /// Check if point is near bezier curve with enhanced sampling for semicircle
        /// </summary>
        private bool IsPointNearBezier(Vector2 point, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float tolerance)
        {
            // Enhanced sampling for better hit detection on curved paths
            const int samples = 40; // More samples for smooth semicircle detection

            for (int i = 0; i <= samples; i++)
            {
                float t = i / (float)samples;
                Vector2 curvePoint = CalculateBezierPoint(t, p0, p1, p2, p3);

                if (Vector2.Distance(point, curvePoint) <= tolerance)
                    return true;
            }

            // Additional check: test perpendicular distance to curve segments
            for (int i = 0; i < samples; i++)
            {
                float t1 = i / (float)samples;
                float t2 = (i + 1) / (float)samples;

                Vector2 p1_curve = CalculateBezierPoint(t1, p0, p1, p2, p3);
                Vector2 p2_curve = CalculateBezierPoint(t2, p0, p1, p2, p3);

                // Check distance to line segment
                float segmentDistance = DistanceToLineSegment(point, p1_curve, p2_curve);
                if (segmentDistance <= tolerance)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Calculate distance from point to line segment
        /// </summary>
        private float DistanceToLineSegment(Vector2 point, Vector2 lineStart, Vector2 lineEnd)
        {
            Vector2 line = lineEnd - lineStart;
            float lineLength = line.magnitude;

            if (lineLength < 0.001f)
                return Vector2.Distance(point, lineStart);

            Vector2 lineDirection = line / lineLength;
            Vector2 toPoint = point - lineStart;

            float projection = Vector2.Dot(toPoint, lineDirection);
            projection = Mathf.Clamp(projection, 0f, lineLength);

            Vector2 closestPoint = lineStart + lineDirection * projection;
            return Vector2.Distance(point, closestPoint);
        }

        /// <summary>
        /// Calculate point on bezier curve at parameter t
        /// </summary>
        private Vector2 CalculateBezierPoint(float t, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
        {
            float u = 1 - t;
            float tt = t * t;
            float uu = u * u;
            float uuu = uu * u;
            float ttt = tt * t;

            Vector2 p = uuu * p0; // (1-t)^3 * p0
            p += 3 * uu * t * p1; // 3(1-t)^2 * t * p1
            p += 3 * u * tt * p2; // 3(1-t) * t^2 * p2
            p += ttt * p3; // t^3 * p3

            return p;
        }

        public void SetStateTransition(StateTransition stateTransition, ActionStateNode fromState)
        {
            _stateTransition = stateTransition;
            _fromState = fromState;
        }

        /// <summary>
        /// Override OnSelected to update visual when selected
        /// </summary>
        public override void OnSelected()
        {
            base.OnSelected();
            UpdateEdgeControl();
        }

        /// <summary>
        /// Override OnUnselected to update visual when deselected
        /// </summary>
        public override void OnUnselected()
        {
            base.OnUnselected();
            UpdateEdgeControl();
        }

        /// <summary>
        /// Setup custom rendering without completely replacing the edge
        /// </summary>
        private void SetupCustomRendering()
        {
            // Create custom visual element for our rendering
            _customVisualElement = new VisualElement();
            _customVisualElement.name = "custom-edge-renderer";
            _customVisualElement.pickingMode = PickingMode.Ignore; // Let parent handle picking
            _customVisualElement.generateVisualContent += OnGenerateVisualContent;

            // Style to cover the edge area with expanded bounds
            _customVisualElement.style.position = Position.Absolute;
            _customVisualElement.style.left = -_selectedHitTolerance;
            _customVisualElement.style.top = -_selectedHitTolerance;
            _customVisualElement.style.right = -_selectedHitTolerance;
            _customVisualElement.style.bottom = -_selectedHitTolerance;

            // Add as child but don't interfere with edge's own elements
            Add(_customVisualElement);

            // Hide default edge rendering
            HideDefaultEdgeRendering();

            // Update on geometry changes
            RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);

            _isSetupComplete = true;
        }

        /// <summary>
        /// Hide default edge rendering while keeping functionality
        /// </summary>
        private void HideDefaultEdgeRendering()
        {
            // Hide the visual parts of edgeControl but keep it functional
            edgeControl.style.opacity = 0;

            // Alternative: Override the edge control's visual generation
            edgeControl.generateVisualContent = (ctx) => { }; // Empty visual content
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            if (_isSetupComplete)
            {
                UpdateEdgeControl();
            }
        }

        /// <summary>
        /// Force update of the edge control
        /// </summary>
        public override bool UpdateEdgeControl()
        {
            if (_customVisualElement != null)
            {
                _customVisualElement.MarkDirtyRepaint();
            }

            // Call base to maintain edge functionality
            return base.UpdateEdgeControl();
        }

        /// <summary>
        /// Override ContainsPoint for proper hit detection with expanded area
        /// </summary>
        public override bool ContainsPoint(Vector2 localPoint)
        {
            return IsPointOnEdgePath(localPoint, true);
        }

        /// <summary>
        /// Custom visual content generation for the edge using symmetric semicircle arc style
        /// </summary>
        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            if (output?.node == null || input?.node == null)
                return;

            var outputPos = GetPortPositionForDisplay(output);
            var inputPos = GetPortPositionForDisplay(input);

            if (outputPos == Vector2.zero || inputPos == Vector2.zero)
                return;

            // Use symmetric semicircle arc control points
            var (controlPoint1, controlPoint2) = CalculateSymmetricSemicircleControlPoints(outputPos, inputPos);

            // Draw the custom curve
            DrawCustomBezier(ctx, outputPos, inputPos, controlPoint1, controlPoint2);
        }

        private Vector2 GetPortPositionForDisplay(Port port)
        {
            if (port?.node == null) return Vector2.zero;

            try
            {
                var portWorldPos = GetPortWorldPosition(port);
                return this.WorldToLocal(portWorldPos);
            }
            catch
            {
                return Vector2.zero;
            }
        }

        private Vector2 GetPortPositionForHitTest(Port port, bool isOut)
        {
            if (port?.node == null) return Vector2.zero;

            try
            {
                var portWorldPos = GetPortWorldPosition(port);
                var localPos = this.WorldToLocal(portWorldPos);

                if (isOut)
                {
                    localPos += new Vector2(-5, -20);
                }
                else
                {
                    localPos += new Vector2(-20, 0);
                }

                return localPos;
            }
            catch
            {
                return Vector2.zero;
            }
        }

        /// <summary>
        /// Get world position of a port center
        /// </summary>
        private Vector2 GetPortWorldPosition(Port port)
        {
            var portCenter = new Vector2(port.layout.width * 0.5f, port.layout.height * 0.5f);
            return port.LocalToWorld(portCenter);
        }

        /// <summary>
        /// Calculate control points for symmetric semicircle arc curves
        /// Creates symmetric arcs: left connections curve left, right connections curve right
        /// This creates a more balanced and visually pleasing connection layout
        /// </summary>
        private (Vector2, Vector2) CalculateSymmetricSemicircleControlPoints(Vector2 start, Vector2 end)
        {
            var direction = end - start;
            var distance = direction.magnitude;
            var midPoint = (start + end) * 0.5f;

            // Calculate perpendicular direction for arc height
            Vector2 perpendicular = new Vector2(-direction.y, direction.x).normalized;

            // Adaptive arc height based on distance
            float arcHeight = _arcHeight;
            if (_adaptiveArcHeight)
            {
                // Scale arc height based on distance for better visual proportions
                arcHeight = Mathf.Lerp(40f, 120f, Mathf.Clamp01(distance / 300f));
            }

            // Determine symmetric arc direction based on connection layout
            bool shouldArcLeft = DetermineSymmetricArcDirection(start, end);
            float arcMultiplier = shouldArcLeft ? -1f : 1f;

            // Create symmetric arc by using perpendicular with calculated multiplier
            Vector2 arcCenter = midPoint + perpendicular * arcHeight * arcMultiplier;

            // Calculate control points for smooth semicircle that creates the desired symmetry
            float controlDistance = distance * 0.552f; // Magic number for circular approximation
            Vector2 tangent = direction.normalized;

            Vector2 control1 = start + tangent * (controlDistance * 0.5f) + perpendicular * (arcHeight * 0.8f * arcMultiplier);
            Vector2 control2 = end - tangent * (controlDistance * 0.5f) + perpendicular * (arcHeight * 0.8f * arcMultiplier);

            return (control1, control2);
        }

        /// <summary>
        /// Determine symmetric arc direction for balanced visual layout
        /// </summary>
        /// <param name="start">Start position of the connection</param>
        /// <param name="end">End position of the connection</param>
        /// <returns>True if arc should go left (negative perpendicular), false for right arc</returns>
        private bool DetermineSymmetricArcDirection(Vector2 start, Vector2 end)
        {
            Vector2 outputNodeCenter = GetNodeCenter(output?.node);
            Vector2 inputNodeCenter = GetNodeCenter(input?.node);

            if (outputNodeCenter == Vector2.zero || inputNodeCenter == Vector2.zero)
            {
                var direction = end - start;
                return direction.x > 0;
            }

            Vector2 nodeDirection = inputNodeCenter - outputNodeCenter;

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

        private Vector2 GetNodeCenter(Node node)
        {
            if (node == null) return Vector2.zero;

            try
            {
                var nodeCenter = new Vector2(node.layout.width * 0.5f, node.layout.height * 0.5f);
                var worldCenter = node.LocalToWorld(nodeCenter);
                return this.WorldToLocal(worldCenter);
            }
            catch
            {
                return Vector2.zero;
            }
        }

        /// <summary>
        /// Draw custom bezier curve with symmetric semicircle arc styling
        /// </summary>
        private void DrawCustomBezier(MeshGenerationContext ctx, Vector2 start, Vector2 end, Vector2 control1, Vector2 control2)
        {
            var painter = ctx.painter2D;

            // Determine edge color based on state
            Color edgeColor = DefaultColor;
            float lineWidth = 3f;

            if (selected)
            {
                lineWidth = 4f;
            }
            else if (_isHovered)
            {
                edgeColor = HoverColor;
                lineWidth = 4f;
            }

            // Line properties for smooth arc
            painter.strokeColor = edgeColor;
            painter.lineWidth = lineWidth;
            painter.lineCap = LineCap.Round;
            painter.lineJoin = LineJoin.Round;

            // Draw the symmetric semicircle bezier curve
            painter.BeginPath();
            painter.MoveTo(start);
            painter.BezierCurveTo(control1, control2, end);
            painter.Stroke();

            // Draw arrow at the end
            DrawArrow(painter, end, (end - control2).normalized);

            // Draw connection points
            DrawConnectionPoint(painter, start, 4f);
            DrawConnectionPoint(painter, end, 4f);
        }

        /// <summary>
        /// Draw arrow head at the connection end
        /// </summary>
        private void DrawArrow(Painter2D painter, Vector2 tip, Vector2 direction)
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
    }
}