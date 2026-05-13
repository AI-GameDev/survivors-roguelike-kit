#region

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
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
    ///     출력: ml-training/balance-logs/&lt;run_id&gt;.jsonl (1 episode = 1 line).
    /// </summary>
    public class MLBalanceLogger : MonoBehaviour, MLBalanceHook.ISink
    {
        private const float SPAWN_BIN_SECONDS = 5f;

        // Dependencies (주입)
        private CommonStatRuntimeSO _stats;
        private GlobalConfigSO _globalConfig;
        private StringEventChannelSO _upgradeSkillChannel;
        private VoidEventChannelSO _levelUpChannel;
        private VoidEventChannelSO _openTreasureChannel;
        private VoidEventChannelSO _gameOverChannel;
        private string _modelName;

        // 출력
        private string _runId;
        private string _outputPath;
        private int _episodeIndex;

        // 에피소드 상태
        private bool _episodeActive;
        private float _episodeStartTime;
        private int _episodeStartGold;
        private int _episodeStartKills;

        private int _prevHp;
        private int _prevLevel;

        private readonly List<LevelEvent> _levelEvents = new List<LevelEvent>();
        private readonly List<SkillEvent> _skillEvents = new List<SkillEvent>();
        private readonly List<DamageTakenEvent> _damageTakenEvents = new List<DamageTakenEvent>();
        private readonly List<SpawnEvent> _spawnEvents = new List<SpawnEvent>();

        // (skillKey -> (enemyKey -> total damage))
        private readonly Dictionary<string, Dictionary<string, int>> _damageDealt =
            new Dictionary<string, Dictionary<string, int>>();
        // (skillKey -> (enemyKey -> kill count))
        private readonly Dictionary<string, Dictionary<string, int>> _killsBySkill =
            new Dictionary<string, Dictionary<string, int>>();
        // (enemyKey -> kill count) — Tier B 보조
        private readonly Dictionary<string, int> _killsByEnemy = new Dictionary<string, int>();
        // (enemyKey -> (attackKind -> incoming damage))
        private readonly Dictionary<string, Dictionary<string, int>> _damageTaken =
            new Dictionary<string, Dictionary<string, int>>();

        // 5초 bin
        private readonly List<BinSnapshot> _bins = new List<BinSnapshot>();
        private BinSnapshot _currentBin;
        private float _currentBinStartTime;
        private int _activeEnemySamples;
        private int _activeEnemySumOverBin;

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

            _runId = string.Format(CultureInfo.InvariantCulture,
                "{0}_{1:yyyyMMdd_HHmmss}", _modelName, DateTime.Now);

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string logDir = Path.Combine(projectRoot, "ml-training", "balance-logs");
            Directory.CreateDirectory(logDir);
            _outputPath = Path.Combine(logDir, _runId + ".jsonl");

            Debug.Log("[MLLogger] run_id=" + _runId + " output=" + _outputPath);
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

        // 에피소드 시작 — Bootstrap이 PlayerSpawn 후 호출.
        public void BeginEpisode()
        {
            _episodeActive = true;
            _episodeStartTime = Time.realtimeSinceStartup;
            _episodeStartGold = _globalConfig != null ? _globalConfig.CurrentGetGold : 0;
            _episodeStartKills = _globalConfig != null ? _globalConfig.CurrentGetKill : 0;
            _prevHp = _stats != null ? _stats.GetValue("HP") : 0;
            _prevLevel = _stats != null ? _stats.GetValue("Level") : 1;

            _levelEvents.Clear();
            _skillEvents.Clear();
            _damageTakenEvents.Clear();
            _spawnEvents.Clear();
            _damageDealt.Clear();
            _killsBySkill.Clear();
            _killsByEnemy.Clear();
            _damageTaken.Clear();
            _bins.Clear();
            _currentBin = new BinSnapshot { TSec = 0f };
            _currentBinStartTime = _episodeStartTime;
            _activeEnemySamples = 0;
            _activeEnemySumOverBin = 0;
            _lastDamageTakenSourceKey = null;
            _lastDamageTakenAttackKind = null;
            _recent5sDamageTaken = 0f;
            _recent5sStartTime = _episodeStartTime;

            Debug.Log("[MLLogger] BeginEpisode #" + _episodeIndex);
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

            // 진행 중인 bin 마감
            CloseCurrentBin();

            float endTime = Time.realtimeSinceStartup;
            float duration = endTime - _episodeStartTime;

            int finalLevel = _stats != null ? _stats.GetValue("Level") : 0;
            int finalExp = _stats != null ? _stats.GetValue("Exp") : 0;
            int totalKills = (_globalConfig != null ? _globalConfig.CurrentGetKill : 0) - _episodeStartKills;
            int totalGold = (_globalConfig != null ? _globalConfig.CurrentGetGold : 0) - _episodeStartGold;

            int totalDamageTaken = 0;
            foreach (var ev in _damageTakenEvents) totalDamageTaken += ev.Delta;

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

            // 최종 스킬 보유 — reflection
            var finalSkills = ReadFinalSkills();

            string json = BuildEpisodeJson(
                cause, duration, finalLevel, finalExp, totalGold, totalKills, totalDamageTaken,
                nearestEnemyKey, nearestEnemyDistance, activeEnemyCount, finalSkills);

            try
            {
                File.AppendAllText(_outputPath, json + "\n");
                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "[MLLogger] Episode {0} flushed: kills={1} level={2} duration={3:F1}s",
                    _episodeIndex, totalKills, finalLevel, duration));
            }
            catch (Exception e)
            {
                Debug.LogError("[MLLogger] Failed to append JSONL: " + e.Message);
            }

            _episodeIndex++;
        }

        private void FixedUpdate()
        {
            if (!_episodeActive) return;

            // 활성 적 수 샘플링 (bin avg 용)
            if (_enemySystem != null)
            {
                var enemies = _enemySystem.GetEnemies();
                _activeEnemySumOverBin += enemies != null ? enemies.Count : 0;
                _activeEnemySamples++;
            }

            // 5초 bin 마감 체크
            float now = Time.realtimeSinceStartup;
            if (now - _currentBinStartTime >= SPAWN_BIN_SECONDS)
            {
                CloseCurrentBin();
                _currentBin = new BinSnapshot { TSec = now - _episodeStartTime };
                _currentBinStartTime = now;
            }

            // recent_5s_damage_taken 슬라이딩 — 5초 경과 시 reset
            if (now - _recent5sStartTime >= 5f)
            {
                _recent5sDamageTaken = 0f;
                _recent5sStartTime = now;
            }

            // HP 변화로 데미지 받음을 추적 (Tier C에서도 보강 — but Tier C가 권위)
            if (_stats != null)
            {
                int curHp = _stats.GetValue("HP");
                int curLevel = _stats.GetValue("Level");
                if (curLevel > _prevLevel)
                {
                    _levelEvents.Add(new LevelEvent
                    {
                        Level = curLevel,
                        TSec = now - _episodeStartTime,
                        Step = Time.frameCount
                    });
                    _prevLevel = curLevel;
                }
                _prevHp = curHp;
            }
        }

        private void CloseCurrentBin()
        {
            float avgEnemies = _activeEnemySamples > 0
                ? (float)_activeEnemySumOverBin / _activeEnemySamples
                : 0f;
            _currentBin.ActiveEnemiesAvg = avgEnemies;
            _bins.Add(_currentBin);
            _activeEnemySamples = 0;
            _activeEnemySumOverBin = 0;
        }

        // ============= ISink =============

        public void OnDamageDealt(UnityEngine.Object enemy, int damage, string sourceSkillKey)
        {
            if (!_episodeActive) return;
            string skillKey = string.IsNullOrEmpty(sourceSkillKey) ? "unknown" : sourceSkillKey;
            string enemyKey = ResolveEnemyKey(enemy);
            AccumulateMatrix(_damageDealt, skillKey, enemyKey, damage);
            _currentBin.DamageDealt += damage;
        }

        public void OnDamageTaken(int damage, string attackerEnemyKey, string attackKind)
        {
            if (!_episodeActive) return;
            string enemyKey = string.IsNullOrEmpty(attackerEnemyKey) ? "unknown" : attackerEnemyKey;
            string kind = string.IsNullOrEmpty(attackKind) ? "unknown" : attackKind;
            AccumulateMatrix(_damageTaken, enemyKey, kind, damage);
            _damageTakenEvents.Add(new DamageTakenEvent
            {
                TSec = Time.realtimeSinceStartup - _episodeStartTime,
                Delta = damage,
                HpAfter = _stats != null ? _stats.GetValue("HP") : 0
            });
            _currentBin.DamageTaken += damage;
            _recent5sDamageTaken += damage;
            _lastDamageTakenSourceKey = enemyKey;
            _lastDamageTakenAttackKind = kind;
        }

        public void OnEnemyDeath(UnityEngine.Object enemy, string lastSourceSkillKey)
        {
            if (!_episodeActive) return;
            string enemyKey = ResolveEnemyKey(enemy);
            string skillKey = string.IsNullOrEmpty(lastSourceSkillKey) ? "unknown" : lastSourceSkillKey;

            if (!_killsByEnemy.ContainsKey(enemyKey)) _killsByEnemy[enemyKey] = 0;
            _killsByEnemy[enemyKey]++;

            AccumulateMatrix(_killsBySkill, skillKey, enemyKey, 1);
            _currentBin.Kills++;
        }

        public void OnEnemySpawn(string enemyKey, Vector3 position, float gameTimeSec, float intensity)
        {
            if (!_episodeActive) return;
            _spawnEvents.Add(new SpawnEvent
            {
                TSec = Time.realtimeSinceStartup - _episodeStartTime,
                Key = enemyKey,
                Intensity = intensity
            });
        }

        // ============= 채널 핸들러 =============

        private void OnSkillChosen(string key)
        {
            if (!_episodeActive) return;
            int levelAfter = ResolveCurrentSkillLevel(key);
            _skillEvents.Add(new SkillEvent
            {
                TSec = Time.realtimeSinceStartup - _episodeStartTime,
                Type = levelAfter <= 1 ? "add" : "upgrade",
                Key = key,
                LevelAfter = levelAfter
            });
        }

        private void OnLevelUp()
        {
            // FixedUpdate가 _stats를 polling해서 level_progression을 채우므로 여기는 logging만.
        }

        private void OnTreasureOpened()
        {
            if (!_episodeActive) return;
            _skillEvents.Add(new SkillEvent
            {
                TSec = Time.realtimeSinceStartup - _episodeStartTime,
                Type = "treasure_opened",
                Key = null,
                LevelAfter = 0
            });
        }

        // ============= 헬퍼 =============

        private static string ResolveEnemyKey(UnityEngine.Object obj)
        {
            if (obj == null) return "unknown";
            if (obj is EnemyHit eh)
            {
                var be = eh.GetComponentInParent<BaseEnemy>();
                if (be != null) return string.IsNullOrEmpty(be.Key) ? be.GetType().Name : be.Key;
            }
            if (obj is BaseEnemy b)
            {
                return string.IsNullOrEmpty(b.Key) ? b.GetType().Name : b.Key;
            }
            return obj.GetType().Name;
        }

        private static void AccumulateMatrix(
            Dictionary<string, Dictionary<string, int>> matrix, string row, string col, int value)
        {
            if (!matrix.TryGetValue(row, out var inner))
            {
                inner = new Dictionary<string, int>();
                matrix[row] = inner;
            }
            inner.TryGetValue(col, out int prev);
            inner[col] = prev + value;
        }

        private int ResolveCurrentSkillLevel(string skillKey)
        {
            var skillMgr = UnityEngine.Object.FindFirstObjectByType<SkillManager>(FindObjectsInactive.Include);
            if (skillMgr == null) return 0;
            var bf = BindingFlags.NonPublic | BindingFlags.Instance;
            var dict = typeof(SkillManager).GetField("_skillDataDictionary", bf)?.GetValue(skillMgr)
                as Dictionary<string, SkillDataSO>;
            if (dict != null && dict.TryGetValue(skillKey, out var so)) return so.Level;
            return 0;
        }

        private List<FinalSkill> ReadFinalSkills()
        {
            var result = new List<FinalSkill>();
            var skillMgr = UnityEngine.Object.FindFirstObjectByType<SkillManager>(FindObjectsInactive.Include);
            if (skillMgr == null) return result;
            var bf = BindingFlags.NonPublic | BindingFlags.Instance;
            var dict = typeof(SkillManager).GetField("_skillDataDictionary", bf)?.GetValue(skillMgr)
                as Dictionary<string, SkillDataSO>;
            var activeSet = typeof(SkillManager).GetField("_activeSkills", bf)?.GetValue(skillMgr)
                as HashSet<string>;
            if (dict == null) return result;
            foreach (var kv in dict)
            {
                if (kv.Value.Level <= 0 && (activeSet == null || !activeSet.Contains(kv.Key))) continue;
                result.Add(new FinalSkill
                {
                    Key = kv.Key,
                    Level = kv.Value.Level,
                    Damage = kv.Value.Damage,
                    Cd = kv.Value.CD
                });
            }
            return result;
        }

        // ============= JSON 직렬화 (수동) =============

        private string BuildEpisodeJson(
            string cause, float duration, int finalLevel, int finalExp,
            int totalGold, int totalKills, int totalDamageTaken,
            string nearestEnemyKey, float nearestEnemyDistance, int activeEnemyCount,
            List<FinalSkill> finalSkills)
        {
            var sb = new StringBuilder(8192);
            sb.Append("{");
            JKV(sb, "schema_version", 2); sb.Append(',');
            JKV(sb, "run_id", _runId); sb.Append(',');
            JKV(sb, "model", _modelName); sb.Append(',');
            JKV(sb, "episode_index", _episodeIndex); sb.Append(',');
            JKV(sb, "started_at", DateTime.Now.AddSeconds(-duration).ToString("o", CultureInfo.InvariantCulture)); sb.Append(',');
            JKV(sb, "ended_at", DateTime.Now.ToString("o", CultureInfo.InvariantCulture)); sb.Append(',');

            // outcome
            sb.Append("\"outcome\":{");
            JKV(sb, "cause", cause); sb.Append(',');
            JKV(sb, "duration_real_sec", duration); sb.Append(',');
            JKV(sb, "final_level", finalLevel); sb.Append(',');
            JKV(sb, "final_exp", finalExp); sb.Append(',');
            JKV(sb, "total_gold", totalGold); sb.Append(',');
            JKV(sb, "total_kills", totalKills); sb.Append(',');
            JKV(sb, "total_damage_taken", totalDamageTaken);
            sb.Append("},");

            // level_progression
            sb.Append("\"level_progression\":[");
            for (int i = 0; i < _levelEvents.Count; i++)
            {
                if (i > 0) sb.Append(',');
                var ev = _levelEvents[i];
                sb.Append("{");
                JKV(sb, "level", ev.Level); sb.Append(',');
                JKV(sb, "t_sec", ev.TSec); sb.Append(',');
                JKV(sb, "step", ev.Step);
                sb.Append("}");
            }
            sb.Append("],");

            // skill_events
            sb.Append("\"skill_events\":[");
            for (int i = 0; i < _skillEvents.Count; i++)
            {
                if (i > 0) sb.Append(',');
                var ev = _skillEvents[i];
                sb.Append("{");
                JKV(sb, "t_sec", ev.TSec); sb.Append(',');
                JKV(sb, "type", ev.Type);
                if (ev.Key != null) { sb.Append(','); JKV(sb, "key", ev.Key); }
                if (ev.LevelAfter > 0) { sb.Append(','); JKV(sb, "level_after", ev.LevelAfter); }
                sb.Append("}");
            }
            sb.Append("],");

            // final_skills
            sb.Append("\"final_skills\":[");
            for (int i = 0; i < finalSkills.Count; i++)
            {
                if (i > 0) sb.Append(',');
                var s = finalSkills[i];
                sb.Append("{");
                JKV(sb, "key", s.Key); sb.Append(',');
                JKV(sb, "level", s.Level); sb.Append(',');
                JKV(sb, "damage", s.Damage); sb.Append(',');
                JKV(sb, "cd", s.Cd);
                sb.Append("}");
            }
            sb.Append("],");

            JNestedDict(sb, "damage_dealt_matrix", _damageDealt); sb.Append(',');
            JNestedDict(sb, "kills_by_enemy_and_skill", _killsBySkill); sb.Append(',');
            JFlatDict(sb, "kills_by_enemy", _killsByEnemy); sb.Append(',');
            JNestedDict(sb, "damage_taken_matrix", _damageTaken); sb.Append(',');

            // damage_taken_events
            sb.Append("\"damage_taken_events\":[");
            for (int i = 0; i < _damageTakenEvents.Count; i++)
            {
                if (i > 0) sb.Append(',');
                var ev = _damageTakenEvents[i];
                sb.Append("{");
                JKV(sb, "t_sec", ev.TSec); sb.Append(',');
                JKV(sb, "delta", ev.Delta); sb.Append(',');
                JKV(sb, "hp_after", ev.HpAfter);
                sb.Append("}");
            }
            sb.Append("],");

            // spawn_timeline
            sb.Append("\"spawn_timeline\":[");
            for (int i = 0; i < _spawnEvents.Count; i++)
            {
                if (i > 0) sb.Append(',');
                var ev = _spawnEvents[i];
                sb.Append("{");
                JKV(sb, "t_sec", ev.TSec); sb.Append(',');
                JKV(sb, "key", ev.Key); sb.Append(',');
                JKV(sb, "intensity", ev.Intensity);
                sb.Append("}");
            }
            sb.Append("],");

            // spawn_pressure_5s_bins
            sb.Append("\"spawn_pressure_5s_bins\":[");
            for (int i = 0; i < _bins.Count; i++)
            {
                if (i > 0) sb.Append(',');
                var b = _bins[i];
                sb.Append("{");
                JKV(sb, "t_sec", b.TSec); sb.Append(',');
                JKV(sb, "active_enemies_avg", b.ActiveEnemiesAvg); sb.Append(',');
                JKV(sb, "kills", b.Kills); sb.Append(',');
                JKV(sb, "damage_dealt", b.DamageDealt); sb.Append(',');
                JKV(sb, "damage_taken", b.DamageTaken);
                sb.Append("}");
            }
            sb.Append("],");

            // death_context
            sb.Append("\"death_context\":{");
            if (nearestEnemyKey != null) JKV(sb, "nearest_enemy_key", nearestEnemyKey);
            else sb.Append("\"nearest_enemy_key\":null");
            sb.Append(',');
            JKV(sb, "nearest_enemy_distance", nearestEnemyDistance); sb.Append(',');
            JKV(sb, "active_enemy_count", activeEnemyCount); sb.Append(',');
            JKV(sb, "recent_5s_damage_taken", _recent5sDamageTaken); sb.Append(',');
            if (_lastDamageTakenAttackKind != null) JKV(sb, "last_hit_kind", _lastDamageTakenAttackKind);
            else sb.Append("\"last_hit_kind\":null");
            sb.Append(',');
            if (_lastDamageTakenSourceKey != null) JKV(sb, "last_hit_attacker", _lastDamageTakenSourceKey);
            else sb.Append("\"last_hit_attacker\":null");
            sb.Append("}");

            sb.Append("}");
            return sb.ToString();
        }

        private static void JKV(StringBuilder sb, string key, string value)
        {
            sb.Append('"').Append(key).Append("\":");
            if (value == null) sb.Append("null"); else AppendString(sb, value);
        }
        private static void JKV(StringBuilder sb, string key, int value)
        {
            sb.Append('"').Append(key).Append("\":").Append(value.ToString(CultureInfo.InvariantCulture));
        }
        private static void JKV(StringBuilder sb, string key, float value)
        {
            sb.Append('"').Append(key).Append("\":");
            if (float.IsNaN(value) || float.IsInfinity(value)) sb.Append("null");
            else sb.Append(value.ToString("F4", CultureInfo.InvariantCulture));
        }
        private static void AppendString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ') sb.Append(string.Format(CultureInfo.InvariantCulture, "\\u{0:X4}", (int)c));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
        private static void JNestedDict(StringBuilder sb, string name, Dictionary<string, Dictionary<string, int>> d)
        {
            sb.Append('"').Append(name).Append("\":{");
            bool first = true;
            foreach (var kv in d)
            {
                if (!first) sb.Append(','); first = false;
                AppendString(sb, kv.Key); sb.Append(':');
                sb.Append('{');
                bool ifirst = true;
                foreach (var inner in kv.Value)
                {
                    if (!ifirst) sb.Append(','); ifirst = false;
                    AppendString(sb, inner.Key); sb.Append(':').Append(inner.Value.ToString(CultureInfo.InvariantCulture));
                }
                sb.Append('}');
            }
            sb.Append('}');
        }
        private static void JFlatDict(StringBuilder sb, string name, Dictionary<string, int> d)
        {
            sb.Append('"').Append(name).Append("\":{");
            bool first = true;
            foreach (var kv in d)
            {
                if (!first) sb.Append(','); first = false;
                AppendString(sb, kv.Key); sb.Append(':').Append(kv.Value.ToString(CultureInfo.InvariantCulture));
            }
            sb.Append('}');
        }

        // ============= 내부 데이터 타입 =============

        private struct LevelEvent { public int Level; public float TSec; public int Step; }
        private struct SkillEvent { public float TSec; public string Type; public string Key; public int LevelAfter; }
        private struct DamageTakenEvent { public float TSec; public int Delta; public int HpAfter; }
        private struct SpawnEvent { public float TSec; public string Key; public float Intensity; }
        private struct BinSnapshot
        {
            public float TSec;
            public float ActiveEnemiesAvg;
            public int Kills;
            public int DamageDealt;
            public int DamageTaken;
        }
        private struct FinalSkill { public string Key; public int Level; public int Damage; public float Cd; }
    }
}
