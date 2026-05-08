using RGame.Framework;
using UnityEditor;
using UnityEngine;

namespace RGame.CommonStat
{
    [CustomEditor(typeof(CommonStatRuntimeSO))]
    public class CommonStatRuntimeSOEditor : DescriptionBaseSOEditor
    {
        private bool showRuntimeValues = true;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            
            CommonStatRuntimeSO runtimeSO = (CommonStatRuntimeSO)target;
            EditorGUILayout.Space();
            showRuntimeValues = EditorGUILayout.Foldout(showRuntimeValues, $"Runtime Values ({runtimeSO.RuntimeValues.Count})");
            if (showRuntimeValues)
            {
                EditorGUI.indentLevel++;
                foreach (var kvp in runtimeSO.RuntimeValues)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(kvp.Key, GUILayout.Width(100));
                    EditorGUILayout.LabelField("Current: " + kvp.Value.GetCurrentValue(), GUILayout.Width(100));
                    EditorGUILayout.LabelField("Max: " + kvp.Value.GetMaxValue());
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUI.indentLevel--;
            }

            DrawSceneReferences();
        }
    }
}
