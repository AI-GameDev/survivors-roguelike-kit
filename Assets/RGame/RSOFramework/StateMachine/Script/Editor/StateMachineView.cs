#region

using System;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

#endregion

namespace RGame.ScriptableCoreKit
{
    public class StateMachineView : VisualElement
    {
        private const float PANEL_PADDING = 8f;

        private StateMachineGraphContainer _graphContainer;
        private VisualElement _mainPanel;
        private VisualElement _stateMachineContainer;
        private Label _stateMachineHeader;

        public Action<StateNodeView> OnStateSelected;

        public StateMachineView()
        {
            style.flexGrow = 1;

            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Roofen/ScriptableCoreKitPro/StateMachine/Scripts/Editor/StateMachineWindow.uss");
            if (styleSheet != null) styleSheets.Add(styleSheet);

            SetupMainPanel();
        }

        private void SetupMainPanel()
        {
            _mainPanel = new VisualElement();
            _mainPanel.style.flexGrow = 1;
            _mainPanel.style.backgroundColor = EditorGUIUtility.isProSkin ? new Color(0.22f, 0.22f, 0.22f, 0.95f) : new Color(0.76f, 0.76f, 0.76f, 0.95f);
            _mainPanel.style.paddingTop = PANEL_PADDING;
            _mainPanel.style.paddingBottom = PANEL_PADDING;
            _mainPanel.style.paddingLeft = PANEL_PADDING;
            _mainPanel.style.paddingRight = PANEL_PADDING;

            _stateMachineHeader = new Label("State Machine Graph");
            _stateMachineHeader.style.fontSize = 13;
            _stateMachineHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            _stateMachineHeader.style.color = EditorGUIUtility.isProSkin ? new Color(0.85f, 0.85f, 0.85f) : new Color(0.15f, 0.15f, 0.15f);
            _stateMachineHeader.style.paddingTop = 6;
            _stateMachineHeader.style.paddingBottom = 6;
            _stateMachineHeader.style.paddingLeft = 8;
            _stateMachineHeader.style.paddingRight = 8;
            _stateMachineHeader.style.marginBottom = 8;

            _stateMachineContainer = new VisualElement();
            _stateMachineContainer.style.flexGrow = 1;
            _stateMachineContainer.style.backgroundColor = EditorGUIUtility.isProSkin ? new Color(0.25f, 0.25f, 0.25f, 0.8f) : new Color(0.9f, 0.9f, 0.9f, 0.8f);
            _stateMachineContainer.style.borderTopWidth = 1;
            _stateMachineContainer.style.borderBottomWidth = 1;
            _stateMachineContainer.style.borderLeftWidth = 1;
            _stateMachineContainer.style.borderRightWidth = 1;
            _stateMachineContainer.style.borderTopColor = EditorGUIUtility.isProSkin ? new Color(0.4f, 0.4f, 0.4f) : new Color(0.7f, 0.7f, 0.7f);
            _stateMachineContainer.style.borderBottomColor = EditorGUIUtility.isProSkin ? new Color(0.4f, 0.4f, 0.4f) : new Color(0.7f, 0.7f, 0.7f);
            _stateMachineContainer.style.borderLeftColor = EditorGUIUtility.isProSkin ? new Color(0.4f, 0.4f, 0.4f) : new Color(0.7f, 0.7f, 0.7f);
            _stateMachineContainer.style.borderRightColor = EditorGUIUtility.isProSkin ? new Color(0.4f, 0.4f, 0.4f) : new Color(0.7f, 0.7f, 0.7f);
            _stateMachineContainer.style.borderTopLeftRadius = 4;
            _stateMachineContainer.style.borderTopRightRadius = 4;
            _stateMachineContainer.style.borderBottomLeftRadius = 4;
            _stateMachineContainer.style.borderBottomRightRadius = 4;

            CreateStateMachineGraphContainer();

            _mainPanel.Add(_stateMachineHeader);
            _mainPanel.Add(_stateMachineContainer);

            Add(_mainPanel);
        }

        private void CreateStateMachineGraphContainer()
        {
            _graphContainer = new StateMachineGraphContainer();
            _graphContainer.style.flexGrow = 1;
            _graphContainer.style.position = Position.Relative;
            
            // Add grid background to the graph container
            var gridBackground = new GridBackground();
            _graphContainer.Insert(0, gridBackground);
            
            // Set up state selection callback
            _graphContainer.OnStateSelected = (stateView) => OnStateSelected?.Invoke(stateView);

            _stateMachineContainer.Add(_graphContainer);
        }

        /// <summary>
        /// Loads the state machine from the current selection
        /// </summary>
        public void LoadStateMachine()
        {
            _graphContainer?.LoadStateMachine();
        }

        public new class UxmlFactory : UxmlFactory<StateMachineView, UxmlTraits>
        {
        }
    }
}