#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using RGame.RoguelikeKit.RGame.RoguelikeKit;
using RGame.Framework;

namespace RGame.RoguelikeKit
{
    /// <summary>
    /// Custom inspector for SpawnSet. Shows only the fields relevant to the selected pattern.
    /// Inherits DescriptionBaseSOEditor so the Scene reference foldout remains.
    /// </summary>
    [CustomEditor(typeof(SpawnSet))]
    public class SpawnSetEditor : DescriptionBaseSOEditor
    {
        // cache property references
        private SerializedProperty _entries;
        private SerializedProperty _pattern;
        private SerializedProperty _baseRate;
        private SerializedProperty _count;
        private SerializedProperty _radius;
        private SerializedProperty _start;
        private SerializedProperty _end;
        private SerializedProperty _curve;

        protected override void OnEnable()
        {
            base.OnEnable();
            _entries  = serializedObject.FindProperty("Entries");
            _pattern  = serializedObject.FindProperty("Pattern");
            _baseRate = serializedObject.FindProperty("BaseRatePerSecond");
            _count    = serializedObject.FindProperty("Count");
            _radius   = serializedObject.FindProperty("CircleRadius");
            _start    = serializedObject.FindProperty("StartTime");
            _end      = serializedObject.FindProperty("EndTime");
            _curve    = serializedObject.FindProperty("IntensityCurve");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // entries list always visible
            EditorGUILayout.PropertyField(_entries, includeChildren: true);
            EditorGUILayout.Space(4);

            EditorGUILayout.PropertyField(_pattern);
            SpawnPatternType pattern = (SpawnPatternType)_pattern.enumValueIndex;
            EditorGUILayout.Space(4);

            // switch on pattern
            switch (pattern)
            {
                case SpawnPatternType.Random:
                    EditorGUILayout.PropertyField(_baseRate);
                    EditorGUILayout.PropertyField(_start);
                    EditorGUILayout.PropertyField(_end);
                    EditorGUILayout.PropertyField(_curve);
                    break;
                case SpawnPatternType.Circle:
                    EditorGUILayout.PropertyField(_count);
                    EditorGUILayout.PropertyField(_radius);
                    EditorGUILayout.PropertyField(_start);
                    break;
                case SpawnPatternType.Dense:
                    EditorGUILayout.PropertyField(_count);
                    EditorGUILayout.PropertyField(_start);
                    break;
            }

            serializedObject.ApplyModifiedProperties();

            // Draw scene references fold‑out from base class
            DrawSceneReferences();
        }
    }
}
#endif
