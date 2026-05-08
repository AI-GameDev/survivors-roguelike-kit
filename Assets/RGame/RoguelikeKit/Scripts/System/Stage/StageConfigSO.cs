using System;
using System.Collections.Generic;
using RGame.Framework;
using RGame.RoguelikeKit.RGame.RoguelikeKit;
using UnityEngine;

namespace RGame.RoguelikeKit
{
    /// <summary>
    /// Full asset driving enemy spawning for a stage.
    /// </summary>
    [CreateAssetMenu(menuName = "RGame/RoguelikeKit/Stage/StageConfigSO", fileName = "StageConfig")]
    public class StageConfigSO : DescriptionBaseSO
    {
        public List<SpawnSet> SpawnSets = new();
    }
}
