using System.Collections.Generic;
using RGame.Framework;
using UnityEngine;

namespace RGame.RoguelikeKit
{
    /// <summary>
    /// Event channel for skill UI upgrades
    /// </summary>
    [CreateAssetMenu(menuName = "RGame/RoguelikeKit/Events/Skill/Upgrade Skill UI Channel")]
    public class UpgradeSkillUIChannel : ScriptableEventT<List<SkillDataSO>>
    {
    }
}