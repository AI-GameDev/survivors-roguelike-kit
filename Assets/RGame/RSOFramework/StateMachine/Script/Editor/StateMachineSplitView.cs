#region

using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

#endregion

namespace RGame.ScriptableCoreKit
{
    public class StateMachineSplitView : TwoPaneSplitView
    {
        private bool _isInitialized;
        private float _lastSavedPosition = -1f;

        private string _positionKey = "StateMachineSplitView_Position";

        public StateMachineSplitView()
        {
            schedule.Execute(Initialize).StartingIn(100);
        }

        private void Initialize()
        {
            if (_isInitialized) return;
            _isInitialized = true;

            SetupPositionKey();
            RestorePosition();
            StartPositionMonitoring();
        }

        private void SetupPositionKey()
        {
            var parent = this.parent;
            while (parent != null)
            {
                if (parent.name == "StateMachineWindow" || parent.GetType().Name.Contains("StateMachine"))
                {
                    _positionKey = $"StateMachineSplitView_{parent.GetType().Name}_Position";
                    break;
                }

                parent = parent.parent;
            }
        }

        private void RestorePosition()
        {
            var savedPosition = EditorPrefs.GetFloat(_positionKey, -1f);

            if (savedPosition > 0)
            {
                fixedPaneInitialDimension = savedPosition;
                _lastSavedPosition = savedPosition;

                schedule.Execute(() => { MarkDirtyRepaint(); }).StartingIn(1);
            }
        }

        private void StartPositionMonitoring()
        {
            schedule.Execute(MonitorPosition).Every(200);
            RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            RegisterCallback<MouseUpEvent>(OnMouseUp);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
        }

        private void MonitorPosition()
        {
            var currentPosition = GetCurrentPosition();

            if (currentPosition > 0 && Mathf.Abs(currentPosition - _lastSavedPosition) > 5f) SavePosition(currentPosition);
        }

        private float GetCurrentPosition()
        {
            try
            {
                var type = typeof(TwoPaneSplitView);

                string[] fieldNames =
                {
                    "m_FixedPaneDimension",
                    "m_FixedPaneInitialDimension",
                    "fixedPaneDimension",
                    "m_SplitPosition",
                    "splitPosition"
                };

                foreach (var fieldName in fieldNames)
                {
                    var field = type.GetField(fieldName,
                        BindingFlags.NonPublic |
                        BindingFlags.Instance |
                        BindingFlags.Public);

                    if (field != null)
                    {
                        var value = field.GetValue(this);
                        if (value is float dimension && dimension > 0) return dimension;
                    }
                }

                var fixedPane = this.Q(null, "unity-two-pane-split-view__fixed-pane");
                if (fixedPane != null && fixedPane.layout.width > 0 && fixedPane.layout.height > 0)
                {
                    var rect = fixedPane.layout;
                    var dimension = orientation == TwoPaneSplitViewOrientation.Horizontal ? rect.width : rect.height;
                    if (dimension > 0 && dimension != fixedPaneInitialDimension) return dimension;
                }

                return fixedPaneInitialDimension;
            }
            catch (Exception)
            {
                return fixedPaneInitialDimension;
            }
        }

        private void SavePosition(float position)
        {
            if (position > 0 && position < 3000)
            {
                EditorPrefs.SetFloat(_positionKey, position);
                _lastSavedPosition = position;
            }
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            schedule.Execute(() =>
            {
                var currentPosition = GetCurrentPosition();
                if (currentPosition > 0) SavePosition(currentPosition);
            }).StartingIn(100);
        }

        private void OnMouseUp(MouseUpEvent evt)
        {
            schedule.Execute(() =>
            {
                var currentPosition = GetCurrentPosition();
                if (currentPosition > 0) SavePosition(currentPosition);
            }).StartingIn(50);
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            schedule.Execute(() =>
            {
                var currentPosition = GetCurrentPosition();
                if (currentPosition > 0) SavePosition(currentPosition);
            }).StartingIn(50);
        }

        public new class UxmlFactory : UxmlFactory<StateMachineSplitView, UxmlTraits>
        {
        }
    }
}