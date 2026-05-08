using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace RGame.RoguelikeKit
{
    [Serializable]
    public class AttackSkillAttributeMod
    {
        public UpgradeSkillAttributeType skillAttributeType;
        
        [Range(0, 99)] public int IntValue;
    }
}
