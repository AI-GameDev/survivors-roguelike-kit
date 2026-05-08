#region

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

#endregion

namespace RGame.Framework
{
    [CustomEditor(typeof(DescriptionBaseSO), true)]
    public class DescriptionBaseSOEditor : Editor
    {
        private const double updateInterval = 1.0;
        private double lastUpdateTime;
        private readonly List<GameObject> references = new();

        protected virtual void OnEnable()
        {
            FindReferencesInScene();
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            DrawSceneReferences();
        }

        protected void DrawSceneReferences()
        {
            EditorGUILayout.Space(10);

            if (references.Count > 0)
            {
                var showReferences = EditorGUILayout.Foldout(true, $"Scene References ({references.Count})");

                if (showReferences)
                {
                    EditorGUI.indentLevel++;
                    foreach (var go in references)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.ObjectField(go, typeof(GameObject), true);
                        if (GUILayout.Button("Select", GUILayout.Width(60)))
                        {
                            Selection.activeGameObject = go;
                            SceneView.FrameLastActiveSceneView();
                        }

                        EditorGUILayout.EndHorizontal();
                    }

                    EditorGUI.indentLevel--;
                }
            }
        }
        
        private void FindReferencesInScene()
        {
            references.Clear();
            var targetSO = target;
            
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;

                var rootObjects = scene.GetRootGameObjects();
                foreach (var root in rootObjects)
                    
                foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (component == null) continue;

                    var serializedComponent = new SerializedObject(component);
                    var property = serializedComponent.GetIterator();

                    while (property.Next(true))
                        if (property.propertyType == SerializedPropertyType.ObjectReference &&
                            property.objectReferenceValue == targetSO)
                            if (!references.Contains(component.gameObject))
                                references.Add(component.gameObject);
                }
            }
        }

        private void OnEditorUpdate()
        {
            var currentTime = EditorApplication.timeSinceStartup;
            if (currentTime - lastUpdateTime >= updateInterval)
            {
                lastUpdateTime = currentTime;
                FindReferencesInScene();
            }
        }
    }
}