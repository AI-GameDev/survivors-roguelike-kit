namespace RGame.ScriptableCoreKit
{
    /// <summary>
    ///     Abstract base class for state transition conditions
    /// </summary>
    [System.Serializable]
    public abstract class BaseTransition
    {
        /// <summary>
        ///     Evaluates the transition condition. This method is called every frame during state updates.
        /// </summary>
        /// <returns>True if the transition condition is met, false otherwise</returns>
        public abstract bool ShouldTransition(float deltaTime, ActionStateNode node = null);

        /// <summary>
        ///     Called when the transition is exited or reset
        /// </summary>
        public virtual void OnExit()
        {
        }
    }
}