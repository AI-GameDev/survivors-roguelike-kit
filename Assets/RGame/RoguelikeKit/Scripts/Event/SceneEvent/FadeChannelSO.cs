using RGame.Framework;
using UnityEngine;
using UnityEngine.Events;

namespace RGame.RoguelikeKit
{
    /// <summary>
    /// Event channel for screen fade effects
    /// </summary>
    [CreateAssetMenu(menuName = "RGame/RoguelikeKit/Events/Scene/Fade Channel")]
    public class FadeChannelSO : DescriptionBaseSO
    {
        public UnityAction<bool, float, Color> OnEventRaised;

        public void FadeIn(float duration)
        {
            Fade(true, duration, Color.clear);
        }

        public void FadeOut(float duration)
        {
            Fade(false, duration, Color.black);
        }

        private void Fade(bool fadeIn, float duration, Color color)
        {
            OnEventRaised?.Invoke(fadeIn, duration, color);
        }
    }
}