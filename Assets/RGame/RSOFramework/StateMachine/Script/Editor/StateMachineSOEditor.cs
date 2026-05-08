#region

using UnityEditor;
using UnityEngine;

#endregion

namespace RGame.ScriptableCoreKit
{
    [CustomEditor(typeof(StateMachineSO))]
    public class StateMachineSOEditor : Editor
    {
        private SerializedProperty blackboardTableProperty;
        private GUIStyle buttonStyle;

        private GUIStyle headerStyle;
        private GUIStyle infoBoxStyle;

        private readonly Color primaryColor = new(0.2f, 0.6f, 1f, 1f);
        private StateMachineSO stateMachineSO;
        private readonly Color successColor = new(0.3f, 0.8f, 0.3f, 1f);
        private GUIStyle warningBoxStyle;
        private readonly Color warningColor = new(1f, 0.7f, 0.2f, 1f);

        private void OnEnable()
        {
            stateMachineSO = (StateMachineSO)target;
            blackboardTableProperty = serializedObject.FindProperty("blackboardTable");
        }

        private void InitializeStyles()
        {
            if (headerStyle == null)
                headerStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 16,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = primaryColor }
                };

            if (buttonStyle == null)
                buttonStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    fixedHeight = 30,
                    margin = new RectOffset(4, 4, 4, 4)
                };

            if (warningBoxStyle == null)
                warningBoxStyle = new GUIStyle(EditorStyles.helpBox)
                {
                    normal = { textColor = warningColor },
                    fontSize = 11,
                    fontStyle = FontStyle.Bold
                };

            if (infoBoxStyle == null)
                infoBoxStyle = new GUIStyle(EditorStyles.helpBox)
                {
                    normal = { textColor = Color.white },
                    fontSize = 10,
                    wordWrap = true
                };
        }

        public override void OnInspectorGUI()
        {
            InitializeStyles();
            serializedObject.Update();

            DrawHeader();
            EditorGUILayout.Space(10);

            DrawBlackboardSection();
            EditorGUILayout.Space(10);

            DrawStateMachineInfoSection();
            EditorGUILayout.Space(10);

            DrawActionButtons();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawHeader()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("State Machine ScriptableObject", headerStyle);

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();

                    var status = stateMachineSO.MachineState.ToString();
                    var statusColor = GetStateColor(stateMachineSO.MachineState);

                    var originalColor = GUI.color;
                    GUI.color = statusColor;
                    GUILayout.Label($"● {status}", EditorStyles.boldLabel, GUILayout.Width(100));
                    GUI.color = originalColor;

                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void DrawBlackboardSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Blackboard Configuration", EditorStyles.boldLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(blackboardTableProperty, new GUIContent("Blackboard Table"));

                    if (GUILayout.Button("New", GUILayout.Width(50))) CreateNewBlackboardTable();
                }

                if (stateMachineSO.blackboardTable == null)
                {
                    EditorGUILayout.HelpBox("⚠ Blackboard Table is required for proper state machine functionality! " +
                                            "Please assign or create a new one.", MessageType.Warning);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.FlexibleSpace();

                        if (GUILayout.Button("Create Blackboard Table")) CreateNewBlackboardTable();

                        GUILayout.FlexibleSpace();
                    }
                }
                else
                {
                    DrawBlackboardInfo();
                }
            }
        }

        private void DrawBlackboardInfo()
        {
            var blackboardTable = stateMachineSO.blackboardTable;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.Space(5);

                using (new EditorGUILayout.HorizontalScope())
                {
                    var originalColor = GUI.color;
                    GUI.color = successColor;
                    EditorGUILayout.LabelField("✓ Blackboard configured", EditorStyles.boldLabel);
                    GUI.color = originalColor;

                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("Edit Blackboard", GUILayout.Width(120)))
                    {
                        Selection.activeObject = blackboardTable;
                        EditorGUIUtility.PingObject(blackboardTable);
                    }
                }

                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField($"Asset Name: {blackboardTable.name}");
            }
        }

        private void DrawStateMachineInfoSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("State Machine Information", EditorStyles.boldLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("States:", GUILayout.Width(50));
                    EditorGUILayout.LabelField(stateMachineSO.States?.Count.ToString() ?? "0", EditorStyles.boldLabel);

                    GUILayout.FlexibleSpace();

                    EditorGUILayout.LabelField("Status:", GUILayout.Width(50));
                    var stateColor = GetStateColor(stateMachineSO.MachineState);
                    var originalColor = GUI.color;
                    GUI.color = stateColor;
                    EditorGUILayout.LabelField(stateMachineSO.MachineState.ToString(), EditorStyles.boldLabel);
                    GUI.color = originalColor;
                }

                if (Application.isPlaying && stateMachineSO.CurrentState != null)
                {
                    EditorGUILayout.Space(5);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("Current State:", GUILayout.Width(100));
                        EditorGUILayout.LabelField(stateMachineSO.CurrentState.GetDisplayName(), EditorStyles.boldLabel);
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("Time in State:", GUILayout.Width(100));
                        EditorGUILayout.LabelField($"{stateMachineSO.CurrentState.TimeInState:F2}s", EditorStyles.boldLabel);
                    }
                }

                if (Application.isPlaying)
                {
                    EditorGUILayout.Space(5);
                    EditorGUILayout.HelpBox("Runtime mode: State machine state may change during execution.",
                        MessageType.Info);
                }
            }
        }

        private void DrawActionButtons()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    var originalColor = GUI.backgroundColor;
                    GUI.backgroundColor = primaryColor;

                    if (GUILayout.Button("Open State Machine Editor", buttonStyle)) StateMachineWindow.OpenWindow();

                    GUI.backgroundColor = originalColor;
                }
            }
        }

        private void CreateNewBlackboardTable()
        {
            var path = EditorUtility.SaveFilePanelInProject(
                "Create New Blackboard Table",
                "NewBlackboardTable",
                "asset",
                "Choose location for new Blackboard Table"
            );

            if (!string.IsNullOrEmpty(path))
            {
                var newBlackboardTable = CreateInstance<BlackboardTable>();
                AssetDatabase.CreateAsset(newBlackboardTable, path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                blackboardTableProperty.objectReferenceValue = newBlackboardTable;
                serializedObject.ApplyModifiedProperties();

                EditorGUIUtility.PingObject(newBlackboardTable);
            }
        }

        private Color GetStateColor(StateMachineState state)
        {
            return state switch
            {
                StateMachineState.Running => new Color(0.3f, 0.8f, 0.3f, 1f),
                StateMachineState.Paused => new Color(1f, 0.7f, 0.2f, 1f),
                StateMachineState.Stopped => new Color(0.5f, 0.5f, 0.5f, 1f),
                StateMachineState.Error => new Color(1f, 0.3f, 0.3f, 1f),
                _ => Color.white
            };
        }
    }
}