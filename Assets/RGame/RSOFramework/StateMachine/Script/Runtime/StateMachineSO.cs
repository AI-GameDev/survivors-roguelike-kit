#region

using System;
using System.Collections.Generic;
using System.Linq;
using RGame.Framework;
using UnityEngine;

#endregion

namespace RGame.ScriptableCoreKit
{
    public enum RunningTimerType
    {
        Update,
        FixedUpdate
    }

    [Serializable]
    public class BlackboardData
    {
        public Vector3 moveToPosition;
    }
    
    [System.Serializable]
    public class ViewState
    {
        public Vector3 position = Vector3.zero;
        public Vector3 scale = Vector3.one;
    }
    
    /// <summary>
    ///     State machine scriptable object that manages states, transitions, and blackboard data
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableCoreKit/StateMachine")]
    public class StateMachineSO : DescriptionBaseSO
    {
        [Header("State Machine Configuration")] 
        [SerializeField]
        private ActionStateNode entryState;

        [SerializeField]
        private ActionStateNode currentState;
        
        [SerializeField] 
        private StateMachineState machineState = StateMachineState.Stopped;
        
        [SerializeField] 
        private RunningTimerType runningTimerType = RunningTimerType.Update;

        [Header("Node Management")] 
        [SerializeField] 
        private List<ActionStateNode> states = new();

        [Header("Blackboard Integration")] 
        [SerializeField]
        private BlackboardData blackboardData = new();
        [SerializeField]
        public BlackboardTable blackboardTable;

        [SerializeField] 
        public ViewState viewState = new();

        // Runtime tracking
        private StateMachine ownerStateMachine;
        private float stateChangeTime;
        private readonly List<ActionStateNode> stateHistory = new();

        #region Runtime Properties

        public ActionStateNode EntryState 
        { 
            get => entryState; 
            set => entryState = value; 
        }
        
        public ActionStateNode CurrentState 
        { 
            get => currentState; 
            private set => currentState = value; 
        }
        
        /// <summary>
        ///     Gets all states in the state machine
        /// </summary>
        public List<ActionStateNode> States 
        { 
            get => states ??= new List<ActionStateNode>(); 
            set => states = value; 
        }

        /// <summary>
        ///     Gets the current state machine status
        /// </summary>
        public StateMachineState MachineState 
        { 
            get => machineState; 
            private set => machineState = value; 
        }

        /// <summary>
        ///     Gets the time since the last state change
        /// </summary>
        public float TimeSinceStateChange => Time.time - stateChangeTime;

        /// <summary>
        ///     Gets the previous state
        /// </summary>
        public ActionStateNode PreviousState { get; private set; }

        /// <summary>
        ///     Gets the state transition history
        /// </summary>
        public IReadOnlyList<ActionStateNode> StateHistory => stateHistory.AsReadOnly();

        #endregion

        #region Runtime Methods

        private void OnEnable()
        {
            if (states == null)
                states = new List<ActionStateNode>();
                
            if (blackboardData == null)
                blackboardData = new BlackboardData();
                
            if (viewState == null)
                viewState = new ViewState();
        }

        /// <summary>
        ///     Starts the state machine with the entry state
        /// </summary>
        public void OnStart(StateMachine stateMachine)
        {
            if (entryState == null)
            {
                Debug.LogWarning($"StateMachine '{name}' has no entry state defined!");
                machineState = StateMachineState.Error;
                return;
            }

            ownerStateMachine = stateMachine;

            machineState = StateMachineState.Running;
            ChangeState(entryState);
        }

        /// <summary>
        ///     Updates the current state and checks for transitions
        /// </summary>
        public void Update()
        {
            if (machineState != StateMachineState.Running || currentState == null || runningTimerType != RunningTimerType.Update)
                return;

            // Update current state
            currentState.OnUpdate(Time.deltaTime);
        }

        /// <summary>
        ///     Fixed update for the current state
        /// </summary>
        public void FixedUpdate()
        {
            if (machineState != StateMachineState.Running || currentState == null || runningTimerType != RunningTimerType.FixedUpdate)
                return;

            currentState.OnUpdate(Time.fixedDeltaTime);
        }

        /// <summary>
        ///     Changes to a new state
        /// </summary>
        /// <param name="newState">The state to transition to</param>
        /// <param name="forced">Whether this is a forced transition (bypasses conditions)</param>
        /// <returns>True if state change was successful</returns>
        public bool ChangeState(ActionStateNode newState, bool forced = false)
        {
            if (newState == null)
            {
                Debug.LogWarning($"Attempted to change to null state in StateMachine '{name}'");
                return false;
            }

            foreach (var stateTransition in newState.StateTransitions)
            foreach (var transition in stateTransition.Transitions)
              transition.OnExit();
          
            // Store previous state
            PreviousState = currentState;

            // Exit current state
            if (currentState != null) currentState.OnExit();

            currentState = newState;
            stateChangeTime = Time.time;

            // Enter new state
            if (ownerStateMachine != null) currentState.Initialize(ownerStateMachine);
            currentState.OnEnter();

            return true;
        }

        public void ValidateReferences()
        {
            if (states == null)
            {
                states = new List<ActionStateNode>();
                return;
            }

            // Remove null references
            states.RemoveAll(state => state == null);

            // Validate entry state reference
            if (entryState != null && !states.Contains(entryState))
            {
                entryState = null;
            }

            // Find entry state if not set
            if (entryState == null)
            {
                entryState = states.OfType<EntryStateNode>().FirstOrDefault();
            }
        }
        
        /// <summary>
        ///     Stops the state machine
        /// </summary>
        public void Stop()
        {
            if (currentState != null) currentState.OnExit();

            machineState = StateMachineState.Stopped;
            currentState = null;
        }

        /// <summary>
        ///     Pauses the state machine
        /// </summary>
        public void Pause()
        {
            if (machineState == StateMachineState.Running) machineState = StateMachineState.Paused;
        }

        /// <summary>
        ///     Resumes the state machine from pause
        /// </summary>
        public void Resume()
        {
            if (machineState == StateMachineState.Paused) machineState = StateMachineState.Running;
        }
        
        #endregion
    }

    /// <summary>
    ///     Enum representing the state machine's execution state
    /// </summary>
    public enum StateMachineState
    {
        Stopped,
        Running,
        Paused,
        Error
    }
}