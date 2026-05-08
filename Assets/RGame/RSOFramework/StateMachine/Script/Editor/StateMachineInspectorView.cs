#region

using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

#endregion

namespace RGame.ScriptableCoreKit
{
   /// <summary>
    /// Enhanced inspector view with transition editing capabilities
    /// </summary>
    public class StateMachineInspectorView : VisualElement
    {
        private VisualElement _actionsContainer;
        private VisualElement _contentContainer;
        private Editor _editor;
        private IMGUIContainer _editorContainer;
        private readonly Color _errorColor = new(1f, 0.3f, 0.3f, 1f);
        private Button _focusStateButton;
        private Label _guidLabel;
        private VisualElement _headerContainer;
        private readonly Color _primaryColor = new(0.2f, 0.6f, 1f, 1f);
        private VisualElement _propertiesContainer;
        private Button _selectAssetButton;
        private StateNodeView _selectedStateView;
        private VisualElement _statusContainer;
        private VisualElement _statusIndicator;
        private Label _statusLabel;
        private readonly Color _successColor = new(0.3f, 0.8f, 0.3f, 1f);
        private Label _titleLabel;
        private Label _typeLabel;
        private Color _warningColor = new(1f, 0.7f, 0.2f, 1f);
        
        // Transition editing
        private TransitionInspectorView _transitionInspector;
        private VisualElement _transitionContainer;
        private Button _toggleTransitionButton;
        private bool _showingTransitions = false;

        public StateMachineInspectorView()
        {
            SetupUI();
            ShowEmptyState();
        }

        private void SetupUI()
        {
            style.flexGrow = 1;
            style.backgroundColor = EditorGUIUtility.isProSkin ? new Color(0.22f, 0.22f, 0.22f, 0.95f) : new Color(0.76f, 0.76f, 0.76f, 0.95f);
            style.paddingTop = 8;
            style.paddingBottom = 8;
            style.paddingLeft = 8;
            style.paddingRight = 8;

            CreateHeader();
            CreateStatusSection();
            CreatePropertiesSection();
            CreateTransitionSection();
            CreateActionsSection();
        }

        private void CreateHeader()
        {
            _headerContainer = new VisualElement();
            _headerContainer.style.backgroundColor = EditorGUIUtility.isProSkin ? new Color(0.25f, 0.25f, 0.25f, 0.8f) : new Color(0.9f, 0.9f, 0.9f, 0.8f);
            _headerContainer.style.borderTopWidth = 1;
            _headerContainer.style.borderBottomWidth = 1;
            _headerContainer.style.borderLeftWidth = 1;
            _headerContainer.style.borderRightWidth = 1;
            _headerContainer.style.borderTopColor = _primaryColor;
            _headerContainer.style.borderBottomColor = _primaryColor;
            _headerContainer.style.borderLeftColor = _primaryColor;
            _headerContainer.style.borderRightColor = _primaryColor;
            _headerContainer.style.borderTopLeftRadius = 6;
            _headerContainer.style.borderTopRightRadius = 6;
            _headerContainer.style.borderBottomLeftRadius = 6;
            _headerContainer.style.borderBottomRightRadius = 6;
            _headerContainer.style.paddingTop = 12;
            _headerContainer.style.paddingBottom = 12;
            _headerContainer.style.paddingLeft = 16;
            _headerContainer.style.paddingRight = 16;
            _headerContainer.style.marginBottom = 8;

            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;

            _statusIndicator = new VisualElement();
            _statusIndicator.style.width = 12;
            _statusIndicator.style.height = 12;
            _statusIndicator.style.borderTopLeftRadius = 6;
            _statusIndicator.style.borderTopRightRadius = 6;
            _statusIndicator.style.borderBottomLeftRadius = 6;
            _statusIndicator.style.borderBottomRightRadius = 6;
            _statusIndicator.style.backgroundColor = new Color(0.5f, 0.5f, 0.5f, 0.8f);
            _statusIndicator.style.marginRight = 8;

            var titleContainer = new VisualElement();
            titleContainer.style.flexGrow = 1;

            _titleLabel = new Label("State Inspector");
            _titleLabel.style.fontSize = 16;
            _titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _titleLabel.style.color = _primaryColor;

            _typeLabel = new Label("");
            _typeLabel.style.fontSize = 10;
            _typeLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            _typeLabel.style.color = EditorGUIUtility.isProSkin ? new Color(0.7f, 0.7f, 0.7f) : new Color(0.4f, 0.4f, 0.4f);
            _typeLabel.style.letterSpacing = 0.5f;
            _typeLabel.style.marginTop = 2;

            titleContainer.Add(_titleLabel);
            titleContainer.Add(_typeLabel);

            headerRow.Add(_statusIndicator);
            headerRow.Add(titleContainer);

            _headerContainer.Add(headerRow);
            Add(_headerContainer);
        }

        private void CreateStatusSection()
        {
            _statusContainer = new VisualElement();
            _statusContainer.style.backgroundColor = EditorGUIUtility.isProSkin ? new Color(0.2f, 0.2f, 0.2f, 0.6f) : new Color(0.85f, 0.85f, 0.85f, 0.6f);
            _statusContainer.style.borderTopLeftRadius = 4;
            _statusContainer.style.borderTopRightRadius = 4;
            _statusContainer.style.borderBottomLeftRadius = 4;
            _statusContainer.style.borderBottomRightRadius = 4;
            _statusContainer.style.paddingTop = 8;
            _statusContainer.style.paddingBottom = 8;
            _statusContainer.style.paddingLeft = 12;
            _statusContainer.style.paddingRight = 12;
            _statusContainer.style.marginBottom = 8;

            var statusTitle = new Label("State Status");
            statusTitle.style.fontSize = 12;
            statusTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            statusTitle.style.color = EditorGUIUtility.isProSkin ? new Color(0.9f, 0.9f, 0.9f) : new Color(0.1f, 0.1f, 0.1f);
            statusTitle.style.marginBottom = 6;

            _statusLabel = new Label("Status: Idle");
            _statusLabel.style.fontSize = 11;
            _statusLabel.style.color = EditorGUIUtility.isProSkin ? new Color(0.8f, 0.8f, 0.8f) : new Color(0.2f, 0.2f, 0.2f);
            _statusLabel.style.marginBottom = 3;

            _guidLabel = new Label("");
            _guidLabel.style.fontSize = 9;
            _guidLabel.style.color = EditorGUIUtility.isProSkin ? new Color(0.6f, 0.6f, 0.6f) : new Color(0.5f, 0.5f, 0.5f);
            _guidLabel.style.unityFontStyleAndWeight = FontStyle.Italic;

            _statusContainer.Add(statusTitle);
            _statusContainer.Add(_statusLabel);
            _statusContainer.Add(_guidLabel);

            Add(_statusContainer);
        }

        private void CreatePropertiesSection()
        {
            _propertiesContainer = new VisualElement();
            _propertiesContainer.style.backgroundColor = EditorGUIUtility.isProSkin ? new Color(0.18f, 0.18f, 0.18f, 0.8f) : new Color(0.92f, 0.92f, 0.92f, 0.8f);
            _propertiesContainer.style.borderTopWidth = 1;
            _propertiesContainer.style.borderBottomWidth = 1;
            _propertiesContainer.style.borderLeftWidth = 1;
            _propertiesContainer.style.borderRightWidth = 1;
            _propertiesContainer.style.borderTopColor = EditorGUIUtility.isProSkin ? new Color(0.4f, 0.4f, 0.4f) : new Color(0.7f, 0.7f, 0.7f);
            _propertiesContainer.style.borderBottomColor = EditorGUIUtility.isProSkin ? new Color(0.4f, 0.4f, 0.4f) : new Color(0.7f, 0.7f, 0.7f);
            _propertiesContainer.style.borderLeftColor = EditorGUIUtility.isProSkin ? new Color(0.4f, 0.4f, 0.4f) : new Color(0.7f, 0.7f, 0.7f);
            _propertiesContainer.style.borderRightColor = EditorGUIUtility.isProSkin ? new Color(0.4f, 0.4f, 0.4f) : new Color(0.7f, 0.7f, 0.7f);
            _propertiesContainer.style.borderTopLeftRadius = 4;
            _propertiesContainer.style.borderTopRightRadius = 4;
            _propertiesContainer.style.borderBottomLeftRadius = 4;
            _propertiesContainer.style.borderBottomRightRadius = 4;
            _propertiesContainer.style.paddingTop = 12;
            _propertiesContainer.style.paddingBottom = 12;
            _propertiesContainer.style.paddingLeft = 8;
            _propertiesContainer.style.paddingRight = 8;
            _propertiesContainer.style.marginBottom = 8;

            var propertiesTitle = new Label("State Properties");
            propertiesTitle.style.fontSize = 12;
            propertiesTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            propertiesTitle.style.color = EditorGUIUtility.isProSkin ? new Color(0.9f, 0.9f, 0.9f) : new Color(0.1f, 0.1f, 0.1f);
            propertiesTitle.style.marginBottom = 8;
            propertiesTitle.style.paddingLeft = 4;

            _propertiesContainer.Add(propertiesTitle);
            Add(_propertiesContainer);
        }

        private void CreateTransitionSection()
        {
            _transitionContainer = new VisualElement();
            _transitionContainer.style.backgroundColor = EditorGUIUtility.isProSkin ? new Color(0.18f, 0.18f, 0.18f, 0.8f) : new Color(0.92f, 0.92f, 0.92f, 0.8f);
            _transitionContainer.style.borderTopWidth = 1;
            _transitionContainer.style.borderBottomWidth = 1;
            _transitionContainer.style.borderLeftWidth = 1;
            _transitionContainer.style.borderRightWidth = 1;
            _transitionContainer.style.borderTopColor = EditorGUIUtility.isProSkin ? new Color(0.4f, 0.4f, 0.4f) : new Color(0.7f, 0.7f, 0.7f);
            _transitionContainer.style.borderBottomColor = EditorGUIUtility.isProSkin ? new Color(0.4f, 0.4f, 0.4f) : new Color(0.7f, 0.7f, 0.7f);
            _transitionContainer.style.borderLeftColor = EditorGUIUtility.isProSkin ? new Color(0.4f, 0.4f, 0.4f) : new Color(0.7f, 0.7f, 0.7f);
            _transitionContainer.style.borderRightColor = EditorGUIUtility.isProSkin ? new Color(0.4f, 0.4f, 0.4f) : new Color(0.7f, 0.7f, 0.7f);
            _transitionContainer.style.borderTopLeftRadius = 4;
            _transitionContainer.style.borderTopRightRadius = 4;
            _transitionContainer.style.borderBottomLeftRadius = 4;
            _transitionContainer.style.borderBottomRightRadius = 4;
            _transitionContainer.style.paddingTop = 8;
            _transitionContainer.style.paddingBottom = 8;
            _transitionContainer.style.paddingLeft = 8;
            _transitionContainer.style.paddingRight = 8;
            _transitionContainer.style.marginBottom = 8;
            _transitionContainer.style.display = DisplayStyle.None;

            var transitionHeader = new VisualElement();
            transitionHeader.style.flexDirection = FlexDirection.Row;
            transitionHeader.style.alignItems = Align.Center;
            transitionHeader.style.marginBottom = 8;
            transitionHeader.style.paddingLeft = 4;
            transitionHeader.style.paddingRight = 4;

            var transitionTitle = new Label("Transition Editor");
            transitionTitle.style.fontSize = 12;
            transitionTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            transitionTitle.style.color = EditorGUIUtility.isProSkin ? new Color(0.9f, 0.9f, 0.9f) : new Color(0.1f, 0.1f, 0.1f);
            transitionTitle.style.flexGrow = 1;

            _toggleTransitionButton = new Button(ToggleTransitionView);
            _toggleTransitionButton.text = "▼";
            _toggleTransitionButton.style.width = 20;
            _toggleTransitionButton.style.height = 20;
            _toggleTransitionButton.style.fontSize = 12;
            _toggleTransitionButton.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 0.8f);
            _toggleTransitionButton.style.borderTopWidth = 0;
            _toggleTransitionButton.style.borderBottomWidth = 0;
            _toggleTransitionButton.style.borderLeftWidth = 0;
            _toggleTransitionButton.style.borderRightWidth = 0;
            _toggleTransitionButton.style.borderTopLeftRadius = 10;
            _toggleTransitionButton.style.borderTopRightRadius = 10;
            _toggleTransitionButton.style.borderBottomLeftRadius = 10;
            _toggleTransitionButton.style.borderBottomRightRadius = 10;
            _toggleTransitionButton.style.color = Color.white;

            transitionHeader.Add(transitionTitle);
            transitionHeader.Add(_toggleTransitionButton);

            _transitionInspector = new TransitionInspectorView();
            _transitionInspector.style.display = DisplayStyle.None;

            _transitionContainer.Add(transitionHeader);
            _transitionContainer.Add(_transitionInspector);

            Add(_transitionContainer);
        }

        private void CreateActionsSection()
        {
            _actionsContainer = new VisualElement();
            _actionsContainer.style.backgroundColor = EditorGUIUtility.isProSkin ? new Color(0.2f, 0.2f, 0.2f, 0.6f) : new Color(0.85f, 0.85f, 0.85f, 0.6f);
            _actionsContainer.style.borderTopLeftRadius = 4;
            _actionsContainer.style.borderTopRightRadius = 4;
            _actionsContainer.style.borderBottomLeftRadius = 4;
            _actionsContainer.style.borderBottomRightRadius = 4;
            _actionsContainer.style.paddingTop = 8;
            _actionsContainer.style.paddingBottom = 8;
            _actionsContainer.style.paddingLeft = 12;
            _actionsContainer.style.paddingRight = 12;

            var actionsTitle = new Label("Actions");
            actionsTitle.style.fontSize = 12;
            actionsTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            actionsTitle.style.color = EditorGUIUtility.isProSkin ? new Color(0.9f, 0.9f, 0.9f) : new Color(0.1f, 0.1f, 0.1f);
            actionsTitle.style.marginBottom = 8;

            var buttonsContainer = new VisualElement();
            buttonsContainer.style.flexDirection = FlexDirection.Row;
            buttonsContainer.style.justifyContent = Justify.SpaceBetween;

            _selectAssetButton = new Button(() => SelectStateAsset());
            _selectAssetButton.text = "Select Asset";
            _selectAssetButton.style.height = 24;
            _selectAssetButton.style.fontSize = 10;
            _selectAssetButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            _selectAssetButton.style.backgroundColor = _primaryColor;
            _selectAssetButton.style.color = Color.white;
            _selectAssetButton.style.borderTopWidth = 0;
            _selectAssetButton.style.borderBottomWidth = 0;
            _selectAssetButton.style.borderLeftWidth = 0;
            _selectAssetButton.style.borderRightWidth = 0;
            _selectAssetButton.style.borderTopLeftRadius = 4;
            _selectAssetButton.style.borderTopRightRadius = 4;
            _selectAssetButton.style.borderBottomLeftRadius = 4;
            _selectAssetButton.style.borderBottomRightRadius = 4;
            _selectAssetButton.style.flexGrow = 1;
            _selectAssetButton.style.marginRight = 4;

            _focusStateButton = new Button(() => FocusOnState());
            _focusStateButton.text = "Focus State";
            _focusStateButton.style.height = 24;
            _focusStateButton.style.fontSize = 10;
            _focusStateButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            _focusStateButton.style.backgroundColor = new Color(0.6f, 0.3f, 0.8f);
            _focusStateButton.style.color = Color.white;
            _focusStateButton.style.borderTopWidth = 0;
            _focusStateButton.style.borderBottomWidth = 0;
            _focusStateButton.style.borderLeftWidth = 0;
            _focusStateButton.style.borderRightWidth = 0;
            _focusStateButton.style.borderTopLeftRadius = 4;
            _focusStateButton.style.borderTopRightRadius = 4;
            _focusStateButton.style.borderBottomLeftRadius = 4;
            _focusStateButton.style.borderBottomRightRadius = 4;
            _focusStateButton.style.flexGrow = 1;
            _focusStateButton.style.marginLeft = 4;

            buttonsContainer.Add(_selectAssetButton);
            buttonsContainer.Add(_focusStateButton);

            _actionsContainer.Add(actionsTitle);
            _actionsContainer.Add(buttonsContainer);

            Add(_actionsContainer);
        }

        private void ToggleTransitionView()
        {
            _showingTransitions = !_showingTransitions;
            _toggleTransitionButton.text = _showingTransitions ? "▲" : "▼";
            _transitionInspector.style.display = _showingTransitions ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void ShowEmptyState()
        {
            _titleLabel.text = "State Inspector";
            _typeLabel.text = "NO STATE SELECTED";
            _statusLabel.text = "Status: Select a state to view properties";
            _guidLabel.text = "";
            _statusIndicator.style.backgroundColor = new Color(0.5f, 0.5f, 0.5f, 0.8f);

            _selectAssetButton.style.display = DisplayStyle.None;
            _focusStateButton.style.display = DisplayStyle.None;
            _transitionContainer.style.display = DisplayStyle.None;

            ClearPropertiesContainer();

            var emptyMessage = new Label("Select a state to edit its properties");
            emptyMessage.style.fontSize = 11;
            emptyMessage.style.color = EditorGUIUtility.isProSkin ? new Color(0.6f, 0.6f, 0.6f) : new Color(0.5f, 0.5f, 0.5f);
            emptyMessage.style.unityFontStyleAndWeight = FontStyle.Italic;
            emptyMessage.style.unityTextAlign = TextAnchor.MiddleCenter;
            emptyMessage.style.paddingTop = 20;
            emptyMessage.style.paddingBottom = 20;

            _propertiesContainer.Add(emptyMessage);
        }

        public void UpdateSelection(StateNodeView stateView)
        {
            _selectedStateView = stateView;

            if (stateView == null)
            {
                ShowEmptyState();
                return;
            }

            UpdateHeader(stateView);
            UpdateStatus(stateView);
            UpdateProperties(stateView);

            _selectAssetButton.style.display = DisplayStyle.Flex;
            _focusStateButton.style.display = DisplayStyle.Flex;
            _transitionContainer.style.display = DisplayStyle.None; // Hide by default
        }

        public void ShowTransitionEditor(StateMachineEdge edge)
        {
            if (edge?.StateTransition == null || edge.FromState == null)
            {
                _transitionContainer.style.display = DisplayStyle.None;
                return;
            }

            _transitionContainer.style.display = DisplayStyle.Flex;
            _showingTransitions = true;
            _toggleTransitionButton.text = "▲";
            _transitionInspector.style.display = DisplayStyle.Flex;
            _transitionInspector.SetTransition(edge);
        }

        // ... (rest of the methods remain the same as in the original code)
        
        private void UpdateHeader(StateNodeView stateView)
        {
            var state = stateView.state;
            _titleLabel.text = GetFormattedStateName(state);
            _typeLabel.text = GetStateTypeCategory(state);

            if (Application.isPlaying)
            {
                var stateMachine = state.StateMachine;
                if (stateMachine != null && stateMachine.MyStateMachineSO.CurrentState == state)
                    _statusIndicator.style.backgroundColor = _successColor;
                else
                    _statusIndicator.style.backgroundColor = new Color(0.5f, 0.5f, 0.5f, 0.8f);
            }
            else
            {
                _statusIndicator.style.backgroundColor = _primaryColor;
            }
        }

        private void UpdateStatus(StateNodeView stateView)
        {
            var state = stateView.state;

            if (Application.isPlaying)
            {
                var stateMachine = state.StateMachine;
                if (stateMachine != null && stateMachine.MyStateMachineSO.CurrentState == state)
                {
                    _statusLabel.text = $"Status: Active (Time: {state.TimeInState:F2}s)";
                    _statusLabel.style.color = _successColor;
                }
                else
                {
                    _statusLabel.text = "Status: Inactive";
                    _statusLabel.style.color = EditorGUIUtility.isProSkin ? new Color(0.8f, 0.8f, 0.8f) : new Color(0.2f, 0.2f, 0.2f);
                }
            }
            else
            {
                _statusLabel.text = "Status: Editor Mode";
                _statusLabel.style.color = EditorGUIUtility.isProSkin ? new Color(0.8f, 0.8f, 0.8f) : new Color(0.2f, 0.2f, 0.2f);
            }

            // Show GUID for debugging
            _guidLabel.text = $"GUID: {state.guid}";
            
            // Special handling for EntryStateNode
            if (state is EntryStateNode)
            {
                if (!Application.isPlaying)
                {
                    _statusLabel.text = "Status: Entry Point (Will auto-transition when playing)";
                    _statusLabel.style.color = new Color(1f, 0.7f, 0.2f);
                }
            }
        }

        private void ClearPropertiesContainer()
        {
            if (_editorContainer != null)
            {
                _propertiesContainer.Remove(_editorContainer);
                _editorContainer = null;
            }

            _propertiesContainer.Clear();

            var propertiesTitle = new Label("State Properties");
            propertiesTitle.style.fontSize = 12;
            propertiesTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            propertiesTitle.style.color = EditorGUIUtility.isProSkin ? new Color(0.9f, 0.9f, 0.9f) : new Color(0.1f, 0.1f, 0.1f);
            propertiesTitle.style.marginBottom = 8;
            propertiesTitle.style.paddingLeft = 4;

            _propertiesContainer.Add(propertiesTitle);
        }

        private void UpdateProperties(StateNodeView stateView)
        {
            ClearPropertiesContainer();

            if (_editor != null)
            {
                Object.DestroyImmediate(_editor);
                _editor = null;
            }

            _editor = Editor.CreateEditor(stateView.state);

            if (_editor == null)
            {
                var errorMessage = new Label("Failed to create editor for this state");
                errorMessage.style.fontSize = 11;
                errorMessage.style.color = _errorColor;
                errorMessage.style.unityFontStyleAndWeight = FontStyle.Italic;
                errorMessage.style.unityTextAlign = TextAnchor.MiddleCenter;
                errorMessage.style.paddingTop = 20;
                errorMessage.style.paddingBottom = 20;
                _propertiesContainer.Add(errorMessage);
                return;
            }

            _editorContainer = new IMGUIContainer(() =>
            {
                if (_editor != null && _editor.target != null)
                    try
                    {
                        GUIUtility.GetControlID(FocusType.Passive);
                        GUILayout.Space(8);

                        EditorGUI.BeginChangeCheck();
                        _editor.serializedObject.Update();
                        _editor.OnInspectorGUI();

                        if (EditorGUI.EndChangeCheck())
                        {
                            _editor.serializedObject.ApplyModifiedProperties();
                            EditorUtility.SetDirty(stateView.state);
                        }

                        GUILayout.Space(8);
                    }
                    catch (Exception)
                    {
                        // Handle GUI exceptions gracefully
                    }
            });

            _editorContainer.style.paddingLeft = 8;
            _editorContainer.style.paddingRight = 8;
            _editorContainer.style.paddingTop = 8;
            _editorContainer.style.paddingBottom = 8;
            _editorContainer.style.marginTop = 4;
            _editorContainer.style.overflow = Overflow.Hidden;
            _editorContainer.style.flexShrink = 0;
            _editorContainer.style.minHeight = 60;

            _propertiesContainer.Add(_editorContainer);

            schedule.Execute(() =>
            {
                if (_editorContainer != null) _editorContainer.MarkDirtyRepaint();
            }).ExecuteLater(1);
        }

        private string GetFormattedStateName(ActionStateNode state)
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

       
        private string GetStateTypeCategory(ActionStateNode state)
        {
            if (state is EntryStateNode) return "ENTRY STATE";
            if (state is StateNodeDebugLog) return "DEBUG STATE";
            if (state is ActionStateNode) return "ACTION STATE";
            return "STATE NODE";
        }

        private void SelectStateAsset()
        {
            if (_selectedStateView?.state != null)
            {
                Selection.activeObject = _selectedStateView.state;
                EditorGUIUtility.PingObject(_selectedStateView.state);
            }
        }

        private void FocusOnState()
        {
            if (_selectedStateView != null)
            {
                // Focus functionality implementation can be added here
            }
        }

        public new class UxmlFactory : UxmlFactory<StateMachineInspectorView, UxmlTraits>
        {
        }
    }
}