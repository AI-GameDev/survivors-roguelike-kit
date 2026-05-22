#region

using System.Collections;
using System.Collections.Generic;
using RGame.CommonStat;
using RGame.Framework;
using RGame.RoguelikeKit;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;

using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;

#endregion

namespace RGame.MLAgents
{
    /// <summary>
    ///     ML 학습 모드 부트스트랩. PlayerSpawn 이벤트를 받아 동적으로 ML 컴포넌트를 부착하고
    ///     사망 시 Game.unity 를 재로드하여 다음 에피소드를 시작한다.
    ///     이 컴포넌트는 MLInitialization 씬에만 존재하므로 정상 플레이 흐름에는 영향이 없다.
    /// </summary>
    public class MLAgentBootstrap : MonoBehaviour
    {
        [Header("Spawn / Reset Channels")]
        [SerializeField] private PlayerSpawnChannelSO _playerSpawnChannel;
        [SerializeField] private VoidEventChannelSO _gameOverChannel;
        [SerializeField] private VoidEventChannelSO _levelUpChannel;
        [SerializeField] private LoadEventChannelSO _loadLevelChannel;
        [SerializeField] private GameSceneSO _gameLevelScene;

        [Header("Balance Logger Channels (optional)")]
        [SerializeField] private bool _enableBalanceLogger = true;
        [SerializeField] private StringEventChannelSO _upgradeSkillChannel;
        [SerializeField] private VoidEventChannelSO _openTreasureChannel;

        [Header("Runtime State SOs")]
        [SerializeField] private CommonStatRuntimeSO _stats;
        [SerializeField] private EnemySystem _enemySystem;
        [SerializeField] private GlobalConfigSO _globalConfig;
        [SerializeField] private ExpConfig _expConfig;

        [Header("Agent Configuration")]
        [SerializeField] private string _behaviorName = "SurvivorFighterAgent";
        [SerializeField] private int _decisionPeriod = 5;
        [SerializeField] private bool _useInference;
        [SerializeField] private Unity.InferenceEngine.ModelAsset _inferenceModel;
        [SerializeField] private float _resetDelaySeconds = 0.5f;
        [SerializeField] private float _trainingTimeScale = 20f;

        [Header("Threat Sensor (A)")]
        [SerializeField] private string[] _threatTags = new[] { "Enemy", "EnemyHit" };
        [SerializeField] private int _threatRaysPerDir = 4;
        [SerializeField] private float _threatRayLength = 12f;
        [SerializeField] private float _threatMaxAngle = 90f;

        [Header("Reward Sensor (B)")]
        [SerializeField] private string[] _rewardTags = new[] { "Exp", "DropOut" };
        [SerializeField] private int _rewardRaysPerDir = 4;
        [SerializeField] private float _rewardRayLength = 15f;
        [SerializeField] private float _rewardMaxAngle = 90f;

        private GameObject _rig;
        private SurvivorFighterAgent _agent;
        private MLBalanceLogger _balanceLogger;
        private bool _initialized;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            Application.runInBackground = true;
#if UNITY_EDITOR
            // ML-Agents가 플레이 중 Assets/ML-Agents/Timers/*.json을 써서 AssetDatabase 자동 임포트를
            // 유발하면 씬 언로드(PersistentManager 락)와 충돌해 SIGSEGV 크래시가 발생함.
            // 플레이 모드 동안 자동 임포트를 억제해 이를 방지한다.
            UnityEditor.AssetDatabase.DisallowAutoRefresh();
#endif
        }

        private void OnDestroy()
        {
#if UNITY_EDITOR
            UnityEditor.AssetDatabase.AllowAutoRefresh();
#endif
        }

        private void OnEnable()
        {
            if (_playerSpawnChannel != null) _playerSpawnChannel.RegisterListener(OnPlayerSpawn);
            if (_gameOverChannel != null) _gameOverChannel.RegisterListener(OnGameOver);
        }

        private void OnDisable()
        {
            if (_playerSpawnChannel != null) _playerSpawnChannel.UnregisterListener(OnPlayerSpawn);
            if (_gameOverChannel != null) _gameOverChannel.UnregisterListener(OnGameOver);
        }

        private void OnPlayerSpawn(Player player)
        {
            if (player == null) return;

            Transform playerTf = player.transform;
            Rigidbody2D rb = player.GetComponentInParent<Rigidbody2D>();
            if (rb == null) rb = playerTf.GetComponentInChildren<Rigidbody2D>();

            PlayerMovement movement = playerTf.GetComponentInParent<PlayerMovement>();
            if (movement != null) movement.enabled = false;

            if (!_initialized)
            {
                CreateAgentRig();
                CreateBalanceLogger();
                _initialized = true;
            }

            Transform rootTf = (rb != null) ? rb.transform : playerTf;
            _agent.SetPlayer(rootTf, rb);

            if (_balanceLogger != null)
            {
                _balanceLogger.SetPlayer(rootTf);
                _balanceLogger.BeginEpisode();
            }

            AttachMLCameraFollow(rootTf);
        }

        private void AttachMLCameraFollow(Transform target)
        {
            // ML 모드(학습/추론)에서는 맵 경계로 카메라가 멈추지 않게 한다.
            // 기존 CameraFollow를 disable하고 경계 무시하는 MLCameraFollow를 부착.
            var mainCam = Camera.main;
            if (mainCam == null) return;
            var original = mainCam.GetComponent<CameraFollow>();
            if (original != null) original.enabled = false;
            var mlFollow = mainCam.GetComponent<MLCameraFollow>();
            if (mlFollow == null) mlFollow = mainCam.gameObject.AddComponent<MLCameraFollow>();
            mlFollow.Target = target;
            if (original != null) mlFollow.SmoothSpeed = original.SmoothSpeed;
            mlFollow.enabled = true;
        }

        private void CreateBalanceLogger()
        {
            if (!_enableBalanceLogger) return;
            // 학습 모드(mlagents-learn 연결됨)에서는 PlayTrace 서버 부하를 피하기 위해 logger를 만들지 않는다.
            // inference Play와 heuristic 수동 테스트에서만 활성화.
            if (Academy.Instance.IsCommunicatorOn)
            {
                Debug.Log("[MLBoot] Balance logger skipped — training mode (Academy communicator on)");
                return;
            }
            _balanceLogger = gameObject.AddComponent<MLBalanceLogger>();
            string modelName = (_useInference && _inferenceModel != null) ? _inferenceModel.name : _behaviorName;
            _balanceLogger.Init(
                _stats, _globalConfig, _enemySystem,
                _upgradeSkillChannel, _levelUpChannel, _openTreasureChannel, _gameOverChannel,
                modelName);
        }

        private void CreateAgentRig()
        {
            _rig = new GameObject("MLAgentRig");
            _rig.transform.SetParent(transform, worldPositionStays: false);

            var bp = _rig.AddComponent<BehaviorParameters>();
            bp.BehaviorName = _behaviorName;
            bp.BrainParameters.VectorObservationSize = 39;
            bp.BrainParameters.ActionSpec = ActionSpec.MakeContinuous(2);
            if (_useInference && _inferenceModel != null)
            {
                bp.Model = _inferenceModel;
                bp.BehaviorType = BehaviorType.InferenceOnly;
            }
            else
            {
                bp.BehaviorType = BehaviorType.Default;
            }

            // 360° 사방 커버 — Vampire Survivors는 적이 사방에서 옴. Inspector 값 무시하고 강제 180.
            AddRaySensor(_rig, "ThreatSensor", _threatTags, _threatRaysPerDir, _threatRayLength, 180f);
            AddRaySensor(_rig, "RewardSensor", _rewardTags, _rewardRaysPerDir, _rewardRayLength, 180f);

            _agent = _rig.AddComponent<SurvivorFighterAgent>();
            _agent.Inject(_stats, _enemySystem, _globalConfig, _expConfig, _gameOverChannel, _levelUpChannel);

            var dr = _rig.AddComponent<DecisionRequester>();
            dr.DecisionPeriod = _decisionPeriod;
            dr.TakeActionsBetweenDecisions = true;
        }

        private static void AddRaySensor(GameObject host, string sensorName, string[] tags, int raysPerDir, float length, float maxAngle)
        {
            var sensor = host.AddComponent<RayPerceptionSensorComponent2D>();
            sensor.SensorName = sensorName;
            sensor.DetectableTags = new System.Collections.Generic.List<string>(tags);
            sensor.RaysPerDirection = raysPerDir;
            sensor.MaxRayDegrees = maxAngle;
            sensor.RayLength = length;
            sensor.SphereCastRadius = 0.5f;
            sensor.ObservationStacks = 1;
        }

        private void OnGameOver()
        {
            // Agent가 MaxStep timeout 직전 CheckTimeoutClear에서 RaiseEvent한 케이스는 "timeout"으로 분리 기록한다.
            // 그래야 PlayTrace의 episode.cause 가 진짜 contact death 와 timer 종료를 구분할 수 있다.
            string cause = (_agent != null && _agent.ClearedTimeoutThisEpisode) ? "timeout" : "death";
            Debug.Log("[MLBoot] OnGameOver fired. cause=" + cause + " timeScale was=" + Time.timeScale);
            // 에피소드 통계를 reload 시작 전에 dump.
            if (_balanceLogger != null) _balanceLogger.FlushEpisode(cause);
            // FixedUpdate를 즉시 멈춰 큐에 쌓인 시간-스텝 액션이 파괴된 Player를 참조하지 못하게 한다.
            Time.timeScale = 0f;
            StartCoroutine(ReloadGameScene());
        }

        private IEnumerator ReloadGameScene()
        {
            Debug.Log("[MLBoot] ReloadGameScene start, waiting " + _resetDelaySeconds + "s realtime");
            yield return new WaitForSecondsRealtime(_resetDelaySeconds);
            // timeScale=0을 씬 언로드/리로드 완료까지 유지해 Academy.FixedUpdate가
            // 씬 해제 중 Job을 스케줄하지 못하도록 한다. 복원은 reload 완료 후 수행.
            Debug.Log("[MLBoot] After wait, beginning reload (timeScale stays 0)");

            if (_gameLevelScene == null || _gameLevelScene.sceneReference == null)
            {
                Debug.LogError("[MLBoot] _gameLevelScene or sceneReference is null — abort reload");
                yield break;
            }

            // SceneManager 기반 unload — Addressables AsyncOperationHandle yield 버그 회피.
            // 활성 추가 씬 중 GamePlay(매니저) 외의 씬을 레벨 씬으로 간주하여 unload.
            int sceneCount = SceneManager.sceneCount;
            Scene levelScene = default;
            for (int i = 0; i < sceneCount; i++)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (!s.isLoaded) continue;
                if (s.name == "GamePlay" || s.name == "MLInitialization" || s.name == "PersistentManagers" || s.name == "Initialization") continue;
                levelScene = s;
                break;
            }
            Debug.Log("[MLBoot] Detected level scene to unload: name='" + (levelScene.IsValid() ? levelScene.name : "<none>") + "'");

            if (levelScene.IsValid() && levelScene.isLoaded)
            {
                AsyncOperation unloadOp = SceneManager.UnloadSceneAsync(levelScene);
                if (unloadOp != null)
                {
                    while (!unloadOp.isDone) yield return null;
                }
                Debug.Log("[MLBoot] SceneManager unload done");
            }

            // GamePlay 매니저 씬도 reload하여 SkillManager/Pool/UI 등 SO 누적 상태를 완전 초기화.
            Scene gameplayScene = SceneManager.GetSceneByName("GamePlay");
            GameSceneSO gameplaySO = FindGameplaySceneSO();
            if (gameplayScene.IsValid() && gameplayScene.isLoaded)
            {
                Debug.Log("[MLBoot] Unloading GamePlay manager scene");
                AsyncOperation unloadGp = SceneManager.UnloadSceneAsync(gameplayScene);
                if (unloadGp != null)
                {
                    while (!unloadGp.isDone) yield return null;
                }
            }
            if (gameplaySO != null && gameplaySO.sceneReference != null)
            {
                if (gameplaySO.sceneReference.OperationHandle.IsValid())
                {
                    gameplaySO.sceneReference.ReleaseAsset();
                }
                Debug.Log("[MLBoot] Reloading GamePlay manager scene");
                var gpLoad = gameplaySO.sceneReference.LoadSceneAsync(LoadSceneMode.Additive, true, 0);
                while (!gpLoad.IsDone) yield return null;
                Debug.Log("[MLBoot] GamePlay reload status=" + gpLoad.Status);
            }
            else
            {
                Debug.LogWarning("[MLBoot] GamePlay GameSceneSO not found — skipping manager reload");
            }

            // AssetReference 내부 핸들 캐시가 unload된 씬을 가리키고 있을 수 있어 명시적으로 release.
            var sceneRef = _gameLevelScene.sceneReference;
            if (sceneRef.OperationHandle.IsValid())
            {
                Debug.Log("[MLBoot] Releasing stale AssetReference handle");
                sceneRef.ReleaseAsset();
            }

            Debug.Log("[MLBoot] Calling LoadSceneAsync");
            var loadOp = sceneRef.LoadSceneAsync(LoadSceneMode.Additive, true, 0);
            while (!loadOp.IsDone) yield return null;
            Debug.Log("[MLBoot] Load done, status=" + loadOp.Status);

            if (loadOp.Status == AsyncOperationStatus.Succeeded)
            {
                SceneManager.SetActiveScene(loadOp.Result.Scene);
                Debug.Log("[MLBoot] SetActiveScene done: " + loadOp.Result.Scene.name);
            }

            // GamePlay 씬 reload 후에도 PoolRuntimeSO는 ScriptableObject라 OnDisable 안 됨 → 수동 reset.
            ResetAllPools();
            HideGameOverPanel();

            // 씬 언로드/리로드가 완전히 끝난 뒤 timeScale 복원.
            Time.timeScale = _useInference ? 1f : _trainingTimeScale;
            Debug.Log("[MLBoot] Reload complete, timeScale restored to " + Time.timeScale);
        }

        private static void ResetAllPools()
        {
            // PoolRuntimeSO의 private 상태(Dictionary들 + _isInitialized)를 reflection으로 클리어.
            // 다음 접근 시 EnsureInitialized가 자동 재초기화 → 누적 정의 제거.
            var pools = Resources.FindObjectsOfTypeAll<RGame.Framework.PoolRuntimeSO>();
            if (pools == null || pools.Length == 0) return;

            var t = typeof(RGame.Framework.PoolRuntimeSO);
            var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            string[] dictFields = { "_poolConfigs", "_pooledObjects", "_poolContainers", "_activeObjects" };

            foreach (var pool in pools)
            {
                foreach (var name in dictFields)
                {
                    var dict = t.GetField(name, bf)?.GetValue(pool) as System.Collections.IDictionary;
                    dict?.Clear();
                }
                t.GetField("_rootTransform", bf)?.SetValue(pool, null);
                t.GetField("_isInitialized", bf)?.SetValue(pool, false);
            }
            Debug.Log("[MLBoot] Reset " + pools.Length + " PoolRuntimeSO instance(s)");
        }

        private static GameSceneSO FindGameplaySceneSO()
        {
            // SceneLoader에서 gameplaySceneSO 필드를 reflection으로 가져온다.
            var sceneLoader = Object.FindFirstObjectByType<SceneLoader>(FindObjectsInactive.Include);
            if (sceneLoader == null) return null;
            var field = typeof(SceneLoader).GetField("gameplaySceneSO",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return field?.GetValue(sceneLoader) as GameSceneSO;
        }

        private static void ResetPersistentManagerState()
        {
            // SkillManager: 이전 라이프에서 누적된 _skillDataDictionary, _activeSkills, 카운터 리셋.
            var skillMgr = Object.FindFirstObjectByType<SkillManager>(FindObjectsInactive.Include);
            if (skillMgr != null)
            {
                var t = typeof(SkillManager);
                var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

                var skillData = t.GetField("_skillData", bf)?.GetValue(skillMgr) as SkillDataSO[];
                var dict = t.GetField("_skillDataDictionary", bf)?.GetValue(skillMgr) as Dictionary<string, SkillDataSO>;
                var activeSet = t.GetField("_activeSkills", bf)?.GetValue(skillMgr) as HashSet<string>;
                var attackCountField = t.GetField("_activeAttackSkill", bf);
                var attributeCountField = t.GetField("_activeAttributeSkill", bf);

                if (dict != null && skillData != null)
                {
                    dict.Clear();
                    foreach (var sd in skillData)
                    {
                        if (sd.SkillType == SkillType.AttributeSkill)
                            sd.Key = sd.AttributeType.ToString();
                        dict.Add(sd.Key, sd.Copy());
                    }
                }
                if (activeSet != null) activeSet.Clear();
                attackCountField?.SetValue(skillMgr, 0);
                attributeCountField?.SetValue(skillMgr, 0);
                Debug.Log("[MLBoot] SkillManager state reset");
            }
            else
            {
                Debug.LogWarning("[MLBoot] SkillManager not found during reset");
            }

            // TimeStepManager: 이전 라이프 timer가 새 라이프에서 계속 fire되는 것 방지.
            var timeStep = Object.FindFirstObjectByType<TimeStepManager>(FindObjectsInactive.Include);
            if (timeStep != null)
            {
                var timers = typeof(TimeStepManager).GetField("_activeTimers",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.GetValue(timeStep) as System.Collections.IList;
                int prevCount = timers?.Count ?? 0;
                timers?.Clear();
                Debug.Log("[MLBoot] TimeStepManager timers cleared (was " + prevCount + ")");
            }
        }

        private static void HideGameOverPanel()
        {
            // GameEndPanel 클래스가 GameWin/GameOver 두 인스턴스에 공통으로 쓰이므로 모두 리셋한다.
            var panels = Object.FindObjectsByType<GameEndPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (panels == null || panels.Length == 0)
            {
                Debug.LogWarning("[MLBoot] No GameEndPanel found");
                return;
            }

            var field = typeof(GameEndPanel).GetField("_canvasGroup",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            foreach (var panel in panels)
            {
                CanvasGroup cg = null;
                if (field != null) cg = field.GetValue(panel) as CanvasGroup;
                if (cg == null) cg = panel.GetComponent<CanvasGroup>();
                if (cg == null) cg = panel.GetComponentInChildren<CanvasGroup>(true);

                if (cg != null)
                {
                    cg.alpha = 0f;
                    cg.blocksRaycasts = false;
                    cg.interactable = false;
                    Debug.Log("[MLBoot] Reset panel " + panel.gameObject.name + " (cg on " + cg.gameObject.name + ")");
                }
            }
        }
    }
}
