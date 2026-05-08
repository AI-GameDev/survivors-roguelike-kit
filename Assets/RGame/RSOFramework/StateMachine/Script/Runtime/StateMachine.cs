#region

using UnityEngine;

#endregion

namespace RGame.ScriptableCoreKit
{
    /// <summary>
    ///     State machine component that manages state transitions and updates
    /// </summary>
    public class StateMachine : MonoBehaviour
    {
        [Header("State Machine Configuration")] [SerializeField]
        private StateMachineSO stateMachineSO;

        public StateMachineSO MyStateMachineSO => stateMachineSO;

        private void Start()
        {
            stateMachineSO.OnStart(this);
        }

        private void Update()
        {
            stateMachineSO?.Update();
        }

        private void FixedUpdate()
        {
            stateMachineSO?.FixedUpdate();
        }

        /// <summary>
        ///     Changes to a new state, calling exit on current state and enter on new state
        /// </summary>
        /// <param name="newState">The new state to transition to</param>
        public void ChangeState(ActionStateNode newState)
        {
            stateMachineSO.ChangeState(newState);
        }

        /// <summary>
        ///     Gets a value from the blackboard
        /// </summary>
        public T GetBlackboardValue<T>(string key, T fallback = default)
        {
            return stateMachineSO.blackboardTable.Get(key, fallback) ?? fallback;
        }

        /// <summary>
        ///     Sets a value in the blackboard
        /// </summary>
        public void SetBlackboardValue<T>(string key, T value)
        {
            stateMachineSO.blackboardTable.Set(key, value);
        }
    }
}