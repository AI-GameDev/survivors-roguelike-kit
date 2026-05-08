using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace RGame.ScriptableCoreKit
{
    /// <summary>
    /// Modern type selection panel with search and filtering capabilities
    /// </summary>
    public class TypeSelectionPanel : VisualElement
    {
        private const float PANEL_WIDTH = 300f;
        private const float PANEL_HEIGHT = 400f;
        private const float ANIMATION_DURATION = 0.2f;
        
        private TextField _searchField;
        private ScrollView _scrollView;
        private VisualElement _itemsContainer;
        private VisualElement _overlay;
        private VisualElement _panel;
        private VisualElement _headerContainer;
        private Label _titleLabel;
        private Button _closeButton;
        
        private List<TypeInfo> _allItems = new List<TypeInfo>();
        private List<TypeInfo> _filteredItems = new List<TypeInfo>();
        private Action<Type> _onTypeSelected;
        private string _currentFilter = "";
        
        public TypeSelectionPanel()
        {
            CreateStructure();
            SetupStyling();
            SetupInteractions();
        }
        
        /// <summary>
        /// Shows the panel with specified types and callback
        /// </summary>
        public static void Show(Vector2 position, IEnumerable<Type> types, string title, Action<Type> onSelected)
        {
            // Try to get the focused window's root element
            var root = EditorWindow.focusedWindow?.rootVisualElement;
            if (root == null)
            {
                // Fallback: try to find StateMachineWindow
                var windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
                foreach (var window in windows)
                {
                    if (window.GetType().Name == "StateMachineWindow")
                    {
                        root = window.rootVisualElement;
                        break;
                    }
                }
            }
            
            if (root == null)
            {
                Debug.LogWarning("Cannot find root visual element to show TypeSelectionPanel");
                return;
            }
            
            var panel = new TypeSelectionPanel();
            panel.SetTypes(types, title, onSelected);
            panel.ShowAt(root, position);
        }
        
        private void CreateStructure()
        {
            style.position = Position.Absolute;
            style.top = 0;
            style.left = 0;
            style.right = 0;
            style.bottom = 0;
            pickingMode = PickingMode.Position;
            
            // Professional dark overlay
            _overlay = new VisualElement();
            _overlay.style.position = Position.Absolute;
            _overlay.style.top = 0;
            _overlay.style.left = 0;
            _overlay.style.right = 0;
            _overlay.style.bottom = 0;
            _overlay.style.backgroundColor = new Color(0.08f, 0.08f, 0.12f, 0.85f);
            _overlay.style.opacity = 0;
            
            // Professional main panel with shadow effect
            _panel = new VisualElement();
            _panel.style.position = Position.Absolute;
            _panel.style.width = PANEL_WIDTH;
            _panel.style.height = PANEL_HEIGHT;
            _panel.style.backgroundColor = new Color(0.15f, 0.15f, 0.18f, 1f);
            _panel.style.borderTopLeftRadius = 12;
            _panel.style.borderTopRightRadius = 12;
            _panel.style.borderBottomLeftRadius = 12;
            _panel.style.borderBottomRightRadius = 12;
            _panel.style.borderTopWidth = 1;
            _panel.style.borderBottomWidth = 4;
            _panel.style.borderLeftWidth = 1;
            _panel.style.borderRightWidth = 1;
            _panel.style.borderTopColor = new Color(0.35f, 0.35f, 0.4f, 1f);
            _panel.style.borderBottomColor = new Color(0.05f, 0.05f, 0.08f, 1f);
            _panel.style.borderLeftColor = new Color(0.25f, 0.25f, 0.3f, 1f);
            _panel.style.borderRightColor = new Color(0.25f, 0.25f, 0.3f, 1f);
            _panel.style.scale = new StyleScale(new Vector2(0.9f, 0.9f));
            _panel.style.opacity = 0;
            
            // Add subtle inner shadow effect
            var shadowInner = new VisualElement();
            shadowInner.style.position = Position.Absolute;
            shadowInner.style.top = 0;
            shadowInner.style.left = 0;
            shadowInner.style.right = 0;
            shadowInner.style.bottom = 0;
            shadowInner.style.borderTopLeftRadius = 12;
            shadowInner.style.borderTopRightRadius = 12;
            shadowInner.style.borderBottomLeftRadius = 12;
            shadowInner.style.borderBottomRightRadius = 12;
            shadowInner.style.borderTopWidth = 1;
            shadowInner.style.borderTopColor = new Color(0.4f, 0.4f, 0.45f, 0.3f);
            shadowInner.pickingMode = PickingMode.Ignore;
            
            CreateHeader();
            CreateSearchField();
            CreateScrollView();
            
            _panel.Add(shadowInner);
            _overlay.Add(_panel);
            Add(_overlay);
        }
        
        private void CreateHeader()
        {
            _headerContainer = new VisualElement();
            _headerContainer.style.flexDirection = FlexDirection.Row;
            _headerContainer.style.alignItems = Align.Center;
            _headerContainer.style.paddingTop = 16;
            _headerContainer.style.paddingBottom = 16;
            _headerContainer.style.paddingLeft = 20;
            _headerContainer.style.paddingRight = 20;
            _headerContainer.style.backgroundColor = new Color(0.12f, 0.12f, 0.15f, 1f);
            _headerContainer.style.borderTopLeftRadius = 12;
            _headerContainer.style.borderTopRightRadius = 12;
            _headerContainer.style.borderBottomWidth = 1;
            _headerContainer.style.borderBottomColor = new Color(0.25f, 0.25f, 0.3f, 1f);
            
            // Add subtle gradient effect
            var gradient = new VisualElement();
            gradient.style.position = Position.Absolute;
            gradient.style.top = 0;
            gradient.style.left = 0;
            gradient.style.right = 0;
            gradient.style.height = 3;
            gradient.style.backgroundColor = new Color(0.4f, 0.6f, 1f, 0.6f);
            gradient.style.borderTopLeftRadius = 12;
            gradient.style.borderTopRightRadius = 12;
            gradient.pickingMode = PickingMode.Ignore;
            
            _titleLabel = new Label("Select Type");
            _titleLabel.style.fontSize = 15;
            _titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _titleLabel.style.color = new Color(0.9f, 0.9f, 0.95f, 1f);
            _titleLabel.style.flexGrow = 1;
            _titleLabel.style.letterSpacing = 0.5f;
            
            _closeButton = new Button(Hide);
            _closeButton.text = "✕";
            _closeButton.style.width = 28;
            _closeButton.style.height = 28;
            _closeButton.style.fontSize = 14;
            _closeButton.style.backgroundColor = new Color(0.8f, 0.3f, 0.3f, 1f);
            _closeButton.style.color = Color.white;
            _closeButton.style.borderTopWidth = 0;
            _closeButton.style.borderBottomWidth = 0;
            _closeButton.style.borderLeftWidth = 0;
            _closeButton.style.borderRightWidth = 0;
            _closeButton.style.borderTopLeftRadius = 14;
            _closeButton.style.borderTopRightRadius = 14;
            _closeButton.style.borderBottomLeftRadius = 14;
            _closeButton.style.borderBottomRightRadius = 14;
            _closeButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            
            // Close button hover effect
            _closeButton.RegisterCallback<MouseEnterEvent>(evt => {
                _closeButton.style.backgroundColor = new Color(1f, 0.4f, 0.4f, 1f);
                _closeButton.style.scale = new StyleScale(new Vector2(1.1f, 1.1f));
            });
            _closeButton.RegisterCallback<MouseLeaveEvent>(evt => {
                _closeButton.style.backgroundColor = new Color(0.8f, 0.3f, 0.3f, 1f);
                _closeButton.style.scale = new StyleScale(new Vector2(1f, 1f));
            });
            
            _headerContainer.Add(gradient);
            _headerContainer.Add(_titleLabel);
            _headerContainer.Add(_closeButton);
            _panel.Add(_headerContainer);
        }
        
        private void CreateSearchField()
        {
            var searchContainer = new VisualElement();
            searchContainer.style.paddingTop = 12;
            searchContainer.style.paddingBottom = 12;
            searchContainer.style.paddingLeft = 20;
            searchContainer.style.paddingRight = 20;
            searchContainer.style.backgroundColor = new Color(0.15f, 0.15f, 0.18f, 1f);
            
            var searchWrapper = new VisualElement();
            searchWrapper.style.position = Position.Relative;
            searchWrapper.style.flexDirection = FlexDirection.Row;
            searchWrapper.style.alignItems = Align.Center;
            searchWrapper.style.backgroundColor = new Color(0.08f, 0.08f, 0.12f, 1f);
            searchWrapper.style.borderTopLeftRadius = 8;
            searchWrapper.style.borderTopRightRadius = 8;
            searchWrapper.style.borderBottomLeftRadius = 8;
            searchWrapper.style.borderBottomRightRadius = 8;
            searchWrapper.style.borderTopWidth = 1;
            searchWrapper.style.borderBottomWidth = 1;
            searchWrapper.style.borderLeftWidth = 1;
            searchWrapper.style.borderRightWidth = 1;
            searchWrapper.style.borderTopColor = new Color(0.3f, 0.3f, 0.35f, 1f);
            searchWrapper.style.borderBottomColor = new Color(0.2f, 0.2f, 0.25f, 1f);
            searchWrapper.style.borderLeftColor = new Color(0.25f, 0.25f, 0.3f, 1f);
            searchWrapper.style.borderRightColor = new Color(0.25f, 0.25f, 0.3f, 1f);
            searchWrapper.style.height = 36;
            
            // Search icon
            var searchIcon = new Label("🔍");
            searchIcon.style.fontSize = 14;
            searchIcon.style.color = new Color(0.6f, 0.6f, 0.7f, 1f);
            searchIcon.style.paddingLeft = 12;
            searchIcon.style.paddingRight = 8;
            searchIcon.pickingMode = PickingMode.Ignore;
            
            _searchField = new TextField();
            _searchField.style.flexGrow = 1;
            _searchField.style.backgroundColor = Color.clear;
            _searchField.style.borderTopWidth = 0;
            _searchField.style.borderBottomWidth = 0;
            _searchField.style.borderLeftWidth = 0;
            _searchField.style.borderRightWidth = 0;
            _searchField.style.fontSize = 13;
            _searchField.style.color = new Color(0.9f, 0.9f, 0.95f, 1f);
            _searchField.style.paddingRight = 12;
            
            // Customize the text input element
            var textInput = _searchField.Q<VisualElement>("unity-text-input");
            if (textInput != null)
            {
                textInput.style.backgroundColor = Color.clear;
                textInput.style.borderTopWidth = 0;
                textInput.style.borderBottomWidth = 0;
                textInput.style.borderLeftWidth = 0;
                textInput.style.borderRightWidth = 0;
                textInput.style.paddingLeft = 0;
                textInput.style.paddingRight = 0;
                textInput.style.paddingTop = 6;
                textInput.style.paddingBottom = 6;
            }
            
            // Placeholder text
            var placeholderLabel = new Label("Search components...");
            placeholderLabel.style.position = Position.Absolute;
            placeholderLabel.style.left = 42;
            placeholderLabel.style.top = 10;
            placeholderLabel.style.fontSize = 13;
            placeholderLabel.style.color = new Color(0.5f, 0.5f, 0.6f, 1f);
            placeholderLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            placeholderLabel.pickingMode = PickingMode.Ignore;
            
            _searchField.RegisterValueChangedCallback(evt => {
                _currentFilter = evt.newValue;
                placeholderLabel.style.display = string.IsNullOrEmpty(evt.newValue) ? DisplayStyle.Flex : DisplayStyle.None;
                FilterItems();
            });
            
            // Focus effects
            _searchField.RegisterCallback<FocusInEvent>(evt => {
                searchWrapper.style.borderTopColor = new Color(0.4f, 0.6f, 1f, 1f);
                searchWrapper.style.borderBottomColor = new Color(0.4f, 0.6f, 1f, 1f);
                searchWrapper.style.borderLeftColor = new Color(0.4f, 0.6f, 1f, 1f);
                searchWrapper.style.borderRightColor = new Color(0.4f, 0.6f, 1f, 1f);
            });
            
            _searchField.RegisterCallback<FocusOutEvent>(evt => {
                searchWrapper.style.borderTopColor = new Color(0.3f, 0.3f, 0.35f, 1f);
                searchWrapper.style.borderBottomColor = new Color(0.2f, 0.2f, 0.25f, 1f);
                searchWrapper.style.borderLeftColor = new Color(0.25f, 0.25f, 0.3f, 1f);
                searchWrapper.style.borderRightColor = new Color(0.25f, 0.25f, 0.3f, 1f);
            });
            
            searchWrapper.Add(searchIcon);
            searchWrapper.Add(_searchField);
            searchWrapper.Add(placeholderLabel);
            
            searchContainer.Add(searchWrapper);
            _panel.Add(searchContainer);
        }
        
        private void CreateScrollView()
        {
            _scrollView = new ScrollView(ScrollViewMode.Vertical);
            _scrollView.style.flexGrow = 1;
            _scrollView.style.marginTop = 0;
            _scrollView.style.marginBottom = 0;
            _scrollView.style.marginLeft = 0;
            _scrollView.style.marginRight = 0;
            _scrollView.style.backgroundColor = new Color(0.12f, 0.12f, 0.15f, 1f);
            _scrollView.style.borderBottomLeftRadius = 12;
            _scrollView.style.borderBottomRightRadius = 12;
            
            // Customize scrollbar
            var verticalScroller = _scrollView.Q<Scroller>();
            if (verticalScroller != null)
            {
                verticalScroller.style.backgroundColor = new Color(0.08f, 0.08f, 0.12f, 1f);
                verticalScroller.style.borderLeftWidth = 1;
                verticalScroller.style.borderLeftColor = new Color(0.2f, 0.2f, 0.25f, 1f);
            }
            
            _itemsContainer = new VisualElement();
            _itemsContainer.style.paddingTop = 12;
            _itemsContainer.style.paddingBottom = 12;
            _itemsContainer.style.paddingLeft = 20;
            _itemsContainer.style.paddingRight = 20;
            
            _scrollView.Add(_itemsContainer);
            _panel.Add(_scrollView);
        }
        
        private void SetupStyling()
        {
            // Additional styling if needed
        }
        
        private void SetupInteractions()
        {
            _overlay.RegisterCallback<MouseDownEvent>(evt => {
                if (evt.target == _overlay)
                {
                    Hide();
                    evt.StopPropagation();
                }
            });
            
            RegisterCallback<KeyDownEvent>(evt => {
                if (evt.keyCode == KeyCode.Escape)
                {
                    Hide();
                    evt.StopPropagation();
                }
            });
        }
        
        private void SetTypes(IEnumerable<Type> types, string title, Action<Type> onSelected)
        {
            _titleLabel.text = title;
            _onTypeSelected = onSelected;
            
            _allItems.Clear();
            foreach (var type in types)
            {
                if (type != null && !type.IsAbstract)
                {
                    _allItems.Add(new TypeInfo
                    {
                        Type = type,
                        DisplayName = GetDisplayName(type),
                        Category = GetCategory(type),
                        Description = GetDescription(type)
                    });
                }
            }
            
            _allItems = _allItems.OrderBy(t => t.Category).ThenBy(t => t.DisplayName).ToList();
            FilterItems();
        }
        
        private void FilterItems()
        {
            _filteredItems.Clear();
            
            if (string.IsNullOrEmpty(_currentFilter))
            {
                _filteredItems.AddRange(_allItems);
            }
            else
            {
                var filter = _currentFilter.ToLower();
                _filteredItems.AddRange(_allItems.Where(item => 
                    item.DisplayName.ToLower().Contains(filter) ||
                    item.Category.ToLower().Contains(filter) ||
                    item.Description.ToLower().Contains(filter)));
            }
            
            RefreshItemsDisplay();
        }
        
        private void RefreshItemsDisplay()
        {
            _itemsContainer.Clear();
            
            if (_filteredItems.Count == 0)
            {
                var noResultsContainer = new VisualElement();
                noResultsContainer.style.alignItems = Align.Center;
                noResultsContainer.style.justifyContent = Justify.Center;
                noResultsContainer.style.paddingTop = 40;
                noResultsContainer.style.paddingBottom = 40;
                
                var noResultsIcon = new Label("🔍");
                noResultsIcon.style.fontSize = 32;
                noResultsIcon.style.marginBottom = 12;
                noResultsIcon.style.opacity = 0.5f;
                
                var noResultsLabel = new Label("No matching components found");
                noResultsLabel.style.fontSize = 13;
                noResultsLabel.style.color = new Color(0.6f, 0.6f, 0.7f, 1f);
                noResultsLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                noResultsLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
                noResultsLabel.style.marginBottom = 8;
                
                var suggestionLabel = new Label("Try adjusting your search terms");
                suggestionLabel.style.fontSize = 11;
                suggestionLabel.style.color = new Color(0.5f, 0.5f, 0.6f, 1f);
                suggestionLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                
                noResultsContainer.Add(noResultsIcon);
                noResultsContainer.Add(noResultsLabel);
                noResultsContainer.Add(suggestionLabel);
                
                _itemsContainer.Add(noResultsContainer);
                return;
            }
            
            string currentCategory = "";
            foreach (var item in _filteredItems)
            {
                // Add category header if needed
                if (item.Category != currentCategory)
                {
                    currentCategory = item.Category;
                    var categoryHeader = CreateCategoryHeader(currentCategory);
                    _itemsContainer.Add(categoryHeader);
                }
                
                var itemElement = CreateTypeItem(item);
                _itemsContainer.Add(itemElement);
            }
        }
        
        private VisualElement CreateCategoryHeader(string category)
        {
            var header = new VisualElement();
            header.style.paddingTop = 12;
            header.style.paddingBottom = 8;
            header.style.marginTop = 8;
            header.style.marginBottom = 4;
            
            var labelContainer = new VisualElement();
            labelContainer.style.flexDirection = FlexDirection.Row;
            labelContainer.style.alignItems = Align.Center;
            
            var icon = new Label(GetCategoryIcon(category));
            icon.style.fontSize = 12;
            icon.style.marginRight = 8;
            icon.style.color = new Color(0.4f, 0.6f, 1f, 1f);
            
            var label = new Label(category.ToUpper());
            label.style.fontSize = 11;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = new Color(0.4f, 0.6f, 1f, 1f);
            label.style.letterSpacing = 1.2f;
            label.style.flexGrow = 1;
            
            var separator = new VisualElement();
            separator.style.height = 1;
            separator.style.backgroundColor = new Color(0.3f, 0.3f, 0.35f, 0.6f);
            separator.style.marginTop = 6;
            separator.style.marginLeft = 20;
            
            labelContainer.Add(icon);
            labelContainer.Add(label);
            
            header.Add(labelContainer);
            header.Add(separator);
            
            return header;
        }
        
        private VisualElement CreateTypeItem(TypeInfo typeInfo)
        {
            var item = new VisualElement();
            item.style.marginTop = 3;
            item.style.marginBottom = 3;
            item.style.paddingTop = 12;
            item.style.paddingBottom = 12;
            item.style.paddingLeft = 16;
            item.style.paddingRight = 16;
            item.style.backgroundColor = new Color(0.08f, 0.08f, 0.12f, 1f);
            item.style.borderTopLeftRadius = 8;
            item.style.borderTopRightRadius = 8;
            item.style.borderBottomLeftRadius = 8;
            item.style.borderBottomRightRadius = 8;
            item.style.borderTopWidth = 1;
            item.style.borderBottomWidth = 1;
            item.style.borderLeftWidth = 1;
            item.style.borderRightWidth = 1;
            item.style.borderTopColor = new Color(0.2f, 0.2f, 0.25f, 1f);
            item.style.borderBottomColor = new Color(0.15f, 0.15f, 0.18f, 1f);
            item.style.borderLeftColor = new Color(0.18f, 0.18f, 0.22f, 1f);
            item.style.borderRightColor = new Color(0.18f, 0.18f, 0.22f, 1f);
            item.style.cursor = StyleKeyword.Auto;
            
            var mainRow = new VisualElement();
            mainRow.style.flexDirection = FlexDirection.Row;
            mainRow.style.alignItems = Align.Center;
            
            // Type icon
            var typeIcon = new Label(GetTypeIcon(typeInfo.Type));
            typeIcon.style.fontSize = 16;
            typeIcon.style.marginRight = 12;
            typeIcon.style.color = GetTypeColor(typeInfo.Type);
            typeIcon.style.width = 20;
            typeIcon.style.unityTextAlign = TextAnchor.MiddleCenter;
            
            var textContainer = new VisualElement();
            textContainer.style.flexGrow = 1;
            
            var nameLabel = new Label(typeInfo.DisplayName);
            nameLabel.style.fontSize = 13;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.color = new Color(0.9f, 0.9f, 0.95f, 1f);
            nameLabel.style.marginBottom = 2;
            
            var descLabel = new Label(typeInfo.Description);
            descLabel.style.fontSize = 11;
            descLabel.style.color = new Color(0.6f, 0.6f, 0.7f, 1f);
            descLabel.style.whiteSpace = WhiteSpace.Normal;
            
            // Category badge
            var categoryBadge = new Label(typeInfo.Category);
            categoryBadge.style.fontSize = 9;
            categoryBadge.style.unityFontStyleAndWeight = FontStyle.Bold;
            categoryBadge.style.color = new Color(0.4f, 0.6f, 1f, 1f);
            categoryBadge.style.backgroundColor = new Color(0.4f, 0.6f, 1f, 0.15f);
            categoryBadge.style.paddingTop = 2;
            categoryBadge.style.paddingBottom = 2;
            categoryBadge.style.paddingLeft = 8;
            categoryBadge.style.paddingRight = 8;
            categoryBadge.style.borderTopLeftRadius = 10;
            categoryBadge.style.borderTopRightRadius = 10;
            categoryBadge.style.borderBottomLeftRadius = 10;
            categoryBadge.style.borderBottomRightRadius = 10;
            categoryBadge.style.letterSpacing = 0.5f;
            
            textContainer.Add(nameLabel);
            textContainer.Add(descLabel);
            
            mainRow.Add(typeIcon);
            mainRow.Add(textContainer);
            mainRow.Add(categoryBadge);
            
            item.Add(mainRow);
            
            // Professional hover effects
            item.RegisterCallback<MouseEnterEvent>(evt => {
                item.style.backgroundColor = new Color(0.12f, 0.14f, 0.18f, 1f);
                item.style.borderTopColor = new Color(0.4f, 0.6f, 1f, 0.8f);
                item.style.borderBottomColor = new Color(0.3f, 0.5f, 0.9f, 0.6f);
                item.style.borderLeftColor = new Color(0.35f, 0.55f, 0.95f, 0.7f);
                item.style.borderRightColor = new Color(0.35f, 0.55f, 0.95f, 0.7f);
                item.style.scale = new StyleScale(new Vector2(1.02f, 1.02f));
                item.style.borderTopWidth = 2;
                item.style.borderBottomWidth = 2;
                item.style.borderLeftWidth = 2;
                item.style.borderRightWidth = 2;
                
                // Animate type icon
                typeIcon.style.scale = new StyleScale(new Vector2(1.1f, 1.1f));
            });
            
            item.RegisterCallback<MouseLeaveEvent>(evt => {
                item.style.backgroundColor = new Color(0.08f, 0.08f, 0.12f, 1f);
                item.style.borderTopColor = new Color(0.2f, 0.2f, 0.25f, 1f);
                item.style.borderBottomColor = new Color(0.15f, 0.15f, 0.18f, 1f);
                item.style.borderLeftColor = new Color(0.18f, 0.18f, 0.22f, 1f);
                item.style.borderRightColor = new Color(0.18f, 0.18f, 0.22f, 1f);
                item.style.scale = new StyleScale(new Vector2(1f, 1f));
                item.style.borderTopWidth = 1;
                item.style.borderBottomWidth = 1;
                item.style.borderLeftWidth = 1;
                item.style.borderRightWidth = 1;
                
                typeIcon.style.scale = new StyleScale(new Vector2(1f, 1f));
            });
            
            item.RegisterCallback<MouseDownEvent>(evt => {
                if (evt.button == 0)
                {
                    item.style.scale = new StyleScale(new Vector2(0.98f, 0.98f));
                    schedule.Execute(() => {
                        _onTypeSelected?.Invoke(typeInfo.Type);
                        Hide();
                    }).ExecuteLater(50);
                    evt.StopPropagation();
                }
            });
            
            return item;
        }
        
        private void ShowAt(VisualElement parent, Vector2 position)
        {
            parent.Add(this);
            
            // Get parent bounds
            var parentRect = parent.layout;
            if (parentRect.width <= 0 || parentRect.height <= 0)
            {
                // Use parent's world bound if layout is not ready
                parentRect = parent.worldBound;
            }
            
            // Position panel - clamp to keep it on screen
            var x = Mathf.Clamp(position.x - PANEL_WIDTH / 2, 10, Mathf.Max(10, parentRect.width - PANEL_WIDTH - 10));
            var y = Mathf.Clamp(position.y - PANEL_HEIGHT / 2, 10, Mathf.Max(10, parentRect.height - PANEL_HEIGHT - 10));
            
            _panel.style.left = x;
            _panel.style.top = y;
            
            Debug.Log($"TypeSelectionPanel positioned at ({x}, {y}) in parent bounds ({parentRect.width}, {parentRect.height})");
            
            // Focus search field after a short delay
            schedule.Execute(() => {
                _searchField?.Focus();
                _searchField?.SelectAll();
            }).ExecuteLater(150);
            
            // Animate in
            AnimateIn();
        }
        
        private void AnimateIn()
        {
            var startTime = Time.realtimeSinceStartup;
            
            schedule.Execute(() => {
                var elapsed = Time.realtimeSinceStartup - startTime;
                var progress = Mathf.Clamp01(elapsed / ANIMATION_DURATION);
                var easedProgress = EaseOutCubic(progress);
                
                _overlay.style.opacity = easedProgress * 0.85f;
                _panel.style.opacity = easedProgress;
                _panel.style.scale = new StyleScale(Vector2.Lerp(
                    new Vector2(0.9f, 0.9f), 
                    new Vector2(1f, 1f), 
                    easedProgress));
            }).Every(16).Until(() => Time.realtimeSinceStartup - startTime >= ANIMATION_DURATION);
        }
        
        private void Hide()
        {
            var startTime = Time.realtimeSinceStartup;
            
            schedule.Execute(() => {
                var elapsed = Time.realtimeSinceStartup - startTime;
                var progress = Mathf.Clamp01(elapsed / ANIMATION_DURATION);
                var easedProgress = EaseInCubic(progress);
                
                _overlay.style.opacity = (1f - easedProgress) * 0.85f;
                _panel.style.opacity = 1f - easedProgress;
                _panel.style.scale = new StyleScale(Vector2.Lerp(
                    new Vector2(1f, 1f), 
                    new Vector2(0.9f, 0.9f), 
                    easedProgress));
                
                if (progress >= 1f)
                {
                    RemoveFromHierarchy();
                }
            }).Every(16).Until(() => Time.realtimeSinceStartup - startTime >= ANIMATION_DURATION);
        }
        
        private string GetDisplayName(Type type)
        {
            var name = type.Name;
            
            // Remove common suffixes
            if (name.EndsWith("Transition")) name = name.Substring(0, name.Length - 10);
            if (name.EndsWith("State")) name = name.Substring(0, name.Length - 5);
            if (name.EndsWith("Node")) name = name.Substring(0, name.Length - 4);
            
            // Add spaces before capital letters
            var result = "";
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]) && !char.IsUpper(name[i - 1]))
                {
                    result += " ";
                }
                result += name[i];
            }
            
            return result;
        }
        
        private string GetCategory(Type type)
        {
            if (typeof(BaseTransition).IsAssignableFrom(type))
                return "Transitions";
            if (typeof(ActionStateNode).IsAssignableFrom(type))
                return "States";
            
            return "Other";
        }
        
        private string GetDescription(Type type)
        {
            // You can expand this with actual descriptions or attributes
            if (type.Name.Contains("Timer"))
                return "Time-based transition condition";
            if (type.Name.Contains("Debug"))
                return "Debug and logging functionality";
            if (type.Name.Contains("Action"))
                return "General purpose action state";
            
            return $"{type.Name} component";
        }
        
        private string GetCategoryIcon(string category)
        {
            return category switch
            {
                "Transitions" => "⚡",
                "States" => "🔧",
                _ => "📦"
            };
        }
        
        private string GetTypeIcon(Type type)
        {
            if (typeof(BaseTransition).IsAssignableFrom(type))
            {
                if (type.Name.Contains("Timer"))
                    return "⏰";
                return "⚡";
            }
            
            if (typeof(ActionStateNode).IsAssignableFrom(type))
            {
                if (type.Name.Contains("Debug"))
                    return "🐛";
                if (type.Name.Contains("Entry"))
                    return "🚀";
                return "⚙️";
            }
            
            return "📦";
        }
        
        private Color GetTypeColor(Type type)
        {
            if (typeof(BaseTransition).IsAssignableFrom(type))
                return new Color(1f, 0.7f, 0.3f, 1f); // Orange for transitions
                
            if (typeof(ActionStateNode).IsAssignableFrom(type))
            {
                if (type.Name.Contains("Debug"))
                    return new Color(0.4f, 0.8f, 1f, 1f); // Blue for debug
                if (type.Name.Contains("Entry"))
                    return new Color(0.3f, 1f, 0.5f, 1f); // Green for entry
                return new Color(0.8f, 0.5f, 1f, 1f); // Purple for action states
            }
            
            return new Color(0.7f, 0.7f, 0.7f, 1f); // Gray for others
        }
        
        private float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
        private float EaseInCubic(float t) => t * t * t;
        
        private class TypeInfo
        {
            public Type Type;
            public string DisplayName;
            public string Category;
            public string Description;
        }
    }
}