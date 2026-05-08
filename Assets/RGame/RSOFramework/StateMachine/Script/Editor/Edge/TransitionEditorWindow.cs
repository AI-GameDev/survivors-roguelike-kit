using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.Reflection;
using System;
using System.Linq;

namespace RGame.ScriptableCoreKit
{
    /// <summary>
    /// Inspector view for editing state transitions with mode selection support
    /// </summary>
    public class TransitionInspectorView : VisualElement
    {
        private StateTransition _stateTransition;
        private ActionStateNode _fromState;
        private StateMachineEdge _targetEdge;
        private VisualElement _transitionsContainer;
        private ScrollView _scrollView;
        private Label _titleLabel;
        private EnumField _transitionModeField;
        private VisualElement _modeContainer;
        private Label _modeDescriptionLabel;
        
        public TransitionInspectorView()
        {
            CreateUI();
            ShowEmptyState();
        }
        
        private void CreateUI()
        {
            style.flexGrow = 1;
            style.backgroundColor = EditorGUIUtility.isProSkin ? new Color(0.22f, 0.22f, 0.22f, 0.95f) : new Color(0.76f, 0.76f, 0.76f, 0.95f);
            style.paddingTop = 8;
            style.paddingBottom = 8;
            style.paddingLeft = 8;
            style.paddingRight = 8;
            
            _titleLabel = new Label("Transition Editor");
            _titleLabel.style.fontSize = 16;
            _titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _titleLabel.style.color = new Color(0.2f, 0.6f, 1f, 1f);
            _titleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _titleLabel.style.marginBottom = 10;
            
            // Create transition mode selection container
            CreateTransitionModeUI();
            
            var addButton = new Button(ShowAddTransitionMenu);
            addButton.text = "Add Condition";
            addButton.style.backgroundColor = new Color(0.2f, 0.6f, 1f);
            addButton.style.color = Color.white;
            addButton.style.borderTopWidth = 0;
            addButton.style.borderBottomWidth = 0;
            addButton.style.borderLeftWidth = 0;
            addButton.style.borderRightWidth = 0;
            addButton.style.borderTopLeftRadius = 4;
            addButton.style.borderTopRightRadius = 4;
            addButton.style.borderBottomLeftRadius = 4;
            addButton.style.borderBottomRightRadius = 4;
            addButton.style.marginBottom = 10;
            addButton.style.height = 30;
            
            _scrollView = new ScrollView(ScrollViewMode.Vertical);
            _scrollView.style.flexGrow = 1;
            _scrollView.style.borderTopWidth = 1;
            _scrollView.style.borderBottomWidth = 1;
            _scrollView.style.borderLeftWidth = 1;
            _scrollView.style.borderRightWidth = 1;
            _scrollView.style.borderTopColor = new Color(0.5f, 0.5f, 0.5f);
            _scrollView.style.borderBottomColor = new Color(0.5f, 0.5f, 0.5f);
            _scrollView.style.borderLeftColor = new Color(0.5f, 0.5f, 0.5f);
            _scrollView.style.borderRightColor = new Color(0.5f, 0.5f, 0.5f);
            _scrollView.style.borderTopLeftRadius = 4;
            _scrollView.style.borderTopRightRadius = 4;
            _scrollView.style.borderBottomLeftRadius = 4;
            _scrollView.style.borderBottomRightRadius = 4;
            
            _transitionsContainer = new VisualElement();
            _transitionsContainer.style.paddingTop = 10;
            _transitionsContainer.style.paddingBottom = 10;
            _transitionsContainer.style.paddingLeft = 10;
            _transitionsContainer.style.paddingRight = 10;
            
            _scrollView.Add(_transitionsContainer);
            
            var clearAllButton = new Button(ClearAllTransitions);
            clearAllButton.text = "Clear All";
            clearAllButton.style.backgroundColor = new Color(1f, 0.4f, 0.4f);
            clearAllButton.style.color = Color.white;
            clearAllButton.style.borderTopWidth = 0;
            clearAllButton.style.borderBottomWidth = 0;
            clearAllButton.style.borderLeftWidth = 0;
            clearAllButton.style.borderRightWidth = 0;
            clearAllButton.style.borderTopLeftRadius = 4;
            clearAllButton.style.borderTopRightRadius = 4;
            clearAllButton.style.borderBottomLeftRadius = 4;
            clearAllButton.style.borderBottomRightRadius = 4;
            clearAllButton.style.marginTop = 10;
            clearAllButton.style.height = 25;
            
            Add(_titleLabel);
            Add(_modeContainer);
            Add(addButton);
            Add(_scrollView);
            Add(clearAllButton);
        }
        
        private void CreateTransitionModeUI()
        {
            _modeContainer = new VisualElement();
            _modeContainer.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f, 0.8f);
            _modeContainer.style.borderTopLeftRadius = 6;
            _modeContainer.style.borderTopRightRadius = 6;
            _modeContainer.style.borderBottomLeftRadius = 6;
            _modeContainer.style.borderBottomRightRadius = 6;
            _modeContainer.style.marginBottom = 10;
            _modeContainer.style.paddingTop = 10;
            _modeContainer.style.paddingBottom = 10;
            _modeContainer.style.paddingLeft = 12;
            _modeContainer.style.paddingRight = 12;
            _modeContainer.style.borderTopWidth = 1;
            _modeContainer.style.borderBottomWidth = 1;
            _modeContainer.style.borderLeftWidth = 1;
            _modeContainer.style.borderRightWidth = 1;
            _modeContainer.style.borderTopColor = new Color(0.4f, 0.4f, 0.4f);
            _modeContainer.style.borderBottomColor = new Color(0.4f, 0.4f, 0.4f);
            _modeContainer.style.borderLeftColor = new Color(0.4f, 0.4f, 0.4f);
            _modeContainer.style.borderRightColor = new Color(0.4f, 0.4f, 0.4f);
            
            var modeTitle = new Label("Transition Mode");
            modeTitle.style.fontSize = 12;
            modeTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            modeTitle.style.color = new Color(0.9f, 0.9f, 0.9f);
            modeTitle.style.marginBottom = 8;
            
            var modeFieldContainer = new VisualElement();
            modeFieldContainer.style.flexDirection = FlexDirection.Row;
            modeFieldContainer.style.alignItems = Align.Center;
            
            var modeLabel = new Label("Evaluation Mode:");
            modeLabel.style.width = 120;
            modeLabel.style.fontSize = 11;
            modeLabel.style.color = new Color(0.8f, 0.8f, 0.8f);
            
            _transitionModeField = new EnumField(TransitionMode.All);
            _transitionModeField.style.flexGrow = 1;
            _transitionModeField.RegisterValueChangedCallback(OnTransitionModeChanged);
            
            modeFieldContainer.Add(modeLabel);
            modeFieldContainer.Add(_transitionModeField);
            
            _modeDescriptionLabel = new Label();
            _modeDescriptionLabel.style.fontSize = 10;
            _modeDescriptionLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            _modeDescriptionLabel.style.whiteSpace = WhiteSpace.Normal;
            _modeDescriptionLabel.style.marginTop = 6;
            
            _modeContainer.Add(modeTitle);
            _modeContainer.Add(modeFieldContainer);
            _modeContainer.Add(_modeDescriptionLabel);
            
            UpdateModeDescription(TransitionMode.All);
        }
        
        private void OnTransitionModeChanged(ChangeEvent<Enum> evt)
        {
            if (_stateTransition != null && evt.newValue is TransitionMode newMode)
            {
                _stateTransition.Mode = newMode;
                UpdateModeDescription(newMode);
                MarkFromStateDirty();
            }
        }
        
        private void UpdateModeDescription(TransitionMode mode)
        {
            switch (mode)
            {
                case TransitionMode.Any:
                    _modeDescriptionLabel.text = "Transition will trigger when ANY condition is met";
                    _modeDescriptionLabel.style.color = new Color(0.3f, 0.8f, 0.3f);
                    break;
                case TransitionMode.All:
                    _modeDescriptionLabel.text = "Transition will trigger only when ALL conditions are met";
                    _modeDescriptionLabel.style.color = new Color(1f, 0.7f, 0.3f);
                    break;
            }
        }
        
        public void SetTransition(StateMachineEdge edge)
        {
            if (edge?.StateTransition == null || edge.FromState == null)
            {
                ShowEmptyState();
                return;
            }
            
            _stateTransition = edge.StateTransition;
            _fromState = edge.FromState;
            _targetEdge = edge;
            
            _titleLabel.text = $"Transitions: {_fromState.GetDisplayName()} → {_stateTransition.TargetState.GetDisplayName()}";
            
            // Update transition mode field
            _transitionModeField.value = _stateTransition.Mode;
            UpdateModeDescription(_stateTransition.Mode);
            
            // Show mode container
            _modeContainer.style.display = DisplayStyle.Flex;
            
            RefreshTransitionsList();
        }
        
        private void ShowEmptyState()
        {
            _titleLabel.text = "Transition Editor";
            _transitionsContainer.Clear();
            
            // Hide mode container when no transition is selected
            _modeContainer.style.display = DisplayStyle.None;
            
            var emptyLabel = new Label("Select a connection to edit transitions");
            emptyLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            emptyLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            emptyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            emptyLabel.style.paddingTop = 40;
            emptyLabel.style.paddingBottom = 40;
            _transitionsContainer.Add(emptyLabel);
        }
        
        private void ShowAddTransitionMenu()
        {
            if (_stateTransition == null) return;
    
            var transitionTypes = TypeCache.GetTypesDerivedFrom<BaseTransition>();
            
            var worldBounds = this.worldBound;
            var position = new Vector2(worldBounds.center.x, worldBounds.center.y);
    
            TypeSelectionPanel.Show(position, transitionTypes, "Add Transition Condition", AddTransition);
        }
        
        private void AddTransition(Type transitionType)
        {
            try
            {
                var transition = Activator.CreateInstance(transitionType) as BaseTransition;
                if (transition != null)
                {
                    _stateTransition.AddTransition(transition);
                    RefreshTransitionsList();
                    MarkFromStateDirty();
                    AssetDatabase.SaveAssets();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to create transition of type {transitionType.Name}: {e.Message}");
            }
        }
        
        private void RemoveTransition(BaseTransition transition)
        {
            _stateTransition.Transitions.Remove(transition);
            RefreshTransitionsList();
            MarkFromStateDirty();
        }
        
        private void ClearAllTransitions()
        {
            if (_stateTransition == null) return;
            
            _stateTransition.Transitions.Clear();
            RefreshTransitionsList();
            MarkFromStateDirty();
        }
        
        private void RefreshTransitionsList()
        {
            _transitionsContainer.Clear();
            
            if (_stateTransition?.Transitions == null || _stateTransition.Transitions.Count == 0)
            {
                var emptyLabel = new Label("No transition conditions. This transition will always trigger.");
                emptyLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
                emptyLabel.style.color = new Color(1f, 0.7f, 0.3f);
                emptyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                emptyLabel.style.paddingTop = 20;
                emptyLabel.style.paddingBottom = 20;
                _transitionsContainer.Add(emptyLabel);
                return;
            }
            
            // Add mode information at the top if there are multiple conditions
            if (_stateTransition.Transitions.Count > 1)
            {
                var modeInfo = new Label($"Mode: {_stateTransition.Mode} - {(_stateTransition.Mode == TransitionMode.Any ? "Any condition can trigger" : "All conditions must be true")}");
                modeInfo.style.fontSize = 10;
                modeInfo.style.unityFontStyleAndWeight = FontStyle.Bold;
                modeInfo.style.color = _stateTransition.Mode == TransitionMode.Any ? new Color(0.3f, 0.8f, 0.3f) : new Color(1f, 0.7f, 0.3f);
                modeInfo.style.unityTextAlign = TextAnchor.MiddleCenter;
                modeInfo.style.marginBottom = 10;
                modeInfo.style.paddingTop = 8;
                modeInfo.style.paddingBottom = 8;
                modeInfo.style.backgroundColor = new Color(0, 0, 0, 0.3f);
                modeInfo.style.borderTopLeftRadius = 4;
                modeInfo.style.borderTopRightRadius = 4;
                modeInfo.style.borderBottomLeftRadius = 4;
                modeInfo.style.borderBottomRightRadius = 4;
                _transitionsContainer.Add(modeInfo);
            }
            
            for (int i = 0; i < _stateTransition.Transitions.Count; i++)
            {
                var transition = _stateTransition.Transitions[i];
                var transitionElement = CreateTransitionElement(transition, i);
                _transitionsContainer.Add(transitionElement);
            }
        }
        
        private VisualElement CreateTransitionElement(BaseTransition transition, int index)
        {
            var container = new VisualElement();
            container.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f, 0.8f);
            container.style.borderTopLeftRadius = 6;
            container.style.borderTopRightRadius = 6;
            container.style.borderBottomLeftRadius = 6;
            container.style.borderBottomRightRadius = 6;
            container.style.marginBottom = 8;
            container.style.paddingTop = 8;
            container.style.paddingBottom = 8;
            container.style.paddingLeft = 10;
            container.style.paddingRight = 10;
            container.style.borderTopWidth = 1;
            container.style.borderBottomWidth = 1;
            container.style.borderLeftWidth = 1;
            container.style.borderRightWidth = 1;
            container.style.borderTopColor = new Color(0.4f, 0.4f, 0.4f);
            container.style.borderBottomColor = new Color(0.4f, 0.4f, 0.4f);
            container.style.borderLeftColor = new Color(0.4f, 0.4f, 0.4f);
            container.style.borderRightColor = new Color(0.4f, 0.4f, 0.4f);
            
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 8;
            
            var titleLabel = new Label($"{index + 1}. {transition.GetType().Name.Replace("Transition", "")}");
            titleLabel.style.fontSize = 12;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = new Color(0.3f, 0.7f, 1f);
            titleLabel.style.flexGrow = 1;
            
            var removeButton = new Button(() => RemoveTransition(transition));
            removeButton.text = "✕";
            removeButton.style.width = 20;
            removeButton.style.height = 20;
            removeButton.style.fontSize = 12;
            removeButton.style.backgroundColor = new Color(1f, 0.4f, 0.4f);
            removeButton.style.color = Color.white;
            removeButton.style.borderTopWidth = 0;
            removeButton.style.borderBottomWidth = 0;
            removeButton.style.borderLeftWidth = 0;
            removeButton.style.borderRightWidth = 0;
            removeButton.style.borderTopLeftRadius = 10;
            removeButton.style.borderTopRightRadius = 10;
            removeButton.style.borderBottomLeftRadius = 10;
            removeButton.style.borderBottomRightRadius = 10;
            
            header.Add(titleLabel);
            header.Add(removeButton);
            
            var propertiesContainer = new VisualElement();
            CreateTransitionPropertyFields(transition, propertiesContainer);
            
            container.Add(header);
            container.Add(propertiesContainer);
            
            return container;
        }
        
        private void CreateTransitionPropertyFields(BaseTransition transition, VisualElement container)
        {
            var type = transition.GetType();
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                            .Where(f => f.GetCustomAttribute<SerializeField>() != null || f.IsPublic)
                            .ToArray();
            
            foreach (var field in fields)
            {
                var fieldContainer = new VisualElement();
                fieldContainer.style.flexDirection = FlexDirection.Row;
                fieldContainer.style.alignItems = Align.Center;
                fieldContainer.style.marginBottom = 4;
                
                var label = new Label(ObjectNames.NicifyVariableName(field.Name));
                label.style.width = 120;
                label.style.fontSize = 11;
                
                VisualElement fieldElement = null;
                
                if (field.FieldType == typeof(float))
                {
                    var floatField = new FloatField();
                    floatField.value = (float)field.GetValue(transition);
                    floatField.RegisterValueChangedCallback(evt => 
                    {
                        field.SetValue(transition, evt.newValue);
                        MarkFromStateDirty();
                    });
                    fieldElement = floatField;
                }
                else if (field.FieldType == typeof(int))
                {
                    var intField = new IntegerField();
                    intField.value = (int)field.GetValue(transition);
                    intField.RegisterValueChangedCallback(evt => 
                    {
                        field.SetValue(transition, evt.newValue);
                        MarkFromStateDirty();
                    });
                    fieldElement = intField;
                }
                else if (field.FieldType == typeof(bool))
                {
                    var boolField = new Toggle();
                    boolField.value = (bool)field.GetValue(transition);
                    boolField.RegisterValueChangedCallback(evt => 
                    {
                        field.SetValue(transition, evt.newValue);
                        MarkFromStateDirty();
                    });
                    fieldElement = boolField;
                }
                else if (field.FieldType == typeof(string))
                {
                    var stringField = new TextField();
                    stringField.value = (string)field.GetValue(transition) ?? "";
                    stringField.RegisterValueChangedCallback(evt => 
                    {
                        field.SetValue(transition, evt.newValue);
                        MarkFromStateDirty();
                    });
                    fieldElement = stringField;
                }
                else if (field.FieldType.IsEnum)
                {
                    var enumField = new EnumField((Enum)field.GetValue(transition));
                    enumField.RegisterValueChangedCallback(evt => 
                    {
                        field.SetValue(transition, evt.newValue);
                        MarkFromStateDirty();
                    });
                    fieldElement = enumField;
                }
                
                if (fieldElement != null)
                {
                    fieldElement.style.flexGrow = 1;
                    fieldContainer.Add(label);
                    fieldContainer.Add(fieldElement);
                    container.Add(fieldContainer);
                }
            }
            
            if (container.childCount == 0)
            {
                var noPropsLabel = new Label("No editable properties");
                noPropsLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
                noPropsLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
                noPropsLabel.style.fontSize = 10;
                container.Add(noPropsLabel);
            }
        }
        
        private void MarkFromStateDirty()
        {
            if (_fromState != null)
            {
                EditorUtility.SetDirty(_fromState);
                
                var stateMachineSO = AssetDatabase.LoadAssetAtPath<StateMachineSO>(AssetDatabase.GetAssetPath(_fromState));
                if (stateMachineSO != null)
                {
                    EditorUtility.SetDirty(stateMachineSO);
                }
            }
        }
        
        public new class UxmlFactory : UxmlFactory<TransitionInspectorView, UxmlTraits>
        {
        }
    }
}