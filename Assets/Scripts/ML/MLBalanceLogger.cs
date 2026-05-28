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
        private float _lastEnemyCountPollTime;

        // 사망 컨텍스트
        private string _lastDamageTakenSourceKey;
        private string _lastDamageTakenAttackKind;
        private float _recent5sDamageTaken;
        private float _recent5sStartTime;

        // 게임 객체 참조 (사망 컨텍스트 폴링용)
        private Transform _playerTransform;
        private Rigidbody2D _playerRb;
        private EnemySystem _enemySystem;

        // v18 검증용: XP-방향 상관관계 폴링 (0.25초 주기 — sparse trigger 대비 sample 확보)
        private const float XP_ALIGN_OBS_RANGE = 12f;
        private const float XP_ALIGN_PROXIMITY_RANGE = 5f;
        private const float XP_ALIGN_VELOCITY_EPS = 0.1f;
        private const float XP_ALIGN_POLL_INTERVAL = 0.25f;
        private float _lastXpAlignPollTime;

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

        public void SetPlayer(Transform playerTransform, Rigidbody2D playerRb)
        {
            _playerTransform = playerTransform;
            _playerRb = playerRb;
        }

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
            _lastEnemyCountPollTime = _episodeStartTime;
            _lastXpAlignPollTime = _episodeStartTime;

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

            // 1초 주기로 stage.active_enemy_count 시계열 송신
            if (now - _lastEnemyCountPollTime >= 1f)
            {
                _lastEnemyCountPollTime = now;
                if (_enemySystem != null)
                {
                    var enemies = _enemySystem.GetEnemies();
                    int count = enemies != null ? enemies.Count : 0;
                    SendLog(CurrentPlayNo, "stage.active_enemy_count", count);
                }
            }

            // v18 검증: XP-방향 vs 이동 방향 코사인 + 컨텍스트 (0.25초 주기)
            if (now - _lastXpAlignPollTime >= XP_ALIGN_POLL_INTERVAL)
            {
                _lastXpAlignPollTime = now;
                PollXpAlignment();
            }
        }

        private void PollXpAlignment()
        {
            if (_playerTransform == null || _playerRb == null) return;

            Vector3 pos = _playerTransform.position;
            Vector2 vel = _playerRb.linearVelocity;

            // nearest XP/Drop gem 탐색 (SurvivorFighterAgent와 동일 범위/태그)
            Collider2D[] hits = Physics2D.OverlapCircleAll(pos, XP_ALIGN_OBS_RANGE);
            float bestDistSq = float.MaxValue;
            Vector2 bestDir = Vector2.zero;
            for (int i = 0; i < hits.Length; i++)
            {
                var h = hits[i];
                if (h == null) continue;
                if (!h.CompareTag("Exp") && !h.CompareTag("DropOut")) continue;
                Vector2 diff = (Vector2)(h.transform.position - pos);
                float dsq = diff.sqrMagnitude;
                if (dsq < bestDistSq) { bestDistSq = dsq; bestDir = diff; }
            }

            int playNo = CurrentPlayNo;
            bool inRange = bestDistSq < float.MaxValue && bestDistSq > 1e-6f;

            // 분모 데이터: 매 폴링 trigger 됐는지. 0/1 비율 = "XP 가까이 있는 시간 비율"
            SendLog(playNo, "agent.xp_in_range", inRange ? 1 : 0);

            if (!inRange) return;

            float dist = Mathf.Sqrt(bestDistSq);
            SendLog(playNo, "agent.nearest_xp_distance", dist);

            // velocity 너무 작으면 cos 의미 없음 — distance 만 로깅하고 종료
            if (vel.sqrMagnitude < XP_ALIGN_VELOCITY_EPS * XP_ALIGN_VELOCITY_EPS) return;

            Vector2 xpDir = bestDir / dist;
            Vector2 moveDir = vel.normalized;
            float cos = Vector2.Dot(xpDir, moveDir);

            // 위험 컨텍스트: PROXIMITY_RANGE(5m) 안 적 수
            int nearbyEnemies = 0;
            if (_enemySystem != null)
            {
                var nearby = _enemySystem.GetNearestEnemies(pos, XP_ALIGN_OBS_RANGE, XP_ALIGN_OBS_RANGE);
                for (int i = 0; i < nearby.Count; i++)
                {
                    if (nearby[i] == null) continue;
                    float d = Vector3.Distance(pos, nearby[i].transform.position);
                    if (d < XP_ALIGN_PROXIMITY_RANGE) nearbyEnemies++;
                }
            }

            SendLog(playNo, "agent.xp_align_cos", cos);
            SendLog(playNo, "agent.nearby_enemy_count", nearbyEnemies);
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

        public void OnEnemySpawn(string enemyKey, Vector3 position, float gameTimeSec, float intensity, float actualRate, string setKey)
        {
            if (!_episodeActive) return;
            int playNo = CurrentPlayNo;
            SendLog(playNo, "event.enemy_spawn.intensity", intensity);
            if (actualRate >= 0f) SendLog(playNo, "event.enemy_spawn.actual_rate", actualRate);
            if (!string.IsNullOrEmpty(setKey)) SendLog(playNo, "event.enemy_spawn.set_key", setKey);
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
