#region

using RGame.CommonStat;
using RGame.Framework;
using RGame.RoguelikeKit;
using UnityEngine;

#endregion

namespace RGame.MLAgents
{
    /// <summary>
    ///     레벨 밸런싱용 에피소드 단위 데이터 로거. MLAgentBootstrap이 런타임에 부착하고
    ///     의존성을 주입한다. MLBalanceHook 정적 sink로 등록되어 base 게임 코드의
    ///     데미지/사망/스폰 이벤트를 받는다. listener가 등록되지 않은 일반 플레이에서는
    ///     base 코드의 hook들이 즉시 return → 게임 동작 0% 변화.
    ///
    ///     출력: PlayTraceClient를 통해 http://localhost:8000/api/logs 로 단건 스트리밍.
    ///     project=survivors-roguelike-kit, version=&lt;modelName&gt;, play_no=episodeIndex+1.
    /// </summary>
    public class MLBalanceLogger : MonoBehaviour, MLBalanceHook.ISink
    {
        private const string ProjectName = "survivors-roguelike-kit";

        // Dependencies (주입)
        private CommonStatRuntimeSO _stats;
        private GlobalConfigSO _globalConfig;
        private StringEventChannelSO _upgradeSkillChannel;
        private VoidEventChannelSO _levelUpChannel;
        private VoidEventChannelSO _openTreasureChannel;
        private VoidEventChannelSO _gameOverChannel; // 미사용. Bootstrap 시그니처 호환용.
        private string _modelName;

        private PlayTraceClient _client;
        private int _episodeIndex;

        // 에피소드 상태
        private bool _episodeActive;
        private float _episodeStartTime;
        private int _episodeStartGold;
        private int _episodeStartKills;

        private int _prevLevel;
        private int _episodeDamageTaken;

        // 사망 컨텍스트
        private string _lastDamageTakenSourceKey;
        private string _lastDamageTakenAttackKind;
        private float _recent5sDamageTaken;
        private float _recent5sStartTime;

        // 게임 객체 참조 (사망 컨텍스트 폴링용)
        private Transform _playerTransform;
        private EnemySystem _enemySystem;

        public void Init(
            CommonStatRuntimeSO stats,
            GlobalConfigSO globalConfig,
            EnemySystem enemySystem,
            StringEventChannelSO upgradeSkillChannel,
            VoidEventChannelSO levelUpChannel,
            VoidEventChannelSO openTreasureChannel,
            VoidEventChannelSO gameOverChannel,
            string modelName)
        {
            _stats = stats;
            _globalConfig = globalConfig;
            _enemySystem = enemySystem;
            _upgradeSkillChannel = upgradeSkillChannel;
            _levelUpChannel = levelUpChannel;
            _openTreasureChannel = openTreasureChannel;
            _gameOverChannel = gameOverChannel;
            _modelName = string.IsNullOrEmpty(modelName) ? "unknown" : modelName;

            _client = gameObject.AddComponent<PlayTraceClient>();
            _client.BeginSession(ProjectName, _modelName, _modelName + " run");

            Debug.Log("[MLLogger] init model=" + _modelName);
        }

        public void SetPlayer(Transform playerTransform) { _playerTransform = playerTransform; }

        private void OnEnable()
        {
            MLBalanceHook.Register(this);

            if (_upgradeSkillChannel != null) _upgradeSkillChannel.RegisterListener(OnSkillChosen);
            if (_levelUpChannel != null) _levelUpChannel.RegisterListener(OnLevelUp);
            if (_openTreasureChannel != null) _openTreasureChannel.RegisterListener(OnTreasureOpened);
        }

        private void OnDisable()
        {
            MLBalanceHook.Unregister(this);

            if (_upgradeSkillChannel != null) _upgradeSkillChannel.UnregisterListener(OnSkillChosen);
            if (_levelUpChannel != null) _levelUpChannel.UnregisterListener(OnLevelUp);
            if (_openTreasureChannel != null) _openTreasureChannel.UnregisterListener(OnTreasureOpened);
        }

        private int CurrentPlayNo => _episodeIndex + 1;

        // 에피소드 시작 — Bootstrap이 PlayerSpawn 후 호출.
        public void BeginEpisode()
        {
            _episodeActive = true;
            _episodeStartTime = Time.realtimeSinceStartup;
            _episodeStartGold = _globalConfig != null ? _globalConfig.CurrentGetGold : 0;
            _episodeStartKills = _globalConfig != null ? _globalConfig.CurrentGetKill : 0;
            _prevLevel = _stats != null ? _stats.GetValue("Level") : 1;

            _episodeDamageTaken = 0;
            _lastDamageTakenSourceKey = null;
            _lastDamageTakenAttackKind = null;
            _recent5sDamageTaken = 0f;
            _recent5sStartTime = _episodeStartTime;

            Debug.Log("[MLLogger] BeginEpisode play_no=" + CurrentPlayNo);
        }

        // 에피소드 종료 — Bootstrap이 OnGameOver에서 호출.
        public void FlushEpisode(string cause)
        {
            if (!_episodeActive)
            {
                Debug.LogWarning("[MLLogger] FlushEpisode called but episode not active — skip");
                return;
            }
            _episodeActive = false;

            float endTime = Time.realtimeSinceStartup;
            float duration = endTime - _episodeStartTime;

            int finalLevel = _stats != null ? _stats.GetValue("Level") : 0;
            int finalExp = _stats != null ? _stats.GetValue("Exp") : 0;
            int totalKills = (_globalConfig != null ? _globalConfig.CurrentGetKill : 0) - _episodeStartKills;
            int totalGold = (_globalConfig != null ? _globalConfig.CurrentGetGold : 0) - _episodeStartGold;

            // 사망 컨텍스트
            string nearestEnemyKey = null;
            float nearestEnemyDistance = -1f;
            int activeEnemyCount = 0;
            if (_enemySystem != null && _playerTransform != null)
            {
                var enemies = _enemySystem.GetEnemies();
                activeEnemyCount = enemies != null ? enemies.Count : 0;
                if (enemies != null)
                {
                    Vector3 ppos = _playerTransform.position;
                    float bestSq = float.MaxValue;
                    BaseEnemy nearest = null;
                    for (int i = 0; i < enemies.Count; i++)
                    {
                        var e = enemies[i];
                        if (e == null) continue;
                        float d = (e.transform.position - ppos).sqrMagnitude;
                        if (d < bestSq) { bestSq = d; nearest = e; }
                    }
                    if (nearest != null)
                    {
                        nearestEnemyKey = nearest.Key;
                        nearestEnemyDistance = Mathf.Sqrt(bestSq);
                    }
                }
            }

            int playNo = CurrentPlayNo;
            SendLog(playNo, "episode.cause", cause);
            SendLog(playNo, "episode.duration_sec", duration);
            SendLog(playNo, "episode.final_level", finalLevel);
            SendLog(playNo, "episode.final_exp", finalExp);
            SendLog(playNo, "episode.total_gold", totalGold);
            SendLog(playNo, "episode.total_kills", totalKills);
            SendLog(playNo, "episode.total_damage_taken", _episodeDamageTaken);
            SendLog(playNo, "episode.nearest_enemy_key", nearestEnemyKey);
            SendLog(playNo, "episode.nearest_enemy_distance", nearestEnemyDistance);
            SendLog(playNo, "episode.active_enemy_count", activeEnemyCount);
            SendLog(playNo, "episode.recent_5s_damage_taken", _recent5sDamageTaken);
            SendLog(playNo, "episode.last_hit_kind", _lastDamageTakenAttackKind);
            SendLog(playNo, "episode.last_hit_attacker", _lastDamageTakenSourceKey);

            Debug.Log("[MLLogger] Episode " + _episodeIndex + " flushed: kills=" + totalKills
                + " level=" + finalLevel + " duration=" + duration.ToString("F1") + "s");

            _episodeIndex++;
        }

        private void FixedUpdate()
        {
            if (!_episodeActive) return;

            float now = Time.realtimeSinceStartup;

            // recent_5s_damage_taken 슬라이딩 — 5초 경과 시 reset
            if (now - _recent5sStartTime >= 5f)
            {
                _recent5sDamageTaken = 0f;
                _recent5sStartTime = now;
            }

            // 레벨업 감지 → player.level 전송
            if (_stats != null)
            {
                int curLevel = _stats.GetValue("Level");
                if (curLevel > _prevLevel)
                {
                    SendLog(CurrentPlayNo, "player.level", curLevel);
                    _prevLevel = curLevel;
                }
            }
        }

        // ============= ISink =============

        public void OnDamageDealt(UnityEngine.Object enemy, int damage, string sourceSkillKey)
        {
            if (!_episodeActive) return;
            SendLog(CurrentPlayNo, "event.damage_dealt", damage);
        }

        public void OnDamageTaken(int damage, string attackerEnemyKey, string attackKind)
        {
            if (!_episodeActive) return;
            _episodeDamageTaken += damage;
            _recent5sDamageTaken += damage;
            _lastDamageTakenSourceKey = string.IsNullOrEmpty(attackerEnemyKey) ? "unknown" : attackerEnemyKey;
            _lastDamageTakenAttackKind = string.IsNullOrEmpty(attackKind) ? "unknown" : attackKind;

            int playNo = CurrentPlayNo;
            SendLog(playNo, "event.damage_taken", damage);
            if (_stats != null) SendLog(playNo, "player.hp", _stats.GetValue("HP"));
        }

        public void OnEnemyDeath(UnityEngine.Object enemy, string lastSourceSkillKey)
        {
            if (!_episodeActive) return;
            SendLog(CurrentPlayNo, "event.enemy_death", 1);
        }

        public void OnEnemySpawn(string enemyKey, Vector3 position, float gameTimeSec, float intensity)
        {
            if (!_episodeActive) return;
            SendLog(CurrentPlayNo, "event.enemy_spawn.intensity", intensity);
        }

        // ============= 채널 핸들러 =============

        private void OnSkillChosen(string key)
        {
            if (!_episodeActive) return;
            // 단조 신규/업그레이드 구분은 어렵지만 이전 레벨 비교 없이 단순 보고:
            // 동일 key가 반복되면 dashboard에서 upgrade로 해석 가능.
            SendLog(CurrentPlayNo, "event.skill_chosen", key);
        }

        private void OnLevelUp()
        {
            // FixedUpdate가 _stats를 polling해서 player.level을 전송하므로 여기는 no-op.
        }

        private void OnTreasureOpened()
        {
            if (!_episodeActive) return;
            SendLog(CurrentPlayNo, "event.treasure_opened", 1);
        }

        // ============= 전송 헬퍼 =============

        private void SendLog(int playNo, string key, object value)
        {
            if (_client == null) return;
            _client.Log(playNo, key, value);
        }
    }
}
