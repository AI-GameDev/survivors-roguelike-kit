using RGame.ScriptableCoreKit;
using UnityEngine;

namespace RGame.RoguelikeKit
{
   /// <summary>
    /// Transition condition based on distance to player
    /// </summary>
    [System.Serializable]
    public class DistanceToPlayerTransition : BaseTransition
    {
        [Header("Distance Settings")]
        [SerializeField] private float targetDistance = 5f;
        [SerializeField] private ComparisonType comparisonType = ComparisonType.LessThan;
        [SerializeField] private bool usePlayerTag = true;
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private GlobalConfigSO globalConfigSO;
        
        public enum ComparisonType
        {
            LessThan,           // Distance < targetDistance
            LessThanOrEqual,    // Distance <= targetDistance  
            GreaterThan,        // Distance > targetDistance
            GreaterThanOrEqual, // Distance >= targetDistance
            Equal               // Distance == targetDistance (with tolerance)
        }
        
        [Header("Equal Comparison Settings")]
        [SerializeField] private float equalityTolerance = 0.5f;
        
        private Transform playerTransform;
        
        /// <summary>
        /// Evaluates whether the distance condition is met
        /// </summary>
        public override bool ShouldTransition(float deltaTime, ActionStateNode node = null)
        {
            if (playerTransform == null)
            {
                playerTransform = globalConfigSO.GlobalPlayer.transform;
            }

            if (playerTransform == null || node == null) return false;
            
            // Calculate distance
            float currentDistance = Vector3.Distance(node.transform.position, playerTransform.position);
            
            // Evaluate condition based on comparison type
            return EvaluateDistance(currentDistance);
        }
        
        private bool EvaluateDistance(float currentDistance)
        {
            switch (comparisonType)
            {
                case ComparisonType.LessThan:
                    return currentDistance < targetDistance;
                    
                case ComparisonType.LessThanOrEqual:
                    return currentDistance <= targetDistance;
                    
                case ComparisonType.GreaterThan:
                    return currentDistance > targetDistance;
                    
                case ComparisonType.GreaterThanOrEqual:
                    return currentDistance >= targetDistance;
                    
                case ComparisonType.Equal:
                    return Mathf.Abs(currentDistance - targetDistance) <= equalityTolerance;
                    
                default:
                    return false;
            }
        }
        
        /// <summary>
        /// Gets the current distance to player (for debugging)
        /// </summary>
        public float GetCurrentDistance(Transform fromTransform)
        {
            if (fromTransform == null || playerTransform == null)
                return float.MaxValue;
                
            return Vector3.Distance(fromTransform.position, playerTransform.position);
        }
        
        /// <summary>
        /// Sets the target distance programmatically
        /// </summary>
        public void SetTargetDistance(float distance)
        {
            targetDistance = distance;
        }
        
        /// <summary>
        /// Sets the comparison type programmatically
        /// </summary>
        public void SetComparisonType(ComparisonType type)
        {
            comparisonType = type;
        }
    }
}
