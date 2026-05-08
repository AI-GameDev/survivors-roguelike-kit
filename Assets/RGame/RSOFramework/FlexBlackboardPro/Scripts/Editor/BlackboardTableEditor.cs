#region

using System.Collections.Generic;
using RGame.Framework;
using UnityEditor;
using UnityEditor.Localization.Plugins.XLIFF.V12;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;

#endregion

namespace RGame.ScriptableCoreKit
{
    [CustomEditor(typeof(BlackboardTable))]
    public class BlackboardTableEditor : DescriptionBaseSOEditor
    {
        private BlackboardTable Table => (BlackboardTable)target;
        private BlackboardRenderer _blackboardRenderer;
        
        protected override void OnEnable()
        {
            base.OnEnable();
            
            // Initialize BlackboardRenderer
            _blackboardRenderer = new BlackboardRenderer();
            _blackboardRenderer.AllowDragging = true;
            _blackboardRenderer.AllowRemoving = true;
            _blackboardRenderer.AllowAdding = false; // We have custom buttons
            _blackboardRenderer.ReadOnlyKeys = false;
            
            _blackboardRenderer.SetTarget(Table);
        }
        
        private void OnDisable()
        {
            _blackboardRenderer?.Dispose();
        }
        
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            DrawInspectorHeader();
            GUILayout.Space(EditorStylesUtil.Spacing.SECTION_SPACING);
            
            DrawSlotsList();
            GUILayout.Space(EditorStylesUtil.Spacing.ITEM_SPACING);
            
            DrawActionButtons();
            
            serializedObject.ApplyModifiedProperties();
            base.DrawSceneReferences();
        }
        
        private void DrawInspectorHeader()
        {
            _blackboardRenderer?.DrawBlackboardHeader("Blackboard Slots", false);
        }

        private void DrawSlotsList()
        {
            _blackboardRenderer?.DrawBlackboardList();
        }

        private void DrawActionButtons()
        {
            _blackboardRenderer?.DrawActionButtons();
        }
    }
}