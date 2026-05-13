#region

using System.Collections.Generic;
using RGame.CommonStat;
using RGame.Framework;
using RGame.RoguelikeKit;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

#endregion

namespace RGame.MLAgents
{
    /// <summary>
    ///     Vampire Survivors-style ML-Agents agent for the Fighter character.
    ///     Movement-only policy (auto-attacking game). Persists across episodes via DontDestroyOnLoad.
    ///     Player reference is swapped each episode by MLAgentBootstrap on player spawn.
    /// </summary>
    [RequireComponent(typeof(Unity.MLAgents.Policies.BehaviorParameters))]
    public class SurvivorFighterAgent : Agent
    {
        // 11 base obs (vel.x, vel.y, hpRatio, levelNorm, stepProgress, top2 XP × 3) + 3 enemies × 6 = 29.
        private const int VECTOR_OBS_SIZE = 29;
        private const float ENEMY_HP_NORM = 200f;
        private const float ENEMY_ATTACK_NORM = 20f;
        private const float ENEMY_SPEED_NORM = 30f;
        private const int CONTINUOUS_ACTION_COUNT = 2;
        private const int TOP_ENEMY_COUNT = 3;
        private const int TOP_XP_COUNT = 2;
        private const float NEAREST_ENEMY_RANGE = 12f;
        private const float XP_OBS_RANGE = 12f;
        private const float STEP_REWARD = 0f;
        private const float HP_LOSS_REWARD_PER_POINT = -0.15f;
        private const float XP_GAIN_REWARD_SCALE = 1.0f;
        private const float DEATH_REWARD = -3.0f;
        private const float KILL_REWARD = 0.15f;
        private const float CLEAR_REWARD = 2.0f;
        // Threat penalty: -0.0005 × (enemyAtk / 5) × (1 - dist/8). 약적 Atk=5 → 1배, 강적 Atk=20 → 4배.
        private const float THREAT_BASE_PENALTY = -0.0005f;
        private const float THREAT_ATTACK_REF = 5f;
        private const float PROXIMITY_RANGE = 8f;

        private CommonStatRuntimeSO _stats;
        private EnemySystem _enemySystem;
        private GlobalConfigSO _globalConfig;
        private ExpConfig _expConfig;
        private VoidEventChannelSO _gameOverChannel;
        private VoidEventChannelSO _levelUpChannel;

        private Transform _playerTransform;
        private Rigidbody2D _playerRb;
        private Player _player;
        private bool _hasPlayer;
        private bool _channelsRegistered;

        private int _prevHp;
        private int _prevExp;
        private int _prevLevel;
        private int _prevEnemyCount;

        private int _episodeKills;
        private int _episodeStartStep;
        private float _lastDiagLogTime;
        private bool _clearedTimeout;

        public void Inject(
            CommonStatRuntimeSO stats,
            EnemySystem enemySystem,
            GlobalConfigSO globalConfig,
            ExpConfig expConfig,
            VoidEventChannelSO gameOverChannel,
            VoidEventChannelSO levelUpChannel)
        {
            _stats = stats;
            _enemySystem = enemySystem;
            _globalConfig = globalConfig;
            _expConfig = expConfig;
            _gameOverChannel = gameOverChannel;
            _levelUpChannel = levelUpChannel;
            RegisterChannels();
        }

        public void SetPlayer(Transform player, Rigidbody2D rb)
        {
            _playerTransform = player;
            _playerRb = rb;
            _hasPlayer = player != null;
            _player = (player != null) ? player.GetComponentInChildren<Player>() : null;

            if (_hasPlayer && _stats != null)
            {
                _prevHp = _stats.GetValue("HP");
                _prevExp = _stats.GetValue("Exp");
                _prevLevel = _stats.GetValue("Level");
            }
        }

        public override void Initialize()
        {
            // 5 min sim-time @ 50Hz FixedUpdate = 15000 steps. 도달 시 CLEAR_REWARD 후 종료.
            MaxStep = 15000;
        }

        public override void OnEpisodeBegin()
        {
            if (_hasPlayer && _stats != null)
            {
                _prevHp = _stats.GetValue("HP");
                _prevExp = _stats.GetValue("Exp");
                _prevLevel = _stats.GetValue("Level");
            }
            _prevEnemyCount = (_enemySystem != null) ? _enemySystem.GetEnemies().Count : 0;
            _episodeKills = 0;
            _episodeStartStep = StepCount;
            _clearedTimeout = false;
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            if (!_hasPlayer || _playerRb == null || _stats == null)
            {
                for (int i = 0; i < VECTOR_OBS_SIZE; i++) sensor.AddObservation(0f);
                return;
            }

            float moveSpeed = ComputeMoveSpeed();
            Vector2 vel = _playerRb.linearVelocity;
            sensor.AddObservation(Mathf.Clamp(moveSpeed > 0.001f ? vel.x / moveSpeed : 0f, -1f, 1f));
            sensor.AddObservation(Mathf.Clamp(moveSpeed > 0.001f ? vel.y / moveSpeed : 0f, -1f, 1f));

            int hp = _stats.GetValue("HP");
            int hpMax = _stats.GetMaxValue("HP");
            sensor.AddObservation(hpMax > 0 ? Mathf.Clamp01((float)hp / hpMax) : 0f);

            sensor.AddObservation(Mathf.Clamp01(_stats.GetValue("Level") / 30f));

            sensor.AddObservation(MaxStep > 0 ? Mathf.Clamp01((float)StepCount / MaxStep) : 0f);

            // Top 3 enemies (each: dist, dirX, dirY, hpRatio, attackNorm, speedNorm = 18 obs). 부족하면 (1,0,0,0,0,0)으로 패딩.
            Vector3 playerPos = _playerTransform != null ? _playerTransform.position : Vector3.zero;
            if (_enemySystem != null && _playerTransform != null)
            {
                List<BaseEnemy> nearest = _enemySystem.GetNearestEnemies(playerPos, NEAREST_ENEMY_RANGE, NEAREST_ENEMY_RANGE);
                int count = Mathf.Min(TOP_ENEMY_COUNT, nearest.Count);
                for (int i = 0; i < count; i++)
                {
                    if (nearest[i] == null)
                    {
                        sensor.AddObservation(1f); sensor.AddObservation(0f); sensor.AddObservation(0f);
                        sensor.AddObservation(0f); sensor.AddObservation(0f); sensor.AddObservation(0f);
                        continue;
                    }
                    Vector3 diff = nearest[i].transform.position - playerPos;
                    Vector2 diff2 = new Vector2(diff.x, diff.y);
                    float d = 1f, dx = 0f, dy = 0f;
                    if (diff2.sqrMagnitude > 1e-6f)
                    {
                        Vector2 n = diff2.normalized;
                        dx = n.x; dy = n.y;
                        d = Mathf.Clamp01(diff2.magnitude / NEAREST_ENEMY_RANGE);
                    }
                    float hpRatio = 0f, atkNorm = 0f, spdNorm = 0f;
                    var st = nearest[i].StatRuntime;
                    if (st != null)
                    {
                        hpRatio = Mathf.Clamp01((float)st.GetValue("HP") / ENEMY_HP_NORM);
                        atkNorm = Mathf.Clamp01((float)st.GetValue("Attack") / ENEMY_ATTACK_NORM);
                        spdNorm = Mathf.Clamp01((float)st.GetValue("Speed") / ENEMY_SPEED_NORM);
                    }
                    sensor.AddObservation(d);
                    sensor.AddObservation(dx);
                    sensor.AddObservation(dy);
                    sensor.AddObservation(hpRatio);
                    sensor.AddObservation(atkNorm);
                    sensor.AddObservation(spdNorm);
                }
                for (int i = count; i < TOP_ENEMY_COUNT; i++)
                {
                    sensor.AddObservation(1f); sensor.AddObservation(0f); sensor.AddObservation(0f);
                    sensor.AddObservation(0f); sensor.AddObservation(0f); sensor.AddObservation(0f);
                }
            }
            else
            {
                for (int i = 0; i < TOP_ENEMY_COUNT; i++)
                {
                    sensor.AddObservation(1f); sensor.AddObservation(0f); sensor.AddObservation(0f);
                    sensor.AddObservation(0f); sensor.AddObservation(0f); sensor.AddObservation(0f);
                }
            }

            // Top 2 XP/Drop (each: dist, dirX, dirY = 6 obs). Physics2D로 검색 후 거리 정렬.
            Collider2D[] hits = Physics2D.OverlapCircleAll(playerPos, XP_OBS_RANGE);
            // 간단 정렬: 거리 작은 순으로 TOP_XP_COUNT개만 추출 (selection)
            float[] bestDistSq = new float[TOP_XP_COUNT];
            Vector2[] bestDir = new Vector2[TOP_XP_COUNT];
            int found = 0;
            for (int k = 0; k < TOP_XP_COUNT; k++) bestDistSq[k] = float.MaxValue;
            for (int i = 0; i < hits.Length; i++)
            {
                var h = hits[i];
                if (h == null) continue;
                if (!h.CompareTag("Exp") && !h.CompareTag("DropOut")) continue;
                Vector2 diff = (Vector2)(h.transform.position - playerPos);
                float dsq = diff.sqrMagnitude;
                // insertion into top-K
                for (int k = 0; k < TOP_XP_COUNT; k++)
                {
                    if (dsq < bestDistSq[k])
                    {
                        for (int s = TOP_XP_COUNT - 1; s > k; s--) { bestDistSq[s] = bestDistSq[s - 1]; bestDir[s] = bestDir[s - 1]; }
                        bestDistSq[k] = dsq;
                        bestDir[k] = diff;
                        if (found < TOP_XP_COUNT) found++;
                        break;
                    }
                }
            }
            for (int i = 0; i < TOP_XP_COUNT; i++)
            {
                if (i < found && bestDistSq[i] < float.MaxValue)
                {
                    float mag = Mathf.Sqrt(bestDistSq[i]);
                    Vector2 n = mag > 1e-3f ? bestDir[i] / mag : Vector2.zero;
                    sensor.AddObservation(Mathf.Clamp01(mag / XP_OBS_RANGE));
                    sensor.AddObservation(n.x);
                    sensor.AddObservation(n.y);
                }
                else
                {
                    sensor.AddObservation(1f); sensor.AddObservation(0f); sensor.AddObservation(0f);
                }
            }
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            if (!_hasPlayer || _playerRb == null) return;

            float ax = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
            float ay = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);
            Vector2 input = new Vector2(ax, ay);
            if (input.sqrMagnitude > 1f) input = input.normalized;

            float moveSpeed = ComputeMoveSpeed();
            _playerRb.linearVelocity = input * moveSpeed;

            // MoveDirection-기반 스킬이 사방으로 발사되도록 직접 갱신
            // (PlayerMovement가 disabled이라 기본값 (1,0)에 고정되는 버그 수정).
            if (_player != null && input.sqrMagnitude > 0.0001f)
            {
                _player.MoveDirection = input.normalized;
            }

            // 1초당 1회 진단 로그 (이동 안 됨 디버깅용).
            if (Time.unscaledTime - _lastDiagLogTime > 1f)
            {
                _lastDiagLogTime = Time.unscaledTime;
                Debug.Log(string.Format(
                    "[Agent] act=({0:F2},{1:F2}) ms={2:F2} vel=({3:F2},{4:F2}) ts={5:F2}",
                    ax, ay, moveSpeed, _playerRb.linearVelocity.x, _playerRb.linearVelocity.y, Time.timeScale));
            }

            AddReward(STEP_REWARD);

            PollHpAndExpDeltas();
            ApplyProximityShaping();
            PollEnemyKills();
            CheckTimeoutClear();
        }

        private void CheckTimeoutClear()
        {
            if (_clearedTimeout || !_hasPlayer) return;
            int liveSteps = StepCount - _episodeStartStep;
            // MaxStep 자동 종료 직전에 CLEAR_REWARD 부여 + 게임 reload 트리거.
            if (liveSteps < MaxStep - 1) return;
            _clearedTimeout = true;
            AddReward(CLEAR_REWARD);
            int finalLevel = _stats != null ? _stats.GetValue("Level") : 0;
            Debug.Log(string.Format("[Episode] CLEAR kills={0} steps={1} level={2}", _episodeKills, liveSteps, finalLevel));
            // OnGameOverRaised가 fire되어 씬 리로드 + EndEpisode 수행 (DEATH_REWARD는 flag로 스킵).
            if (_gameOverChannel != null) _gameOverChannel.RaiseEvent();
        }

        private void ApplyProximityShaping()
        {
            if (_playerTransform == null || _enemySystem == null) return;
            Vector3 pos = _playerTransform.position;

            // Threat proximity: PROXIMITY_RANGE 이내 모든 적에 대해 (공격력/5) × (1 - 거리/range) 누적 페널티.
            // 강적(Attack 큼)일수록 + 가까울수록 큰 페널티 → 정책이 강적 회피를 학습.
            var nearest = _enemySystem.GetNearestEnemies(pos, PROXIMITY_RANGE, PROXIMITY_RANGE);
            float threatSum = 0f;
            for (int i = 0; i < nearest.Count; i++)
            {
                var e = nearest[i];
                if (e == null) continue;
                float d = Vector3.Distance(pos, e.transform.position);
                float close = 1f - Mathf.Clamp01(d / PROXIMITY_RANGE);
                if (close <= 0f) continue;
                float atk = THREAT_ATTACK_REF;
                var st = e.StatRuntime;
                if (st != null) atk = Mathf.Max(1f, (float)st.GetValue("Attack"));
                threatSum += (atk / THREAT_ATTACK_REF) * close;
            }
            if (threatSum > 0f)
            {
                AddReward(THREAT_BASE_PENALTY * threatSum);
            }
        }

        private void PollEnemyKills()
        {
            if (_enemySystem == null) return;
            int curCount = _enemySystem.GetEnemies().Count;
            int delta = _prevEnemyCount - curCount;
            // 정상 사망은 매 step 0~수 마리. 큰 양수(scene unload)나 음수(spawn) 무시.
            if (delta > 0 && delta <= 5)
            {
                AddReward(KILL_REWARD * delta);
                _episodeKills += delta;
            }
            _prevEnemyCount = curCount;
        }

        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var ca = actionsOut.ContinuousActions;
            ca[0] = Input.GetAxisRaw("Horizontal");
            ca[1] = Input.GetAxisRaw("Vertical");
        }

        private void PollHpAndExpDeltas()
        {
            if (_stats == null) return;

            int hp = _stats.GetValue("HP");
            if (hp < _prevHp)
            {
                int lost = _prevHp - hp;
                AddReward(HP_LOSS_REWARD_PER_POINT * lost);
            }
            _prevHp = hp;

            int level = _stats.GetValue("Level");
            int exp = _stats.GetValue("Exp");
            if (level == _prevLevel && exp > _prevExp)
            {
                int gained = exp - _prevExp;
                int threshold = _expConfig != null ? _expConfig.GetExperienceForLevel(level + 1) : 100;
                if (threshold < 1) threshold = 1;
                AddReward(XP_GAIN_REWARD_SCALE * Mathf.Clamp01(gained / (float)threshold));
            }
            _prevExp = exp;
            _prevLevel = level;
        }

        private float ComputeMoveSpeed()
        {
            if (_stats == null || _globalConfig == null) return 5f;
            return _stats.GetValue("MoveSpeed") * _globalConfig.MoveSpeedBalanceFactor * 0.5f;
        }

        private void LateUpdate()
        {
            if (_hasPlayer && _playerTransform != null)
            {
                transform.position = _playerTransform.position;
            }
        }

        private void RegisterChannels()
        {
            if (_channelsRegistered) return;
            if (_gameOverChannel != null) _gameOverChannel.RegisterListener(OnGameOverRaised);
            if (_levelUpChannel != null) _levelUpChannel.RegisterListener(OnLevelUpRaised);
            _channelsRegistered = true;
        }

        private void OnDestroy()
        {
            if (_channelsRegistered)
            {
                if (_gameOverChannel != null) _gameOverChannel.UnregisterListener(OnGameOverRaised);
                if (_levelUpChannel != null) _levelUpChannel.UnregisterListener(OnLevelUpRaised);
                _channelsRegistered = false;
            }
        }

        private void OnGameOverRaised()
        {
            int liveSteps = StepCount - _episodeStartStep;
            int finalLevel = _stats != null ? _stats.GetValue("Level") : 0;
            if (_clearedTimeout)
            {
                // 타임아웃으로 우리가 직접 raise한 케이스: DEATH 페널티 없이 reload만 진행.
                Debug.Log(string.Format("[Episode] kills={0} steps={1} level={2} (timeout-clear)", _episodeKills, liveSteps, finalLevel));
            }
            else
            {
                Debug.Log(string.Format("[Episode] kills={0} steps={1} level={2}", _episodeKills, liveSteps, finalLevel));
                AddReward(DEATH_REWARD);
            }
            EndEpisode();
            _hasPlayer = false;
            _playerRb = null;
            _playerTransform = null;
            _player = null;
        }

        private void OnLevelUpRaised()
        {
            // 레벨업 보상은 XP_GAIN_REWARD_SCALE(=1.0) 누적으로 흡수. 이벤트는 listener만 유지.
        }
    }
}
