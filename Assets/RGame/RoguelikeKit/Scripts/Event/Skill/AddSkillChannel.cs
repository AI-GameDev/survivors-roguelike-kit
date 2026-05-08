using System.Collections.Generic;
using RGame.Framework;
using UnityEngine;

namespace RGame.RoguelikeKit
{
    /// <summary>
    /// Event channel for adding new skills
    /// </summary>
    [CreateAssetMenu(menuName = "RGame/RoguelikeKit/Events/Skill/Add Skill Channel")]
    public class AddSkillChannel : ScriptableEventT<SkillDataSO>
    {
    }
}