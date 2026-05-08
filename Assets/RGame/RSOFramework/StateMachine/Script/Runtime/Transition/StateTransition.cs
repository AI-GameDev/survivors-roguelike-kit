using System;
using System.Collections.Generic;
using UnityEngine;

namespace RGame.ScriptableCoreKit
{
    /// <summary>
    ///     Enum defining how multiple transition conditions should be evaluated
    /// </summary>
    public enum TransitionMode
    {
        /// <summary>
        ///     Any single condition being true will trigger the transition
        /// </summary>
        Any,

        /// <summary>
        ///     All conditions must be true to trigger the transition
        /// </summary>
        All
    }

    [System.Serializable]
    public class StateTransition
    {
        [SerializeField] private ActionStateNode targetState;

        [SerializeField] private TransitionMode mode = TransitionMode.All;

        [SerializeReference] private List<BaseTransition> transitions = new List<BaseTransition>();

        public StateTransition(ActionStateNode targetState, TransitionMode mode = TransitionMode.All)
        {
            this.targetState = targetState;
            this.mode = mode;
            this.transitions = new List<BaseTransition>();
        }

        public List<BaseTransition> Transitions
        {
            get
            {
                if (transitions == null)
                    transitions = new List<BaseTransition>();
                return transitions;
            }
        }

        public ActionStateNode TargetState
        {
            get => targetState;
            set => targetState = value;
        }

        public TransitionMode Mode
        {
            get => mode;
            set => mode = value;
        }

        public void AddTransition(BaseTransition transition)
        {
            if (transition != null && !Transitions.Contains(transition))
            {
                Transitions.Add(transition);
            }
        }

        public void RemoveTransition(BaseTransition transition)
        {
            Transitions.Remove(transition);
        }

        public void ClearTransitions()
        {
            Transitions.Clear();
        }

        public bool CheckAndTransition(StateMachine stateMachine, float deltaTime)
        {
            if (stateMachine == null || targetState == null)
                return false;

            if (Transitions.Count == 0)
            {
                stateMachine.ChangeState(targetState);
                return true;
            }

            var shouldTransition = false;

            if (mode == TransitionMode.Any)
            {
                foreach (var transition in Transitions)
                {
                    if (transition != null && transition.ShouldTransition(deltaTime, stateMachine.MyStateMachineSO.CurrentState))
                    {
                        shouldTransition = true;
                        break;
                    }
                }
            }
            else
            {
                shouldTransition = true;
                foreach (var transition in Transitions)
                {
                    if (transition == null || !transition.ShouldTransition(deltaTime, stateMachine.MyStateMachineSO.CurrentState))
                    {
                        shouldTransition = false;
                        //Don't break to avoid subsequent node updates.
                        //break;
                    }
                }
                
                if (Transitions.Count > 0 && Transitions.TrueForAll(t => t == null))
                {
                    shouldTransition = false;
                }
            }

            if (shouldTransition)
            {
#if UNITY_EDITOR && DEBUG_TRANSITIONS
                Debug.Log($"Transitioning to {targetState.name}");
#endif
                stateMachine.ChangeState(targetState);
                return true;
            }

            return false;
        }

        public void ExitAllTransition()
        {
            foreach (var transition in Transitions)
            {
                if (transition != null)
                    transition.OnExit();
            }
        }

        public List<string> Validate()
        {
            var issues = new List<string>();

            if (targetState == null)
                issues.Add("Target state is null");

            for (int i = 0; i < Transitions.Count; i++)
            {
                if (Transitions[i] == null)
                    issues.Add($"Transition condition {i} is null");
            }

            return issues;
        }
    }
}