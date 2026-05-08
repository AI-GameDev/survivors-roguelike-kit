#region

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

#endregion

namespace RGame.RoguelikeKit
{
    [CreateAssetMenu(fileName = "Item", menuName = "RGame/RoguelikeKit/Level/Item")]
    public class ItemData : ScriptableObject
    {
        public AttackSkillAttributeMod baseSkillAttribute = new();
        public List<AttackSkillAttributeMod> UpgradeAttribute = new();
    }
}