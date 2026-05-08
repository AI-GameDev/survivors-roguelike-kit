#region

using System;
using UnityEngine;

#endregion

namespace RGame.ScriptableCoreKit
{
    [System.Serializable]
    public class TimerTransition : BaseTransition
    {
        [Header("Timer Configuration")] 
        [SerializeField]
        private float duration = 1f;

        [SerializeField, HideInInspector]
        private float startTime;

        /// <summary>
        ///     Gets or sets the duration before transition triggers
        /// </summary>
        public float Duration 
        { 
            get => duration; 
            set => duration = Mathf.Max(0f, value); 
        }

        /// <summary>
        ///     Gets the current elapsed time
        /// </summary>
        public float ElapsedTime => startTime;

        /// <summary>
        ///     Gets the remaining time before transition
        /// </summary>
        public float RemainingTime => Mathf.Max(0f, duration - startTime);

        /// <summary>
        ///     Gets the progress as a percentage (0 to 1)
        /// </summary>
        public float Progress => duration > 0 ? Mathf.Clamp01(startTime / duration) : 1f;

        public override bool ShouldTransition(float deltaTime, ActionStateNode node = null)
        {
            startTime += deltaTime;
            return startTime >= duration;
        }

        public override void OnExit()
        {
            base.OnExit();
            startTime = 0f;
        }
    }
}