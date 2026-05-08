using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RGame.ScriptableCoreKit
{
    /// <summary>
    /// Professional dockable editor window for FlexBlackboard with enhanced styling and dynamic type registration
    /// </summary>
    public class BlackboardWindow : EditorWindow
    {
        [SerializeField] private BlackboardTable table;
        [SerializeField] private int selectedTypeIndex = -1;
        [SerializeField] private string searchFilter = "";
        
        // Dynamic type registration fields
        [SerializeField] private string customTypeName = "";
        [SerializeField] private bool showRegistrationPanel = false;
        
        private Vector2 _typeScroll;
        private string[] _typeNames;
        private string[] _filteredTypeNames;
        private GUIStyle _windowStyle;
        private bool _stylesInitialized = false;

        private void OnEnable()
        {
            TypeRegistry.EnsureInitialized();
            RefreshTypeList();
            
            // Set window properties
            titleContent = new GUIContent("Blackboard Types", EditorGUIUtility.IconContent("d_ScriptableObject Icon").image);
            minSize = new Vector2(280, 350);
        }

        private void OnDisable()
        {
            // Cleanup when window closes
            EditorStylesUtil.CleanupCache();
        }

        private void RefreshTypeList()
        {
            _typeNames = TypeRegistry.Registered.Keys.OrderBy(name => name).ToArray();
            ApplySearchFilter();
        }

        private void ApplySearchFilter()
        {
            if (string.IsNullOrWhiteSpace(searchFilter))
            {
                _filteredTypeNames = _typeNames;
            }
            else
            {
                _filteredTypeNames = _typeNames
                    .Where(name => name.ToLower().Contains(searchFilter.ToLower()))
                    .ToArray();
            }
        }

        private void InitializeStyles()
        {
            if (_stylesInitialized) return;

            _windowStyle = new GUIStyle()
            {
                padding = EditorStylesUtil.Spacing.WINDOW_PADDING
            };
            
            _stylesInitialized = true;
        }

        private void OnGUI()
        {
            if (table == null)
            {
                DrawEmptyState();
                return;
            }

            InitializeStyles();
            
            using (new GUILayout.VerticalScope(_windowStyle))
            {
                DrawHeader();
                DrawSearchBar();
                GUILayout.Space(EditorStylesUtil.Spacing.ITEM_SPACING);
                
                DrawRegistrationPanel();
                
                DrawTypeList();
            }
        }

        private void DrawEmptyState()
        {
            using (new GUILayout.VerticalScope())
            {
                GUILayout.FlexibleSpace();
                
                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    
                    using (new GUILayout.VerticalScope())
                    {
                        var centeredStyle = new GUIStyle(EditorStyles.label)
                        {
                            alignment = TextAnchor.MiddleCenter,
                            fontSize = 14,
                            fontStyle = FontStyle.Italic
                        };
                        
                        GUILayout.Label("No Blackboard Table Assigned", centeredStyle);
                        GUILayout.Space(8);
                        GUILayout.Label("Select a BlackboardTable asset to begin", EditorStyles.centeredGreyMiniLabel);
                    }
                    
                    GUILayout.FlexibleSpace();
                }
                
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawHeader()
        {
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label("Available Types", EditorStylesUtil.HeaderStyle());
                
                GUILayout.FlexibleSpace();
                
                // Toggle registration panel button
                var toggleButtonStyle = new GUIStyle(EditorStyles.miniButton)
                {
                    fontSize = 10
                };
                
                if (GUILayout.Button(showRegistrationPanel ? "Hide +" : "Add +", toggleButtonStyle, GUILayout.Width(50)))
                {
                    showRegistrationPanel = !showRegistrationPanel;
                }
                
                GUILayout.Space(4);
                
                // Info label showing count
                var countText = _filteredTypeNames != null ? 
                    $"{_filteredTypeNames.Length} types" : 
                    "Loading...";
                    
                GUILayout.Label(countText, EditorStyles.miniLabel);
            }
            
            // Draw separator line
            GUILayout.Space(2);
            var rect = GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, EditorStylesUtil.IsDarkTheme() ? 
                new Color(0.3f, 0.3f, 0.3f) : 
                new Color(0.7f, 0.7f, 0.7f));
            GUILayout.Space(EditorStylesUtil.Spacing.ITEM_SPACING);
        }

        private void DrawRegistrationPanel()
        {
            if (!showRegistrationPanel) return;
            
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.Label("Register New Type", EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    
                    if (GUILayout.Button("?", EditorStyles.miniButton, GUILayout.Width(20)))
                    {
                        ShowRegistrationHelp();
                    }
                }
                
                GUILayout.Space(4);
                
                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.Label("Type Name:", GUILayout.Width(70));
                    
                    EditorGUI.BeginChangeCheck();
                    customTypeName = EditorGUILayout.TextField(customTypeName);
                    
                    GUI.enabled = !string.IsNullOrWhiteSpace(customTypeName);
                    if (GUILayout.Button("Register", EditorStyles.miniButton, GUILayout.Width(60)))
                    {
                        RegisterCustomType();
                    }
                    GUI.enabled = true;
                }
                
                GUILayout.Space(2);
                DrawQuickRegistrationButtons();
                
                using (new GUILayout.HorizontalScope())
                {
                    var hintStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        fontStyle = FontStyle.Italic,
                        wordWrap = true
                    };
                    
                    GUILayout.Label("💡 Tip: Enter class name (e.g., 'Rigidbody2D') or full name (e.g., 'UnityEngine.Rigidbody2D')", 
                                   hintStyle);
                }
            }
            
            GUILayout.Space(EditorStylesUtil.Spacing.ITEM_SPACING);
        }

        private void DrawQuickRegistrationButtons()
        {
            GUILayout.Label("Quick Add:", EditorStyles.miniLabel);
            
            using (new GUILayout.HorizontalScope())
            {
                var quickTypes = new[]
                {
                    "Rigidbody2D",
                    "Collider2D", 
                    "SpriteRenderer",
                    "Animator",
                    "ParticleSystem"
                };
                
                var buttonStyle = new GUIStyle(EditorStyles.miniButton)
                {
                    fontSize = 9
                };
                
                foreach (var typeName in quickTypes)
                {
                    if (GUILayout.Button(typeName, buttonStyle))
                    {
                        customTypeName = typeName;
                        RegisterCustomType();
                    }
                }
                
                GUILayout.FlexibleSpace();
            }
        }

        private void RegisterCustomType()
        {
            if (string.IsNullOrWhiteSpace(customTypeName))
            {
                EditorUtility.DisplayDialog("Invalid Input", "Please enter a valid type name.", "OK");
                return;
            }
            
            bool success = TypeRegistry.RegisterType(customTypeName.Trim());
            
            if (success)
            {
                RefreshTypeList();
                
                customTypeName = "";
                
                Debug.Log($"Successfully registered type: {customTypeName}");
                
                EditorUtility.DisplayDialog("Success", 
                    $"Type '{customTypeName}' has been registered successfully!", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Registration Failed", 
                    $"Failed to register type '{customTypeName}'.\n\nPlease check:\n" +
                    "• Type name is correct\n" +
                    "• Type exists in loaded assemblies\n" +
                    "• Type is serializable\n\n" +
                    "Check Console for detailed error information.", "OK");
            }
        }

        private void ShowRegistrationHelp()
        {
            var helpMessage = "Dynamic Type Registration Help:\n\n" +
                "• Enter the class name (e.g., 'Rigidbody2D')\n" +
                "• Or full type name (e.g., 'UnityEngine.Rigidbody2D')\n" +
                "• Use Quick Add buttons for common types\n" +
                "• Only serializable types can be registered\n\n" +
                "Supported type examples:\n" +
                "• Unity Components: Rigidbody2D, Collider2D\n" +
                "• Unity Objects: Texture2D, AudioClip\n" +
                "• Custom Classes: YourCustomClass\n" +
                "• System Types: DateTime, TimeSpan\n\n" +
                "Note: Generic types are not supported.";
            
            EditorUtility.DisplayDialog("Type Registration Help", helpMessage, "Got it!");
        }

        private void DrawSearchBar()
        {
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label("Search:", GUILayout.Width(50));
                
                EditorGUI.BeginChangeCheck();
                searchFilter = EditorGUILayout.TextField(searchFilter, EditorStylesUtil.SearchField);
                
                if (EditorGUI.EndChangeCheck())
                {
                    ApplySearchFilter();
                    selectedTypeIndex = -1; // Clear selection when searching
                }
                
                // Clear button
                if (GUILayout.Button("×", EditorStyles.miniButton, GUILayout.Width(20)))
                {
                    searchFilter = "";
                    ApplySearchFilter();
                    selectedTypeIndex = -1;
                    GUI.FocusControl(null); // Remove focus from search field
                }
            }
        }

        private void DrawTypeList()
        {
            if (_filteredTypeNames == null || _filteredTypeNames.Length == 0)
            {
                GUILayout.Label(
                    string.IsNullOrWhiteSpace(searchFilter) ? 
                        "No types available" : 
                        "No types match search filter", 
                    EditorStyles.centeredGreyMiniLabel);
                return;
            }

            _typeScroll = EditorGUILayout.BeginScrollView(
                _typeScroll,
                false, // horizontal scrollbar
                true,  // vertical scrollbar
                GUILayout.ExpandHeight(true));

            for (int i = 0; i < _filteredTypeNames.Length; i++)
            {
                DrawTypeButton(i, _filteredTypeNames[i]);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawTypeButton(int index, string typeName)
        {
            // Determine if this button is selected
            bool isSelected = (selectedTypeIndex == index);
            
            // Choose appropriate style
            var buttonStyle = isSelected ? 
                EditorStylesUtil.SelectedButtonStyle() : 
                EditorStylesUtil.NormalButtonStyle();

            // Create button with full width
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button(typeName, buttonStyle, GUILayout.ExpandWidth(true), GUILayout.Height(28)))
                {
                    OnTypeButtonClicked(index, typeName);
                }
                
                if (Event.current.type == EventType.ContextClick)
                {
                    var rect = GUILayoutUtility.GetLastRect();
                    if (rect.Contains(Event.current.mousePosition))
                    {
                        ShowTypeContextMenu(typeName);
                        Event.current.Use();
                    }
                }
            }

            // Add subtle spacing between buttons
            GUILayout.Space(2);
        }

        private void ShowTypeContextMenu(string typeName)
        {
            GenericMenu menu = new GenericMenu();
            
            menu.AddItem(new GUIContent($"Add '{typeName}' to Blackboard"), false, () =>
            {
                if (TypeRegistry.Registered.TryGetValue(typeName, out var type))
                {
                    table.AddSlot(typeName, type);
                    EditorUtility.SetDirty(table);
                }
            });
            
            menu.AddSeparator("");
            
            menu.AddItem(new GUIContent("Copy Type Name"), false, () =>
            {
                EditorGUIUtility.systemCopyBuffer = typeName;
            });
            
            menu.AddItem(new GUIContent("Show in Console"), false, () =>
            {
                if (TypeRegistry.Registered.TryGetValue(typeName, out var type))
                {
                    Debug.Log($"Type Info: {typeName} -> {type.FullName}");
                }
            });
            
            menu.ShowAsContext();
        }

        private void OnTypeButtonClicked(int index, string displayName)
        {
            // Update selection
            selectedTypeIndex = index;
            
            // Add slot to blackboard table
            if (TypeRegistry.Registered.TryGetValue(displayName, out var type))
            {
                table.AddSlot(displayName, type);
                Debug.Log($"Added type '{displayName}' to blackboard");
                
                // Mark the table as dirty for saving
                EditorUtility.SetDirty(table);
            }
            else
            {
                Debug.LogError($"Type '{displayName}' not found in registry");
            }
            
            // Repaint to update selection visuals
            Repaint();
        }

        /// <summary>
        /// Opens the BlackboardWindow and assigns a table
        /// </summary>
        public static void OpenAndSet(BlackboardTable tableAsset)
        {
            var window = GetWindow<BlackboardWindow>("Blackboard Types");
            window.table = tableAsset;
            window.selectedTypeIndex = -1; // Clear selection
            window.searchFilter = ""; // Clear search
            window.customTypeName = ""; // Clear custom type input
            window.RefreshTypeList();
            window.Show();
            window.Focus();
        }
        
        public static void RefreshTypes()
        {
            TypeRegistry.EnsureInitialized();
            
            var windows = Resources.FindObjectsOfTypeAll<BlackboardWindow>();
            foreach (var window in windows)
            {
                window.RefreshTypeList();
                window.Repaint();
            }
            
            Debug.Log("Blackboard types refreshed");
        }

        /// <summary>
        /// Opens registration panel directly
        /// </summary>
        public static void OpenRegistrationPanel()
        {
            var window = GetWindow<BlackboardWindow>("Blackboard Types");
            window.showRegistrationPanel = true;
            window.Show();
            window.Focus();
        }

        /// <summary>
        /// Handles keyboard shortcuts
        /// </summary>
        private void OnInspectorUpdate()
        {
            // Handle keyboard navigation
            if (Event.current != null && Event.current.type == EventType.KeyDown)
            {
                switch (Event.current.keyCode)
                {
                    case KeyCode.UpArrow:
                        NavigateSelection(-1);
                        Event.current.Use();
                        break;
                        
                    case KeyCode.DownArrow:
                        NavigateSelection(1);
                        Event.current.Use();
                        break;
                        
                    case KeyCode.Return:
                    case KeyCode.KeypadEnter:
                        if (selectedTypeIndex >= 0 && selectedTypeIndex < _filteredTypeNames.Length)
                        {
                            OnTypeButtonClicked(selectedTypeIndex, _filteredTypeNames[selectedTypeIndex]);
                        }
                        Event.current.Use();
                        break;
                        
                    case KeyCode.F2:
                        showRegistrationPanel = !showRegistrationPanel;
                        Repaint();
                        Event.current.Use();
                        break;
                }
            }
        }

        private void NavigateSelection(int direction)
        {
            if (_filteredTypeNames == null || _filteredTypeNames.Length == 0)
                return;

            selectedTypeIndex = Mathf.Clamp(selectedTypeIndex + direction, 0, _filteredTypeNames.Length - 1);
            Repaint();
        }
    }
}