#region

using UnityEngine;

#endregion

namespace Game503
{
    [CreateAssetMenu(fileName = "UpgradeItem", menuName = "RGame/RoguelikeKit/Level/UpgradeItem")]
    public class UpgradeItemData : ScriptableObject
    {
        public Sprite Icon;
        public string ItemName;
        public string Description;
    }
}