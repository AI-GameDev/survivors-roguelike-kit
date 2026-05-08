using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace RGame.ScriptableCoreKit
{
    public class BlackboardView : VisualElement
    {
        public new class UxmlFactory : UxmlFactory<BlackboardView, UxmlTraits> { }

        private readonly BlackboardRenderer _renderer;
        private BlackboardTable _blackboardTable;
        
        private ScrollView _scrollView;
        private VisualElement _headerContainer;
        private VisualElement _contentContainer;
        private Button _editButton;
        private Label _titleLabel;
        
        // Blackboard display state
        private bool _blackboardExpanded = true;

        public BlackboardView()
        {
            // Initialize renderer with same settings as StateMachineWindow
            _renderer = new BlackboardRenderer
            {
                AllowDragging = true,
                AllowRemoving = true,
                AllowAdding = false,
                ReadOnlyKeys = false
            };
            
            _blackboardTable = AssetDatabase.LoadAssetAtPath<BlackboardTable>("Assets/BlackboardTable.asset");
            
            SetupUI();
            RefreshBlackboardRenderer();
        }

        private void SetupUI()
        {
            // Apply CSS classes for styling
            AddToClassList("blackboard-view");
            
            // Create header container
            _headerContainer = new VisualElement();
            _headerContainer.AddToClassList("blackboard-header");
            _headerContainer.style.flexDirection = FlexDirection.Row;
            _headerContainer.style.alignItems = Align.Center;
            _headerContainer.style.paddingTop = 4;
            _headerContainer.style.paddingBottom = 4;
            _headerContainer.style.paddingLeft = 8;
            _headerContainer.style.paddingRight = 8;
            _headerContainer.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 0.3f);
            
            // Create title label with foldout functionality
            _titleLabel = new Label($"Blackboard Variables ({_renderer?.GetSlotsCount() ?? 0})");
            _titleLabel.style.fontSize = 13;
            _titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _titleLabel.style.flexGrow = 1;
            
            // Make title clickable for expand/collapse
            _titleLabel.RegisterCallback<MouseDownEvent>(OnHeaderClicked);
            _titleLabel.style.cursor = StyleKeyword.Auto;
            
            // Create edit button
            _editButton = new Button(OnEditButtonClicked);
            _editButton.text = "Edit";
            _editButton.style.width = 40;
            _editButton.style.height = 20;
            
            _headerContainer.Add(_titleLabel);
            _headerContainer.Add(_editButton);
            
            // Create content container
            _contentContainer = new VisualElement();
            _contentContainer.AddToClassList("blackboard-content");
            _contentContainer.style.paddingTop = 4;
            
            // Create scroll view for blackboard variables
            _scrollView = new ScrollView(ScrollViewMode.Vertical);
            _scrollView.style.flexGrow = 1;
            _contentContainer.Add(_scrollView);
            
            Add(_headerContainer);
            Add(_contentContainer);
            
            UpdateVisibility();
        }

        private void OnHeaderClicked(MouseDownEvent evt)
        {
            _blackboardExpanded = !_blackboardExpanded;
            UpdateVisibility();
            UpdateTitleLabel();
        }

        private void OnEditButtonClicked()
        {
            if (_blackboardTable != null)
            {
                BlackboardWindow.OpenAndSet(_blackboardTable);
            }
        }

        private void UpdateVisibility()
        {
            _contentContainer.style.display = _blackboardExpanded ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void UpdateTitleLabel()
        {
            var expandIcon = _blackboardExpanded ? "▼" : "►";
            var count = _renderer?.GetSlotsCount() ?? 0;
            _titleLabel.text = $"{expandIcon} Blackboard Variables ({count})";
        }

        private void RefreshBlackboardRenderer()
        {
            if (_renderer != null && _blackboardTable != null)
            {
                _renderer.SetTarget(_blackboardTable);
                _renderer.UpdateConfiguration(
                    allowDragging: true,
                    allowRemoving: true,
                    allowAdding: false,
                    readOnlyKeys: false
                );
                
                UpdateTitleLabel();
                RefreshContent();
            }
        }

        private void RefreshContent()
        {
            // Clear existing content
            _scrollView.Clear();
            
            if (_blackboardTable == null)
            {
                DrawNoBlackboardState();
                return;
            }

            if (!_renderer.HasSlots())
            {
                DrawEmptyBlackboardState();
                return;
            }

            // Create a container for the blackboard renderer
            var rendererContainer = new IMGUIContainer(() =>
            {
                if (_renderer != null)
                {
                    _renderer.DrawBlackboardList();
                }
            });
            
            _scrollView.Add(rendererContainer);
        }

        private void DrawNoBlackboardState()
        {
            var container = new VisualElement();
            container.style.alignItems = Align.Center;
            container.style.justifyContent = Justify.Center;
            container.style.paddingTop = 20;
            container.style.paddingBottom = 20;
            container.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.3f);
            
            var titleLabel = new Label("No Blackboard Assigned");
            titleLabel.style.fontSize = 12;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            titleLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            
            var descLabel = new Label("Assign a BlackboardTable in the\nStateMachine component inspector");
            descLabel.style.fontSize = 10;
            descLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            descLabel.style.whiteSpace = WhiteSpace.Normal;
            descLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            descLabel.style.marginTop = 8;
            
            container.Add(titleLabel);
            container.Add(descLabel);
            _scrollView.Add(container);
        }

        private void DrawEmptyBlackboardState()
        {
            var container = new VisualElement();
            container.style.alignItems = Align.Center;
            container.style.justifyContent = Justify.Center;
            container.style.paddingTop = 16;
            container.style.paddingBottom = 16;
            container.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.3f);
            
            var titleLabel = new Label("No variables defined");
            titleLabel.style.fontSize = 12;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            titleLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            
            var addButton = new Button(() => BlackboardWindow.OpenAndSet(_blackboardTable));
            addButton.text = "Add Variables";
            addButton.style.marginTop = 8;
            addButton.style.width = 100;
            
            container.Add(titleLabel);
            container.Add(addButton);
            _scrollView.Add(container);
        }

        /// <summary>
        /// Set the blackboard table to display
        /// </summary>
        public void SetBlackboardTable(BlackboardTable blackboardTable)
        {
            _blackboardTable = blackboardTable;
            RefreshBlackboardRenderer();
        }

        /// <summary>
        /// Refresh the blackboard display
        /// </summary>
        public void Refresh()
        {
            RefreshBlackboardRenderer();
        }

        /// <summary>
        /// Get/Set expanded state
        /// </summary>
        public bool IsExpanded
        {
            get => _blackboardExpanded;
            set
            {
                _blackboardExpanded = value;
                UpdateVisibility();
                UpdateTitleLabel();
            }
        }

        /// <summary>
        /// Get the current blackboard table
        /// </summary>
        public BlackboardTable BlackboardTable => _blackboardTable;

        private void OnDestroy()
        {
            _renderer?.Dispose();
        }
    }
}