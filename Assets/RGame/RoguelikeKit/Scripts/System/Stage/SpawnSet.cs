#region

using System;
using System.Collections.Generic;
using RGame.Framework;
using UnityEngine;

#endregion

namespace RGame.RoguelikeKit
{
    namespace RGame.RoguelikeKit
    {
        /// <summary>
        /// Ways enemies can be spawned for a SpawnSet.
        /// </summary>
        public enum SpawnPatternType
        {
            Random, // scattered random positions just outside camera view
            Circle, // Circular spawn, forming a surrounding circle
            Dense    // Dense spawn, Each spawn will always be concentrated around a single point.
        }

        /// <summary>
        /// Single enemy entry identified by its PoolKey; prefab resolved via PoolRuntime.
        /// </summary>
        [Serializable]
        public class EnemySpawnInfo
        {
            [Tooltip("Pool key for object pooling")] public string PoolKey;
            [Tooltip("Relative weight when randomly picking")] public float Weight = 1f;
        }

        /// <summary>
        /// Group of enemies plus spawn behaviour definition.
        /// Designers create one SpawnSet asset per monster group.
        /// </summary>
        [CreateAssetMenu(menuName = "RGame/RoguelikeKit/Stage/Spawn Set", fileName = "SpawnSet")]
        public class SpawnSet : DescriptionBaseSO
        {
            [Tooltip("Enemy entries included in this set")] public List<EnemySpawnInfo> Entries = new();
            
            [Header("Pattern")]
            [Tooltip("Spawn pattern for this set")] public SpawnPatternType Pattern = SpawnPatternType.Random;
            
            [Tooltip("Spawn budget per second before difficulty multipliers(only used when Pattern = Random)")] public float BaseRatePerSecond = 5f;
            [Tooltip("Spawn Enemy Count (only used when Pattern = Circle/Dense)")] public int Count = 1;
            [Tooltip("Radius for circle spawn (only used when Pattern = Circle)")] public float CircleRadius = 8f;
            
            [Min(0)] public float StartTime = 0f;
            [Min(0)] public float EndTime   = 60f;
            
            [Tooltip("0-1 multiplier curve over this segment")] public AnimationCurve IntensityCurve = AnimationCurve.Linear(0, 1, 1, 1);

        }
    }
}