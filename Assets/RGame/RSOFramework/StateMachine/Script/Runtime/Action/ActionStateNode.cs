using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RGame.ScriptableCoreKit
{
    /// <summary>
    ///     Abstract base class for all states in the state machine
    ///     Enhanced with editor support and cloning capabilities
    /// </summary>
    public abstract class ActionStateNode : ScriptableObject
    {
        [Header("State Identification")] 
        [HideInInspector]
        public string guid = "";

        [HideInInspector] 
        public Vector2 position = Vector2.zero;

        [Header("UI State")] 
        [HideInInspector] 
        public bool isNodeCollapsed;

        [HideInInspector] 
        public bool isPreviewCollapsed;

        [HideInInspector] 
        public bool isFinish;
        
        [Header("State Transitions")]
        [SerializeReference, HideInInspector] 
        private List<StateTransition> stateTransitions = new List<StateTransition>();
        
        /// <summary>
        ///     Constructor initializes the state transitions list
        /// </summary>
        private void OnEnable()
        {
            if (stateTransitions == null)
                stateTransitions = new List<StateTransition>();

            if (string.IsNullOrEmpty(guid))
            {
#if UNITY_EDITOR
                guid = GUID.Generate().ToString();
#else
                guid = System.Guid.NewGuid().ToString();
#endif
            }
        }

        /// <summary>
        ///     Gets the total time this state has been running (Update timer)
        /// </summary>
        public float UpdateTimer { get; private set; }

        /// <summary>
        ///     Gets the total time this state has been running (FixedUpdate timer)
        /// </summary>
        public float FixedUpdateTimer { get; private set; }

        /// <summary>
        ///     Gets the time when this state was entered
        /// </summary>
        public float EnterTime { get; private set; }

        /// <summary>
        ///     Gets the time since this state was entered
        /// </summary>
        public float TimeInState => Time.time - EnterTime;

        /// <summary>
        ///     Gets the StateMachine that owns this state
        /// </summary>
        public StateMachine StateMachine { get; private set; }

        /// <summary>
        ///     Gets the Transform of the StateMachine GameObject
        /// </summary>
        public Transform transform => StateMachine?.transform;

        /// <summary>
        ///     Gets the list of state transitions
        /// </summary>
        public List<StateTransition> StateTransitions 
        { 
            get 
            { 
                if (stateTransitions == null)
                    stateTransitions = new List<StateTransition>();
                return stateTransitions; 
            } 
        }
        
        /// <summary>
        ///     Gets whether this state has been initialized
        /// </summary>
        public bool IsInitialized { get; private set; }

        /// <summary>
        ///     Called when the state is entered
        /// </summary>
        public virtual void OnEnter()
        {
            UpdateTimer = 0f;
            FixedUpdateTimer = 0f;
            EnterTime = Time.time;
        }

        /// <summary>
        ///     Internal method to initialize the state with its parent StateMachine
        /// </summary>
        /// <param name="stateMachine">The StateMachine that owns this state</param>
        public void Initialize(StateMachine stateMachine)
        {
            this.StateMachine = stateMachine;
            IsInitialized = true;
            
            if (stateTransitions == null)
            {
                stateTransitions = new List<StateTransition>();
                Debug.LogWarning($"StateTransitions was null for {name}, creating new list");
            }

            OnInitialize();
        }

        /// <summary>
        /// Called every frame while the state is active
        /// </summary>
        public virtual void OnUpdate(float deltaTime)
        {
            UpdateTimer += deltaTime;
            
            if (StateTransitions != null)
            {
                foreach (var stateTransition in StateTransitions) 
                {
                    if (stateTransition != null)
                    {
                        stateTransition.CheckAndTransition(StateMachine, deltaTime);
                    }
                }
            }
        }

        /// <summary>
        /// Called when the state is exited
        /// </summary>
        public virtual void OnExit()
        {
        }
        
        /// <summary>
        ///     Called when the state is initialized with a StateMachine
        ///     Override this for custom initialization logic
        /// </summary>
        protected virtual void OnInitialize()
        {
        }

        /// <summary>
        ///     Adds a state transition to this state
        /// </summary>
        /// <param name="stateTransition">The state transition to add</param>
        public void AddStateTransition(StateTransition stateTransition)
        {
            if (stateTransition != null && !StateTransitions.Contains(stateTransition)) 
            {
                StateTransitions.Add(stateTransition);
#if UNITY_EDITOR
                EditorUtility.SetDirty(this);
#endif
            }
        }

        /// <summary>
        ///     Removes a state transition from this state
        /// </summary>
        /// <param name="stateTransition">The state transition to remove</param>
        public bool RemoveStateTransition(StateTransition stateTransition)
        {
            if (StateTransitions.Remove(stateTransition))
            {
#if UNITY_EDITOR
                EditorUtility.SetDirty(this);
#endif
                return true;
            }
            return false;
        }

        /// <summary>
        /// Removes all transitions that target a specific state
        /// </summary>
        /// <param name="targetState">The target state to remove transitions for</param>
        public void RemoveTransitionsToState(ActionStateNode targetState)
        {
            var transitionsToRemove = StateTransitions
                .Where(t => t.TargetState == targetState)
                .ToList();
    
            foreach (var transition in transitionsToRemove)
            {
                StateTransitions.Remove(transition);
            }
    
            if (transitionsToRemove.Count > 0)
            {
#if UNITY_EDITOR
                EditorUtility.SetDirty(this);
#endif
            }
        }
        
        /// <summary>
        /// Clears all state transitions
        /// </summary>
        public void ClearStateTransitions()
        {
            if (StateTransitions.Count > 0)
            {
                StateTransitions.Clear();
#if UNITY_EDITOR
                EditorUtility.SetDirty(this);
#endif
            }
        }

        /// <summary>
        ///     Gets a component from the StateMachine GameObject
        /// </summary>
        /// <typeparam name="T">The component type to retrieve</typeparam>
        /// <returns>The component if found, null otherwise</returns>
        public T GetComponent<T>() where T : Component
        {
            return StateMachine?.GetComponent<T>();
        }

        /// <summary>
        ///     Gets a component from a child GameObject of the StateMachine
        /// </summary>
        /// <typeparam name="T">The component type to retrieve</typeparam>
        /// <returns>The component if found, null otherwise</returns>
        public T GetComponentInChildren<T>() where T : Component
        {
            return StateMachine?.GetComponentInChildren<T>();
        }

        /// <summary>
        ///     Gets a component from a parent GameObject of the StateMachine
        /// </summary>
        /// <typeparam name="T">The component type to retrieve</typeparam>
        /// <returns>The component if found, null otherwise</returns>
        public T GetComponentInParent<T>() where T : Component
        {
            return StateMachine?.GetComponentInParent<T>();
        }
        
        /// <summary>
        ///     Gets the name of this state for debugging purposes
        /// </summary>
        /// <returns>The name of the state class</returns>
        public virtual string GetStateName()
        {
            return GetType().Name;
        }

        /// <summary>
        ///     Gets a display name for the state (removes common suffixes)
        /// </summary>
        /// <returns>Formatted display name</returns>
        public virtual string GetDisplayName()
        {
            var stateName = GetStateName();

            if (stateName.EndsWith("State"))
                stateName = stateName.Substring(0, stateName.Length - 5);
            if (stateName.EndsWith("Node"))
                stateName = stateName.Substring(0, stateName.Length - 4);

            var formattedName = "";
            for (var i = 0; i < stateName.Length; i++)
            {
                if (i > 0 && char.IsUpper(stateName[i]) && !char.IsUpper(stateName[i - 1])) 
                    formattedName += " ";
                formattedName += stateName[i];
            }

            return formattedName;
        }

        public void FinishNode()
        {
            isFinish = true;
        }
        
        /// <summary>
        ///     Creates a clone of this state for runtime use
        /// </summary>
        /// <returns>Cloned state instance</returns>
        public virtual ActionStateNode Clone()
        {
            var cloned = Instantiate(this);

            cloned.UpdateTimer = 0f;
            cloned.FixedUpdateTimer = 0f;
            cloned.EnterTime = 0f;
            cloned.StateMachine = null;
            cloned.IsInitialized = false;

            cloned.stateTransitions = new List<StateTransition>();
            foreach (var transition in StateTransitions)
            {
                if (transition != null)
                {
                    cloned.stateTransitions.Add(transition);
                }
            }

            return cloned;
        }

        /// <summary>
        ///     Validates this state's configuration
        /// </summary>
        /// <returns>Validation issues if any</returns>
        public virtual List<string> Validate()
        {
            var issues = new List<string>();

            if (string.IsNullOrEmpty(name)) 
                issues.Add("State name is empty");

            return issues;
        }

        /// <summary>
        ///     Gets blackboard value with type safety
        /// </summary>
        /// <typeparam name="T">Type of value to retrieve</typeparam>
        /// <param name="key">Blackboard key</param>
        /// <param name="fallback">Fallback value if key not found</param>
        /// <returns>Retrieved value or fallback</returns>
        protected T GetBlackboardValue<T>(string key, T fallback = default)
        {
            return StateMachine.GetBlackboardValue(key, fallback) ?? fallback;
        }

        /// <summary>
        ///     Sets blackboard value with type safety
        /// </summary>
        /// <typeparam name="T">Type of value to set</typeparam>
        /// <param name="key">Blackboard key</param>
        /// <param name="value">Value to set</param>
        protected void SetBlackboardValue<T>(string key, T value)
        {
            StateMachine?.SetBlackboardValue(key, value);
        }
    }
}