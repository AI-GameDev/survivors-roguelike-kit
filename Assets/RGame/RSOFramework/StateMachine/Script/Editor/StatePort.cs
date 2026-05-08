#region

using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

#endregion

namespace RGame.ScriptableCoreKit
{
    /// <summary>
    /// Simplified StatePort for hidden port system
    /// Maintains connection logic while being invisible to users
    /// </summary>
    public class StatePort : Port
    {
        public StatePort(Direction direction, Capacity capacity)
            : base(Orientation.Vertical, direction, capacity, typeof(bool))
        {
            var connectorListener = new StateMachineEdgeConnectorListener();

            m_EdgeConnector = new EdgeConnector<StateMachineEdge>(connectorListener);
            this.AddManipulator(m_EdgeConnector);

            SetupHiddenPortStyle();
        }

        /// <summary>
        /// Sets up the port to be completely hidden but functional
        /// </summary>
        private void SetupHiddenPortStyle()
        {
            // Make port completely invisible and non-interactive
            style.height = 1;
            style.width = 1;
            style.opacity = 0;
            style.position = Position.Absolute;
            style.overflow = Overflow.Hidden;
            
            // Remove all visual elements
            style.backgroundColor = Color.clear;
            style.borderTopWidth = 0;
            style.borderBottomWidth = 0;
            style.borderLeftWidth = 0;
            style.borderRightWidth = 0;
            
            // Set port color for internal logic (not visible)
            if (direction == Direction.Input)
                portColor = new Color(0.3f, 0.7f, 1f);
            else
                portColor = new Color(1f, 0.7f, 0.3f);

            // Hide all child elements when they're created
            schedule.Execute(HidePortVisuals);
        }

        /// <summary>
        /// Hides all visual elements of the port
        /// </summary>
        private void HidePortVisuals()
        {
            // Hide connector visual element
            var connector = this.Q("connector");
            if (connector != null)
            {
                connector.style.display = DisplayStyle.None;
            }

            // Hide cap visual element
            var cap = this.Q("cap");
            if (cap != null)
            {
                cap.style.display = DisplayStyle.None;
            }

            // Hide any labels
            this.Query<Label>().ForEach(label => label.style.display = DisplayStyle.None);
        }

        /// <summary>
        /// Override ContainsPoint to make port non-interactive
        /// </summary>
        public override bool ContainsPoint(Vector2 localPoint)
        {
            // Port should never be directly interactable
            return false;
        }

        /// <summary>
        /// Simplified edge connector listener for the hidden port system
        /// Works with the Animator-style connection approach
        /// </summary>
        private class StateMachineEdgeConnectorListener : IEdgeConnectorListener
        {
            private readonly List<Edge> m_EdgesToCreate;
            private readonly List<GraphElement> m_EdgesToDelete;
            private readonly GraphViewChange m_GraphViewChange;

            public StateMachineEdgeConnectorListener()
            {
                m_EdgesToCreate = new List<Edge>();
                m_EdgesToDelete = new List<GraphElement>();
                m_GraphViewChange.edgesToCreate = m_EdgesToCreate;
                m_GraphViewChange.elementsToRemove = m_EdgesToDelete;
            }

            public void OnDropOutsidePort(Edge edge, Vector2 position)
            {
                // Handle dropping edge outside of any port - no action needed for hidden ports
            }

            public void OnDrop(GraphView graphView, Edge edge)
            {
                m_EdgesToCreate.Clear();
                m_EdgesToDelete.Clear();

                if (!IsValidConnection(edge)) return;

                // Check if this exact connection already exists
                if (ConnectionAlreadyExists(edge))
                {
                    Debug.LogWarning("Connection already exists between these states");
                    return;
                }

                // Ensure we're using StateMachineEdge
                StateMachineEdge customEdge;
                if (edge is StateMachineEdge smEdge)
                {
                    customEdge = smEdge;
                }
                else
                {
                    customEdge = new StateMachineEdge();
                    customEdge.output = edge.output;
                    customEdge.input = edge.input;
                }

                m_EdgesToCreate.Add(customEdge);

                // Let the graph view handle the actual state transition creation
                if (graphView.graphViewChanged != null)
                {
                    var result = graphView.graphViewChanged(new GraphViewChange { edgesToCreate = m_EdgesToCreate });
                    if (result.edgesToCreate != null)
                    {
                        foreach (var newEdge in result.edgesToCreate)
                        {
                            if (!graphView.graphElements.Contains(newEdge))
                            {
                                graphView.AddElement(newEdge);
                                
                                if (newEdge.input != null && newEdge.output != null)
                                {
                                    newEdge.input.Connect(newEdge);
                                    newEdge.output.Connect(newEdge);
                                }
                            }
                        }
                    }
                }

                // Refresh all edge displays
                graphView.schedule.Execute(() =>
                {
                    foreach (var element in graphView.graphElements)
                        if (element is Edge refreshEdge)
                        {
                            refreshEdge.UpdateEdgeControl();
                            refreshEdge.MarkDirtyRepaint();
                        }
                }).ExecuteLater(1);
            }

            /// <summary>
            /// Check if a connection already exists between the same nodes
            /// </summary>
            private bool ConnectionAlreadyExists(Edge newEdge)
            {
                if (newEdge.output?.node is StateNodeView outputNode && 
                    newEdge.input?.node is StateNodeView inputNode)
                {
                    // Check if there's already a connection between these specific nodes
                    if (outputNode.outputPort?.connected == true)
                    {
                        foreach (var existingEdge in outputNode.outputPort.connections)
                        {
                            if (existingEdge.input?.node == inputNode)
                            {
                                return true; // Connection already exists
                            }
                        }
                    }
                }
                return false;
            }

            /// <summary>
            /// Validates connection logic for state machine
            /// Simplified validation - only prevents basic invalid connections
            /// </summary>
            private bool IsValidConnection(Edge edge)
            {
                if (edge?.input == null || edge?.output == null)
                    return false;

                // Cannot connect to self (same node)
                if (edge.input.node == edge.output.node)
                    return false;

                // Must be connecting input to output or vice versa (different directions)
                if (edge.input.direction == edge.output.direction)
                    return false;

                // All other connections are allowed - no cycle detection restriction
                return true;
            }
        }
    }
}