#region

using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

#endregion

namespace RGame.ScriptableCoreKit
{
    public class StateMachineWindow : EditorWindow
    {
        private StateMachineGraphContainer _graphContainer;
        private StateMachineInspectorView _inspectorView;
        private TwoPaneSplitView _splitView;
        
        private const string SPLIT_VIEW_POSITION_KEY = "StateMachineWindow_SplitPosition";
        private const float DEFAULT_SPLIT_POSITION = 300f;
        private const float MIN_SPLIT_POSITION = 1f; 
        private const float MAX_SPLIT_POSITION = 9999f;

        public void CreateGUI()
        {
            var root = rootVisualElement;
            root.Clear();

            // Create split view layout with proper draggable separator
            CreateSplitViewLayout(root);
            
            // Setup connections between graph and inspector
            if (_graphContainer != null && _inspectorView != null)
            {
                _graphContainer.SetInspectorView(_inspectorView);
                _graphContainer.OnStateSelected = OnStateSelectionChanged;
                OnSelectionChange();
            }
        }

        private void CreateSplitViewLayout(VisualElement root)
        {
            // Load saved split position and validate it
            var savedPosition = EditorPrefs.GetFloat(SPLIT_VIEW_POSITION_KEY, DEFAULT_SPLIT_POSITION);
            var validPosition = GetValidSplitPosition(savedPosition);
            
            // Use TwoPaneSplitView for draggable separator
            _splitView = new TwoPaneSplitView(0, validPosition, TwoPaneSplitViewOrientation.Horizontal);
            _splitView.style.flexGrow = 1;

            // Create left panel for graph
            var leftPanel = new VisualElement();
            leftPanel.style.flexGrow = 1;
            leftPanel.style.backgroundColor = EditorGUIUtility.isProSkin ? new Color(0.22f, 0.22f, 0.22f) : new Color(0.78f, 0.78f, 0.78f);

            // Create graph container
            _graphContainer = new StateMachineGraphContainer();
            _graphContainer.style.flexGrow = 1;
            
            leftPanel.Add(_graphContainer);

            // Create right panel for inspector
            var rightPanel = new VisualElement();
            rightPanel.style.flexGrow = 1;
            rightPanel.style.backgroundColor = EditorGUIUtility.isProSkin ? new Color(0.25f, 0.25f, 0.25f) : new Color(0.75f, 0.75f, 0.75f);

            // Create inspector view
            _inspectorView = new StateMachineInspectorView();
            _inspectorView.style.flexGrow = 1;
            rightPanel.Add(_inspectorView);

            // Add panels to split view
            _splitView.Add(leftPanel);
            _splitView.Add(rightPanel);

            // Setup split view position change callback
            SetupSplitViewCallbacks();

            // Add split view to root
            root.Add(_splitView);
        }

        private void SetupSplitViewCallbacks()
        {
            // Save split position when drag ends
            _splitView.RegisterCallback<PointerUpEvent>(OnSplitViewDragEnd);
            _splitView.RegisterCallback<MouseUpEvent>(OnSplitViewDragEnd);
            
            // Also monitor geometry changes with delay
            _splitView.RegisterCallback<GeometryChangedEvent>(OnSplitViewGeometryChanged);
            
            // Monitor the actual splitter element
            rootVisualElement.schedule.Execute(() =>
            {
                var splitter = _splitView.Q(null, "unity-two-pane-split-view__dragline-anchor");
                if (splitter != null)
                {
                    splitter.RegisterCallback<PointerUpEvent>(OnSplitViewDragEnd);
                    splitter.RegisterCallback<MouseUpEvent>(OnSplitViewDragEnd);
                }
            }).ExecuteLater(100);
        }

        private void OnSplitViewDragEnd<T>(T evt) where T : EventBase
        {
            // Delay to ensure layout is complete
            rootVisualElement.schedule.Execute(SaveSplitPosition).ExecuteLater(50);
        }

        private void OnSplitViewGeometryChanged(GeometryChangedEvent evt)
        {
            // Delay saving to avoid saving during layout calculations
            rootVisualElement.schedule.Execute(SaveSplitPosition).ExecuteLater(100);
        }

        private void SaveSplitPosition()
        {
            if (_splitView != null)
            {
                // Try to get the actual current split position
                float currentPosition = -1f;
                
                try
                {
                    // Method 1: Try to get fixedPaneDimension (runtime value)
                    var fixedPane = _splitView.Q(null, "unity-two-pane-split-view__fixed-pane");
                    if (fixedPane != null && fixedPane.layout.width > 0)
                    {
                        currentPosition = fixedPane.layout.width;
                    }
                    else
                    {
                        // Method 2: Fallback to reflection to get internal dimension
                        var field = typeof(TwoPaneSplitView).GetField("m_FixedPaneDimension", 
                            BindingFlags.NonPublic | BindingFlags.Instance);
                        if (field != null)
                        {
                            var value = field.GetValue(_splitView);
                            if (value is float dimension && dimension > 0)
                            {
                                currentPosition = dimension;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Failed to get split position: {e.Message}");
                }
                
                // Validate the position before saving
                if (IsValidSplitPosition(currentPosition))
                {
                    EditorPrefs.SetFloat(SPLIT_VIEW_POSITION_KEY, currentPosition);
                }
                else
                {
                  
                }
            }
        }

        private bool IsValidSplitPosition(float position)
        {
            return position > 0f && 
                   position >= MIN_SPLIT_POSITION && 
                   position <= MAX_SPLIT_POSITION && 
                   !float.IsNaN(position) &&
                   !float.IsInfinity(position);
        }

        private float GetValidSplitPosition(float savedPosition)
        {
            // If saved position is invalid, return default
            if (!IsValidSplitPosition(savedPosition))
            {
                Debug.LogWarning($"Invalid saved split position: {savedPosition}, using default: {DEFAULT_SPLIT_POSITION}");
                return DEFAULT_SPLIT_POSITION;
            }
            
            return savedPosition;
        }

        private void OnSelectionChange()
        {
            _graphContainer?.LoadStateMachine();
        }
        
        public static void OpenWindow()
        {
            var wnd = GetWindow<StateMachineWindow>();
            wnd.titleContent = new GUIContent("State Machine Editor");
            wnd.minSize = new Vector2(800, 600);
        }
        
        public static void ResetLayout()
        {
            EditorPrefs.DeleteKey(SPLIT_VIEW_POSITION_KEY);
            Debug.Log("State Machine Editor layout reset to default");
            
            // If window is open, recreate it
            if (HasOpenInstances<StateMachineWindow>())
            {
                var window = GetWindow<StateMachineWindow>();
                window.Close();
                OpenWindow();
            }
        }

        private void OnStateSelectionChanged(StateNodeView stateView)
        {
            _inspectorView?.UpdateSelection(stateView);
        }

        private void OnDestroy()
        {
            // Save split position when window is closed
            SaveSplitPosition();
        }
    }
}