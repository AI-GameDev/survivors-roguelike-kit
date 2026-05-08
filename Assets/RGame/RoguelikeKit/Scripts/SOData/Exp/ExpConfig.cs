using RGame.Framework;
using UnityEngine;

namespace RGame.RoguelikeKit
{
    /// <summary>
    /// Configuration for experience progression.
    /// Designers set a base XP for level 1 and an additional XP increment per level.
    /// </summary>
    [CreateAssetMenu(menuName = "RGame/RoguelikeKit/ExpConfig", fileName = "ExpConfig")]
    public class ExpConfig : DescriptionBaseSO
    {
        [Header("XP Progression Settings")]
        [Tooltip("XP required to reach level 1.")]
        [SerializeField] private int baseExperience = 100;

        [Tooltip("Additional XP added for each subsequent level (linear increment).")]
        [SerializeField] private int experienceIncrement = 50;

        /// <summary>
        /// Returns the XP needed to advance from the previous level to the given level.
        /// Level 1 returns baseExperience.
        /// </summary>
        /// <param name="level">Target level (must be >= 1).</param>
        public int GetExperienceForLevel(int level)
        {
            if (level < 1)
            {
                Debug.LogWarning($"Invalid level {level}. Must be >= 1. Returning 0.");
                return 0;
            }
            
            return baseExperience + (level - 1) * experienceIncrement;
        }
    }
}