using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace RGame.ScriptableCoreKit
{
    public class BlackboardRenderer
    {
        private BlackboardTable _table;
        private SerializedObject _serializedObject;
        private SerializedProperty _slotsProp;
        private ReorderableList _slotList;
        
        // GUI styles cache
        private GUIStyle _headerStyle;
        private GUIStyle _keyLabelStyle;
        private GUIStyle _valueLabelStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _emptyStateStyle;
        private bool _stylesInitialized = false;
        
        // Colors
        private static readonly Color KeyColor = new Color(0.8f, 0.4f, 0.4f, 1f);      // Soft red
        private static readonly Color ValueColor = new Color(0.4f, 0.6f, 0.8f, 1f);    // Soft blue
        private static readonly Color SeparatorColor = new Color(1f, 0.9f, 0.3f, 0.8f); // Yellow
        
        // Configuration
        public bool AllowDragging { get; set; } = true;
        public bool AllowRemoving { get; set; } = true;
        public bool AllowAdding { get; set; } = true;
        public bool ReadOnlyKeys { get; set; } = false;
        
        public BlackboardRenderer()
        {
            
        }
        
        public void SetTarget(BlackboardTable table)
        {
            if (_table == table) return;
            
            // Dispose previous serialized object
            if (_serializedObject != null)
            {
                _serializedObject.Dispose();
            }
            
            _table = table;
            
            if (_table != null)
            {
                _serializedObject = new SerializedObject(_table);
                _slotsProp = _serializedObject.FindProperty("slots");
                SetupReorderableList();
            }
            else
            {
                _serializedObject = null;
                _slotsProp = null;
                _slotList = null;
            }
        }
        
        // Method to update configuration after SetTarget
        public void UpdateConfiguration(bool allowDragging, bool allowRemoving, bool allowAdding, bool readOnlyKeys)
        {
            AllowDragging = allowDragging;
            AllowRemoving = allowRemoving;
            AllowAdding = allowAdding;
            ReadOnlyKeys = readOnlyKeys;
            
            // Refresh the list with new configuration
            if (_table != null)
            {
                SetupReorderableList();
            }
        }
        
        public void Dispose()
        {
            if (_serializedObject != null)
            {
                _serializedObject.Dispose();
                _serializedObject = null;
            }
        }

        private void SetupReorderableList()
        {
            if (_slotsProp == null) return;
            
            _slotList = new ReorderableList(
                _serializedObject,
                _slotsProp,
                draggable: AllowDragging,
                displayHeader: true,
                displayAddButton: AllowAdding,
                displayRemoveButton: AllowRemoving
            );

            _slotList.elementHeightCallback = GetElementHeight;
            _slotList.drawHeaderCallback = DrawListHeader;
            _slotList.drawElementCallback = DrawSlotElement;
            _slotList.onRemoveCallback = OnRemoveSlot;
            _slotList.headerHeight = 25f;
        }

        private void InitializeStyles()
        {
            if (_stylesInitialized) return;

            // Header style
            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(4, 4, 4, 4)
            };

            // Key label style
            _keyLabelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = KeyColor }
            };

            // Value label style  
            _valueLabelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = ValueColor }
            };

            // Button style
            _buttonStyle = new GUIStyle(EditorStyles.miniButton)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                fixedHeight = 28
            };

            // Empty state style
            _emptyStateStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                fontSize = 12,
                fontStyle = FontStyle.Italic,
                alignment = TextAnchor.MiddleCenter
            };

            _stylesInitialized = true;
        }
        
        public void DrawBlackboardList()
        {
            if (_table == null)
            {
                DrawNoBlackboardState();
                return;
            }
            
            InitializeStyles();
            _serializedObject?.Update();
            
            if (_slotsProp == null || _slotsProp.arraySize == 0)
            {
                DrawEmptyState();
                return;
            }

            // Draw the reorderable list
            _slotList?.DoLayoutList();
            
            _serializedObject?.ApplyModifiedProperties();
        }
        
        public void DrawBlackboardHeader(string title = "Blackboard Variables", bool showEditButton = true)
        {
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.Label($"{title} ({(_slotsProp?.arraySize ?? 0)})", EditorStylesUtil.HeaderStyle());
                    
                    GUILayout.FlexibleSpace();
                    
                    if (showEditButton && _table != null)
                    {
                        if (GUILayout.Button("Edit", EditorStyles.miniButton, GUILayout.Width(40)))
                        {
                            BlackboardWindow.OpenAndSet(_table);
                        }
                    }
                }
                
                if (_slotsProp != null && _slotsProp.arraySize > 0)
                {
                    GUILayout.Space(4);
                    GUILayout.Label($"Manage key-value pairs for this blackboard table", EditorStyles.miniLabel);
                }
            }
        }
        
        public void DrawActionButtons()
        {
            if (_table == null) return;
            
            InitializeStyles();
            
            using (new GUILayout.HorizontalScope())
            {
                // Open edit window button
                if (GUILayout.Button("Open Edit Window", _buttonStyle))
                {
                    BlackboardWindow.OpenAndSet(_table);
                }
                
                GUILayout.Space(8);
                
                // Clear all button (with confirmation)
                GUI.enabled = _slotsProp != null && _slotsProp.arraySize > 0;
                if (GUILayout.Button("Clear All", _buttonStyle, GUILayout.Width(80)))
                {
                    if (EditorUtility.DisplayDialog(
                        "Clear All Slots", 
                        $"Are you sure you want to remove all {_slotsProp.arraySize} slots?", 
                        "Clear", 
                        "Cancel"))
                    {
                        _slotsProp.ClearArray();
                        _serializedObject.ApplyModifiedProperties();
                    }
                }
                GUI.enabled = true;
            }
        }

        private void DrawNoBlackboardState()
        {
            InitializeStyles();
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Space(20);
                GUILayout.Label("No Blackboard Assigned", _emptyStateStyle);
                GUILayout.Space(8);
                GUILayout.Label("Assign a BlackboardTable to see variables", 
                               EditorStyles.centeredGreyMiniLabel);
                GUILayout.Space(20);
            }
        }

        private void DrawEmptyState()
        {
            InitializeStyles();
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Space(20);
                GUILayout.Label("No slots defined", _emptyStateStyle);
                GUILayout.Space(8);
                GUILayout.Label("Use the 'Open Edit Window' button to add variables", EditorStyles.centeredGreyMiniLabel);
                GUILayout.Space(20);
            }
        }

        private void DrawListHeader(Rect rect)
        {
            float halfWidth = rect.width * 0.5f;
            float separatorWidth = 20f;
            
            // Key header
            Rect keyRect = new Rect(rect.x, rect.y, halfWidth - separatorWidth * 0.5f, rect.height);
            GUI.Label(keyRect, "Key", _keyLabelStyle);
            
            // Separator
            Rect sepRect = new Rect(rect.x + halfWidth - 1, rect.y + 4, 2, rect.height - 8);
            EditorGUI.DrawRect(sepRect, SeparatorColor);
            
            // Value header  
            Rect valueRect = new Rect(rect.x + halfWidth + separatorWidth * 0.5f, rect.y, 
                                     halfWidth - separatorWidth * 0.5f - 20, rect.height); // -20 for remove button
            GUI.Label(valueRect, "Value", _valueLabelStyle);
        }

        private void DrawSlotElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            if (_table == null || _slotsProp == null) return;

            var element = _slotsProp.GetArrayElementAtIndex(index);
            var keyProp = element.FindPropertyRelative("key");
            var valueProp = element.FindPropertyRelative("value");

            if (keyProp == null) return;

            // Add some padding
            rect.y += 2;
            rect.height = EditorGUIUtility.singleLineHeight;
            
            float halfWidth = rect.width * 0.5f;
            float separatorWidth = 20f;
            
            // Key field
            Rect keyRect = new Rect(rect.x, rect.y, halfWidth - separatorWidth * 0.5f - 13, rect.height);
            
            // Highlight selected element
            if (isActive)
            {
                Rect highlightRect = new Rect(rect.x - 2, rect.y - 1, rect.width + 4, rect.height + 2);
                EditorGUI.DrawRect(highlightRect, new Color(0.3f, 0.5f, 0.8f, 0.3f));
            }
            
            // Key field (configurable as read-only)
            if (ReadOnlyKeys)
            {
                EditorGUI.BeginDisabledGroup(true);
                EditorGUI.PropertyField(keyRect, keyProp, GUIContent.none);
                EditorGUI.EndDisabledGroup();
            }
            else
            {
                EditorGUI.PropertyField(keyRect, keyProp, GUIContent.none);
            }
            
            // Separator line
            Rect sepRect = new Rect(rect.x + halfWidth - 8, rect.y, 2, rect.height);
            EditorGUI.DrawRect(sepRect, SeparatorColor);
            
            // Value field
            Rect valueRect = new Rect(rect.x + halfWidth + separatorWidth * 0.5f, rect.y, 
                halfWidth - separatorWidth * 0.5f - 24, rect.height);

            if (valueProp != null)
            {
                float propertyHeight = EditorGUI.GetPropertyHeight(valueProp, true);
                valueRect.height = propertyHeight;
    
                EditorGUI.PropertyField(valueRect, valueProp, GUIContent.none, true);
            }
            else
            {
                EditorGUI.LabelField(valueRect, "(no value field)", EditorStyles.centeredGreyMiniLabel);
            }
        }

        private float GetElementHeight(int index)
        {
            if (_slotsProp == null || index >= _slotsProp.arraySize)
                return EditorGUIUtility.singleLineHeight + 8;
                
            var element = _slotsProp.GetArrayElementAtIndex(index);
            var valueProp = element.FindPropertyRelative("value");
    
            if (valueProp != null)
            {
                if (IsMultiLineProperty(valueProp))
                {
                    return EditorGUI.GetPropertyHeight(valueProp) + 8; // Extra padding
                }
            }
    
            return EditorGUIUtility.singleLineHeight + 8;
        }

        private bool IsMultiLineProperty(SerializedProperty prop)
        {
            return prop.propertyType == SerializedPropertyType.Rect ||
                   prop.propertyType == SerializedPropertyType.Vector4 ||
                   prop.propertyType == SerializedPropertyType.Bounds;
        }
        
        private void OnRemoveSlot(ReorderableList list)
        {
            if (list.index >= 0 && list.index < _slotsProp.arraySize)
            {
                var element = _slotsProp.GetArrayElementAtIndex(list.index);
                var keyProp = element.FindPropertyRelative("key");
                string keyName = keyProp?.stringValue ?? "Unknown";
              
                ReorderableList.defaultBehaviours.DoRemoveButton(list);
            }
        }
        
        public bool HasSlots()
        {
            return _slotsProp != null && _slotsProp.arraySize > 0;
        }
        
        public int GetSlotsCount()
        {
            return _slotsProp?.arraySize ?? 0;
        }
    }
}