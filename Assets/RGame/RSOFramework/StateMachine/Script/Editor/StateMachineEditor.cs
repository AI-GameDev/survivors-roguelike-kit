#region

using UnityEditor;
using UnityEngine;

#endregion

namespace RGame.ScriptableCoreKit
{
    [CustomEditor(typeof(StateMachine))]
    public class StateMachineEditor : Editor
    {
        private GUIStyle buttonStyle;

        private GUIStyle headerStyle;
        private GUIStyle infoBoxStyle;

        private readonly Color primaryColor = new(0.2f, 0.6f, 1f, 1f);
        private StateMachine stateMachine;
        private SerializedProperty stateMachineSOProperty;
        private readonly Color successColor = new(0.3f, 0.8f, 0.3f, 1f);
        private GUIStyle warningBoxStyle;
        private readonly Color warningColor = new(1f, 0.7f, 0.2f, 1f);

        private void OnEnable()
        {
            stateMachine = (StateMachine)target;
            stateMachineSOProperty = serializedObject.FindProperty("stateMachineSO");
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

            DrawStateMachineSection();
            EditorGUILayout.Space(5);

            DrawBlackboardSection();
            EditorGUILayout.Space(10);

            DrawActionButtons();
            EditorGUILayout.Space(5);

            DrawRuntimeInfo();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawHeader()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("State Machine Controller", headerStyle);

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();

                    var status = Application.isPlaying ? "Runtime" : "Editor";
                    var statusColor = Application.isPlaying ? successColor : primaryColor;

                    var originalColor = GUI.color;
                    GUI.color = statusColor;
                    GUILayout.Label($"● {status}", EditorStyles.boldLabel, GUILayout.Width(80));
                    GUI.color = originalColor;

                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void DrawStateMachineSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("State Machine Asset", EditorStyles.boldLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(stateMachineSOProperty, GUIContent.none);

                    if (GUILayout.Button("New", GUILayout.Width(50))) CreateNewStateMachineSO();
                }

                if (stateMachine.MyStateMachineSO == null)
                    EditorGUILayout.HelpBox("⚠ State Machine SO is required! Please assign or create a new one.",
                        MessageType.Warning);
                else
                    DrawStateMachineInfo();
            }
        }

        private void DrawStateMachineInfo()
        {
            var smSO = stateMachine.MyStateMachineSO;

            using (new EditorGUILayout.VerticalScope())
            {
                EditorGUILayout.Space(5);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("States:", GUILayout.Width(50));
                    EditorGUILayout.LabelField(smSO.States?.Count.ToString() ?? "0", EditorStyles.boldLabel);

                    GUILayout.FlexibleSpace();

                    EditorGUILayout.LabelField("Status:", GUILayout.Width(50));
                    var statusColor = GetStateColor(smSO.MachineState);
                    var originalColor = GUI.color;
                    GUI.color = statusColor;
                    EditorGUILayout.LabelField(smSO.MachineState.ToString(), EditorStyles.boldLabel);
                    GUI.color = originalColor;
                }

                if (Application.isPlaying && smSO.CurrentState != null)
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("Current State:", GUILayout.Width(100));
                        EditorGUILayout.LabelField(smSO.CurrentState.GetDisplayName(), EditorStyles.boldLabel);
                    }

                EditorGUILayout.Space(5);
            }
        }

        private void DrawBlackboardSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Blackboard Configuration", EditorStyles.boldLabel);

                if (stateMachine.MyStateMachineSO != null)
                {
                    var blackboardTable = stateMachine.MyStateMachineSO.blackboardTable;

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (blackboardTable != null)
                            EditorGUILayout.LabelField("\u2713 Blackboard configured",
                                new GUIStyle(EditorStyles.label) { normal = { textColor = successColor } });
                        else
                            EditorGUILayout.LabelField("⚠ No blackboard configured",
                                new GUIStyle(EditorStyles.label) { normal = { textColor = warningColor } });

                        GUILayout.FlexibleSpace();
                    }

                    if (blackboardTable == null)
                        EditorGUILayout.HelpBox("Blackboard Table is required for proper state machine functionality.",
                            MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.HelpBox("Assign a State Machine SO first to configure blackboard.",
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

        private void DrawRuntimeInfo()
        {
            if (Application.isPlaying && stateMachine.MyStateMachineSO != null)
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("Runtime Information", EditorStyles.boldLabel);

                    var machineState = stateMachine.MyStateMachineSO.MachineState;
                    var stateColor = GetStateColor(machineState);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("Machine State:");

                        var originalColor = GUI.color;
                        GUI.color = stateColor;
                        EditorGUILayout.LabelField(machineState.ToString(), EditorStyles.boldLabel);
                        GUI.color = originalColor;
                    }

                    if (stateMachine.MyStateMachineSO.CurrentState != null)
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField("Time in State:");
                            EditorGUILayout.LabelField($"{stateMachine.MyStateMachineSO.CurrentState.TimeInState:F2}s", EditorStyles.boldLabel);
                        }

                    if (Application.isPlaying)
                    {
                        EditorUtility.SetDirty(target);
                        Repaint();
                    }
                }
        }

        private void CreateNewStateMachineSO()
        {
            var path = EditorUtility.SaveFilePanelInProject(
                "Create New State Machine",
                "NewStateMachine",
                "asset",
                "Choose location for new State Machine SO"
            );

            if (!string.IsNullOrEmpty(path))
            {
                var newStateMachineSO = CreateInstance<StateMachineSO>();
                AssetDatabase.CreateAsset(newStateMachineSO, path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                stateMachineSOProperty.objectReferenceValue = newStateMachineSO;
                serializedObject.ApplyModifiedProperties();

                EditorGUIUtility.PingObject(newStateMachineSO);
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