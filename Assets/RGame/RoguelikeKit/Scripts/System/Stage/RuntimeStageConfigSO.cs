#region

using RGame.Framework;
using UnityEngine;

#endregion

namespace RGame.RoguelikeKit
{
    [CreateAssetMenu(menuName = "RGame/RoguelikeKit/Wave/SelectStageConfig")]
    public class RuntimeStageConfigSO : DescriptionBaseSO
    {
        public StageConfigSO SelectStageConfig;
        public Transform EnemyParent;
    }
}