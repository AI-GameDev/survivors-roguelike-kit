using System.Collections.Generic;
using RGame.Framework;
using UnityEngine;

namespace RGame.RoguelikeKit
{
    /// <summary>
    /// Event channel for skill upgrades
    /// </summary>
    [CreateAssetMenu(menuName = "RGame/RoguelikeKit/Events/Skill/Upgrade Skill Channel")]
    public class UpgradeSkillChannel : ScriptableEventT<SkillDataSO>
    {
    }
}