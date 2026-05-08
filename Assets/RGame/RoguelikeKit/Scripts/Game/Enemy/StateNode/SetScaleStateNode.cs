using UnityEngine;

namespace RGame.ScriptableCoreKit
{
    /// <summary>
    /// State node that sets the transform scale when entered
    /// </summary>
    public class SetScaleStateNode : ActionStateNode
    {
        [Header("Scale Settings")]
        [Tooltip("Target scale to set when entering this state")]
        public Vector3 targetScale = Vector3.one;
        
        [Tooltip("Whether to use relative scale (multiply current scale) or absolute scale")]
        public bool useRelativeScale = false;
        
        /// <summary>
        /// Called when entering the state - sets the scale
        /// </summary>
        public override void OnEnter()
        {
            base.OnEnter();
            
            if (transform == null)
            {
                Debug.LogWarning($"SetScaleStateNode '{name}': Transform is null, cannot set scale");
                return;
            }
            
            // Calculate and apply the target scale
            Vector3 newScale;
            if (useRelativeScale)
            {
                newScale = Vector3.Scale(transform.localScale, targetScale);
            }
            else
            {
                newScale = targetScale;
            }
            
            transform.localScale = newScale;
        }
        
        /// <summary>
        /// Gets display name for the state
        /// </summary>
        public override string GetDisplayName()
        {
            return "Set Scale";
        }
        
        /// <summary>
        /// Validates the state configuration
        /// </summary>
        public override System.Collections.Generic.List<string> Validate()
        {
            var issues = base.Validate();
            
            if (targetScale.x == 0f || targetScale.y == 0f || targetScale.z == 0f)
            {
                issues.Add("Target scale contains zero values, which may cause rendering issues");
            }
            
            return issues;
        }
        
        /// <summary>
        /// Creates a clone of this state for runtime use
        /// </summary>
        public override ActionStateNode Clone()
        {
            return (SetScaleStateNode)base.Clone();
        }
    }
}