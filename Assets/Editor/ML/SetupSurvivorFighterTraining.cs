#region

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

#endregion

namespace RGame.MLAgents.EditorTools
{
    /// <summary>
    ///     ML 학습 시작/종료를 위한 보조 메뉴.
    ///     Build Settings의 index 0 씬을 일시적으로 MLInitialization으로 교체/복원한다.
    /// </summary>
    public static class SetupSurvivorFighterTraining
    {
        private const string ML_SCENE_PATH = "Assets/Scenes/MLInitialization.unity";
        private const string NORMAL_SCENE_PATH = "Assets/RGame/RoguelikeKit/Scenes/Initialization.unity";

        [MenuItem("Tools/ML/Open MLInitialization Scene")]
        public static void OpenMlScene()
        {
            EditorSceneManager.OpenScene(ML_SCENE_PATH);
        }

        [MenuItem("Tools/ML/Set MLInitialization as Build Index 0")]
        public static void SetMlSceneAsBuildIndex0()
        {
            ReplaceIndexZero(ML_SCENE_PATH);
            Debug.Log($"[ML Setup] Build Settings index 0 → {ML_SCENE_PATH}. ML 학습 모드 준비 완료.");
        }

        [MenuItem("Tools/ML/Restore Normal Play (Initialization at Build Index 0)")]
        public static void RestoreNormalPlay()
        {
            ReplaceIndexZero(NORMAL_SCENE_PATH);
            Debug.Log($"[ML Setup] Build Settings index 0 → {NORMAL_SCENE_PATH}. 정상 플레이 모드 복원.");
        }

        [MenuItem("Tools/ML/Wire Level Up Autopilot")]
        public static void WireLevelUpAutopilot()
        {
            const string UPGRADE_CHANNEL_PATH = "Assets/RGame/RoguelikeKit/ScriptableObjects/LogicSO/EventChannels/Skill/OpenUpgradeSkillUIChannel.asset";
            const string TREASURE_CHANNEL_PATH = "Assets/RGame/RoguelikeKit/ScriptableObjects/LogicSO/EventChannels/Skill/OpenTreasurePanelChannel.asset";

            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ML_SCENE_PATH);
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

            GameObject host = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                var bs = root.GetComponentInChildren<RGame.MLAgents.MLAgentBootstrap>(true);
                if (bs != null) { host = bs.gameObject; break; }
            }
            if (host == null)
            {
                Debug.LogError("[ML Setup] MLAgentBootstrap host not found in MLInitialization scene");
                return;
            }

            var autopilot = host.GetComponent<RGame.MLAgents.MLLevelUpAutopilot>();
            if (autopilot == null)
            {
                autopilot = host.AddComponent<RGame.MLAgents.MLLevelUpAutopilot>();
                Debug.Log("[ML Setup] Added MLLevelUpAutopilot to " + host.name);
            }

            var upgradeChannel = AssetDatabase.LoadAssetAtPath<RGame.RoguelikeKit.UpgradeSkillUIChannel>(UPGRADE_CHANNEL_PATH);
            var treasureChannel = AssetDatabase.LoadAssetAtPath<RGame.RoguelikeKit.UpgradeSkillUIChannel>(TREASURE_CHANNEL_PATH);
            if (upgradeChannel == null)
            {
                Debug.LogError("[ML Setup] UpgradeSkillUIChannel asset not found at " + UPGRADE_CHANNEL_PATH);
                return;
            }
            if (treasureChannel == null)
            {
                Debug.LogWarning("[ML Setup] OpenTreasurePanelChannel asset not found at " + TREASURE_CHANNEL_PATH);
            }

            var so = new SerializedObject(autopilot);
            so.FindProperty("_openUpgradeUIChannel").objectReferenceValue = upgradeChannel;
            var treasureProp = so.FindProperty("_openTreasurePanelChannel");
            if (treasureProp != null && treasureChannel != null) treasureProp.objectReferenceValue = treasureChannel;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(autopilot);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            Debug.Log("[ML Setup] Autopilot wired and scene saved.");
        }

        [MenuItem("Tools/ML/Setup Inference Mode (v5)")]
        public static void SetupInferenceMode()
        {
            const string MODEL_PATH = "Assets/ML-Models/SurvivorFighterAgent_v5.onnx";
            ApplyInferenceMode(true, MODEL_PATH);
        }

        [MenuItem("Tools/ML/Setup Inference Mode (v5+)")]
        public static void SetupInferenceModeV5Plus()
        {
            const string MODEL_PATH = "Assets/ML-Models/SurvivorFighterAgent_v5plus.onnx";
            ApplyInferenceMode(true, MODEL_PATH);
        }

        [MenuItem("Tools/ML/Setup Inference Mode (v7)")]
        public static void SetupInferenceModeV7()
        {
            const string MODEL_PATH = "Assets/ML-Models/SurvivorFighterAgent_v7.onnx";
            ApplyInferenceMode(true, MODEL_PATH);
        }

        [MenuItem("Tools/ML/Setup Inference Mode (v8)")]
        public static void SetupInferenceModeV8()
        {
            const string MODEL_PATH = "Assets/ML-Models/SurvivorFighterAgent_v8.onnx";
            ApplyInferenceMode(true, MODEL_PATH);
        }

        [MenuItem("Tools/ML/Setup Inference Mode (v9 final)")]
        public static void SetupInferenceModeV9Final()
        {
            const string MODEL_PATH = "Assets/ML-Models/SurvivorFighterAgent_v9_final.onnx";
            ApplyInferenceMode(true, MODEL_PATH);
        }

        [MenuItem("Tools/ML/Setup Inference Mode (v9 best)")]
        public static void SetupInferenceModeV9Best()
        {
            const string MODEL_PATH = "Assets/ML-Models/SurvivorFighterAgent_v9_best.onnx";
            ApplyInferenceMode(true, MODEL_PATH);
        }

        [MenuItem("Tools/ML/Setup Inference Mode (v10 final)")]
        public static void SetupInferenceModeV10Final()
        {
            const string MODEL_PATH = "Assets/ML-Models/SurvivorFighterAgent_v10_final.onnx";
            ApplyInferenceMode(true, MODEL_PATH);
        }

        [MenuItem("Tools/ML/Setup Inference Mode (v10 best)")]
        public static void SetupInferenceModeV10Best()
        {
            const string MODEL_PATH = "Assets/ML-Models/SurvivorFighterAgent_v10_best.onnx";
            ApplyInferenceMode(true, MODEL_PATH);
        }

        [MenuItem("Tools/ML/Setup Inference Mode (v11 final)")]
        public static void SetupInferenceModeV11Final()
        {
            const string MODEL_PATH = "Assets/ML-Models/SurvivorFighterAgent_v11_final.onnx";
            ApplyInferenceMode(true, MODEL_PATH);
        }

        [MenuItem("Tools/ML/Setup Inference Mode (v11 best)")]
        public static void SetupInferenceModeV11Best()
        {
            const string MODEL_PATH = "Assets/ML-Models/SurvivorFighterAgent_v11_best.onnx";
            ApplyInferenceMode(true, MODEL_PATH);
        }

        [MenuItem("Tools/ML/Restore Training Mode")]
        public static void RestoreTrainingMode()
        {
            ApplyInferenceMode(false, null);
        }

        private static void ApplyInferenceMode(bool useInference, string modelPath)
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ML_SCENE_PATH);
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

            RGame.MLAgents.MLAgentBootstrap host = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                var bs = root.GetComponentInChildren<RGame.MLAgents.MLAgentBootstrap>(true);
                if (bs != null) { host = bs; break; }
            }
            if (host == null)
            {
                Debug.LogError("[ML Setup] MLAgentBootstrap not found in MLInitialization scene");
                return;
            }

            var so = new SerializedObject(host);
            so.FindProperty("_useInference").boolValue = useInference;
            if (useInference && !string.IsNullOrEmpty(modelPath))
            {
                var model = AssetDatabase.LoadAssetAtPath<Unity.InferenceEngine.ModelAsset>(modelPath);
                if (model == null)
                {
                    Debug.LogError("[ML Setup] ModelAsset not found at " + modelPath);
                    return;
                }
                so.FindProperty("_inferenceModel").objectReferenceValue = model;
            }
            else
            {
                so.FindProperty("_inferenceModel").objectReferenceValue = null;
            }
            so.ApplyModifiedProperties();

            // Autopilot _postClickTimeScale도 모드에 맞게 동기화
            var autopilot = host.GetComponent<RGame.MLAgents.MLLevelUpAutopilot>();
            if (autopilot != null)
            {
                var aSo = new SerializedObject(autopilot);
                aSo.FindProperty("_postClickTimeScale").floatValue = useInference ? 1f : 20f;
                aSo.ApplyModifiedProperties();
            }

            EditorUtility.SetDirty(host);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            Debug.Log("[ML Setup] " + (useInference ? "Inference mode enabled (model: " + modelPath + ")" : "Training mode restored"));
        }

        [MenuItem("Tools/ML/Print Training Command")]
        public static void PrintTrainingCommand()
        {
            Debug.Log(
                "[ML Setup] Smoke test:\n" +
                "  cd ml-training && source .venv/bin/activate && \\\n" +
                "    mlagents-learn configs/survivor_fighter.yaml --run-id=smoke_01 --time-scale 1 --force\n\n" +
                "터미널에서 위 명령을 실행한 뒤 Unity Editor의 Play 버튼을 누르세요.");
        }

        private static void ReplaceIndexZero(string scenePath)
        {
            var scenes = EditorBuildSettings.scenes;
            var newList = new System.Collections.Generic.List<EditorBuildSettingsScene>();
            newList.Add(new EditorBuildSettingsScene(scenePath, true));
            for (int i = 0; i < scenes.Length; i++)
            {
                if (scenes[i].path != scenePath && scenes[i].path != ML_SCENE_PATH && scenes[i].path != NORMAL_SCENE_PATH)
                {
                    newList.Add(scenes[i]);
                }
                else if (scenes[i].path != scenePath)
                {
                    newList.Add(new EditorBuildSettingsScene(scenes[i].path, false));
                }
            }
            EditorBuildSettings.scenes = newList.ToArray();
        }
    }
}
