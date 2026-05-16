using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using RGame.CommonStat;
using UnityEngine;
using RGame.Framework;
using RGame.MLAgents;
using RGame.RoguelikeKit.RGame.RoguelikeKit;
using Random = UnityEngine.Random;

namespace RGame.RoguelikeKit
{
    /// <summary>
    /// Reads a StageConfigSO asset and spawns enemies according to SpawnSegments / SpawnSets.
    /// Supports Random, Circle and Area patterns and curse-based spawn scaling.
    /// StageConfig.Segments are copied & sorted by StartTime at runtime (original asset is untouched).
    /// </summary>
    public class StageManager : MonoBehaviour
    {
        [Header("Context")]
        [SerializeField] private PoolRuntimeSO poolRuntime;
        [SerializeField] private GlobalConfigSO globalConfig;
        [SerializeField] private EnemySystem enemySystem;
        [SerializeField] private CommonStatRuntimeSO statRuntime;

        [Header("Events")]
        [SerializeField] private PlayerSpawnChannelSO playerSpawnSO;
        [SerializeField] private StartStageEventChannelSO startStageChannelSO;
        [SerializeField] private VoidEventChannelSO gameWinChannelSO;

        [Header("Spawn Distance (Random)")]
        [SerializeField] private float minSpawnRadius = 14f;
        [SerializeField] private float maxSpawnRadius = 16f;

        // Runtime state
        private List<SpawnSet> _spawnSets;          // sorted working copy
        private float _elapsed;
        private int _spawnSetIndex;
        private Transform _player;
        private int _aliveCount;
        private readonly Dictionary<SpawnSet, float> _accumulators = new();

        private void Awake()
        {
            DOTween.SetTweensCapacity(200, 200);
        }

        private void OnEnable()
        {
            playerSpawnSO.RegisterListener(OnPlayerSpawned);
            startStageChannelSO.RegisterListener(OnStageStart);
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            CleanupEnemies();
        }

        private void OnPlayerSpawned(Player player) => _player = player.transform;

        private void OnStageStart(RuntimeStageConfigSO runtime)
        {
            // Make a sorted copy of segments so designer order is irrelevant
            StageConfigSO cfg = runtime.SelectStageConfig;
            _spawnSets = new List<SpawnSet>(cfg.SpawnSets);
            _spawnSets.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));
            _elapsed = 0f;
            _spawnSetIndex = 0;
            _aliveCount = 0;
            _accumulators.Clear();
        }

        private void Update()
        {
            if (_spawnSets == null || _spawnSets.Count == 0) return;
            _elapsed += Time.deltaTime;

            while (_spawnSetIndex < _spawnSets.Count &&
                   _elapsed >= _spawnSets[_spawnSetIndex].StartTime)
            {
                switch (_spawnSets[_spawnSetIndex].Pattern)
                {
                    case SpawnPatternType.Random:
                        StartCoroutine(RunSegment(_spawnSets[_spawnSetIndex]));
                        break;
                    case SpawnPatternType.Circle:
                        CircleSpawnEnemy(_spawnSets[_spawnSetIndex],_spawnSets[_spawnSetIndex].Count);
                        break;
                    case SpawnPatternType.Dense:
                        DenseSpawnEnemy(_spawnSets[_spawnSetIndex],_spawnSets[_spawnSetIndex].Count);
                        break;
                }

                _spawnSetIndex++;
            }
        }

        private IEnumerator RunSegment(SpawnSet spawnSet)
        {
            SpawnSet set = spawnSet;
            if (!_accumulators.ContainsKey(set)) _accumulators[set] = 0f;

            while (_elapsed < spawnSet.EndTime)
            {
                float t01 = Mathf.InverseLerp(spawnSet.StartTime, spawnSet.EndTime, _elapsed);
                float intensity = spawnSet.IntensityCurve.Evaluate(t01);
                float curseMul = statRuntime != null ? statRuntime.GetValue("Curse") * 0.01f : 1f;

                float actualRate = set.BaseRatePerSecond * intensity * curseMul;
                float addBudget = actualRate * Time.deltaTime;
                _accumulators[set] += addBudget;

                while (_accumulators[set] >= 1f)
                {
                    RandomSpawnEnemy(set, intensity, actualRate);
                    _accumulators[set] -= 1f;
                }

                yield return null;
            }
        }

        private void RandomSpawnEnemy(SpawnSet set, float intensity = 1f, float actualRate = -1f)
        {
            EnemySpawnInfo info = PickRandom(set.Entries);
            if (info == null) return;

            GameObject go = poolRuntime.Request(info.PoolKey);
            go.transform.position = GetSpawnPosition();
            BaseEnemy enemy = go.GetComponent<BaseEnemy>();
            enemy.OnDeath += HandleEnemyDeath;
            enemySystem.AddEnemy(enemy);
            _aliveCount++;
            MLBalanceHook.NotifyEnemySpawn(info.PoolKey, go.transform.position, _elapsed, intensity, actualRate, set.name);
        }

        private void CircleSpawnEnemy(SpawnSet set, int count)
        {
            var enemies = GetCirclePosition(_player.position,set.CircleRadius, count);

            for (int i = 0; i < count; i++)
            {
                EnemySpawnInfo info = PickRandom(set.Entries);
                if (info == null) return;
                GameObject go = poolRuntime.Request(info.PoolKey);
                go.transform.position = enemies[i];
                BaseEnemy enemy = go.GetComponent<BaseEnemy>();
                enemy.OnDeath += HandleEnemyDeath;
                enemySystem.AddEnemy(enemy);
                MLBalanceHook.NotifyEnemySpawn(info.PoolKey, go.transform.position, _elapsed, 1f, -1f, set.name);
            }
            _aliveCount += count;
        }

        private void DenseSpawnEnemy(SpawnSet set, int count)
        {
            var enemies = GetDensePosition(count);

            for (int i = 0; i < count; i++)
            {
                EnemySpawnInfo info = PickRandom(set.Entries);
                if (info == null) return;
                GameObject go = poolRuntime.Request(info.PoolKey);
                go.transform.position = enemies[i];
                BaseEnemy enemy = go.GetComponent<BaseEnemy>();
                enemy.OnDeath += HandleEnemyDeath;
                enemySystem.AddEnemy(enemy);
                MLBalanceHook.NotifyEnemySpawn(info.PoolKey, go.transform.position, _elapsed, 1f, -1f, set.name);
            }
            _aliveCount += count;
        }

        private void HandleEnemyDeath(BaseEnemy enemy)
        {
            if (!enemy) return;
            enemy.OnDeath -= HandleEnemyDeath;
            enemySystem.RemoveEnemy(enemy);
            _aliveCount--;
            poolRuntime.Return(enemy.gameObject);
            if (_aliveCount <= 0 && _spawnSetIndex >= _spawnSets.Count)
                gameWinChannelSO.RaiseEvent();
        }

        private Vector3 GetSpawnPosition()
        {
            Vector3 origin = _player ? _player.position : Vector3.zero;
            
            float angleR = Random.value * Mathf.PI * 2f;
            float radius = Random.Range(minSpawnRadius, maxSpawnRadius);
            Vector2 offset = new Vector2(Mathf.Cos(angleR), Mathf.Sin(angleR)) * radius;
            return origin + (Vector3)offset;
        }

        /// <summary>
        /// Returns evenly spaced points on a circle's circumference.
        /// </summary>
        /// <param name="center">World-space center position (x,y).</param>
        /// <param name="radius">Distance from center to each point.</param>
        /// <param name="count">Number of points (≥ 1).</param>
        /// <returns>List of Vector3 (z = 0) ordered counter-clockwise.</returns>
        private List<Vector3> GetCirclePosition(Vector2 center, float radius, int count)
        {
            var points = new List<Vector3>(count);
            if (count <= 0 || radius <= 0f) return points;

            float step = Mathf.PI * 2f / count;
            for (int i = 0; i < count; i++)
            {
                float angle = i * step;
                Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                points.Add(new Vector3(center.x + offset.x, center.y + offset.y, 0f));
            }
            return points;
        }

        private List<Vector3> GetDensePosition(int count)
        {
            Vector3 origin = _player ? _player.position : Vector3.zero;
            var list = new List<Vector3>(count);
            
            float angleR = Random.value * Mathf.PI * 2f;
            Vector2 center = new Vector2(Mathf.Cos(angleR), Mathf.Sin(angleR)) * maxSpawnRadius;
            
            const float spacing = 0.3f;
            
            int side = Mathf.CeilToInt(Mathf.Sqrt(count));
            float half = (side - 1) * 0.5f * spacing;

            for (int i = 0; i < count; i++)
            {
                int row = i / side;
                int col = i % side;
                
                float x = center.x + (col * spacing) - half;
                float y = center.y + (row * spacing) - half;

                list.Add(origin + new Vector3(x, y, 0f));
            }
            return list;
        }
        
        private static EnemySpawnInfo PickRandom(List<EnemySpawnInfo> list)
        {
            if (list == null || list.Count == 0) return null;
            float total = 0f;
            foreach (var e in list) total += Mathf.Max(0.0001f, e.Weight);
            float r = Random.value * total;
            float cum = 0f;
            foreach (var e in list)
            {
                cum += Mathf.Max(0.0001f, e.Weight);
                if (r <= cum) return e;
            }
            return list[0];
        }

        private void CleanupEnemies()
        {
            foreach (BaseEnemy e in enemySystem.GetEnemies())
            {
                if (e) poolRuntime.Return(e.gameObject);
            }
            enemySystem.ClearEnemies();
            _aliveCount = 0;
        }
    }
}
