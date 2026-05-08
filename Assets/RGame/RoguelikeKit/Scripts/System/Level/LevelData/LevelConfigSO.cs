#region

using RGame.Framework;
using UnityEngine;

#endregion

namespace RGame.RoguelikeKit
{
    [CreateAssetMenu(menuName = "RGame/RoguelikeKit/Level/LevelConfig")]
    public class LevelConfigSO : DescriptionBaseSO
    {
        public string LevelDescription;
        public Sprite PreviewImage;
        public MapConfigSO MapConfig;
        public StageConfigSO StageConfig;
    }
}