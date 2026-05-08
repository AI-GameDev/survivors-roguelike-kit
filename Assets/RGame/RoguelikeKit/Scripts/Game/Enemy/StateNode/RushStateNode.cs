using UnityEngine;
using DG.Tweening;
using RGame.CommonStat;
using RGame.RoguelikeKit;

namespace RGame.ScriptableCoreKit
{
    /// <summary>
    /// Survivor rush state: grows for 1s, rushes toward player, then shrinks for 1s
    /// </summary>
    public class RushStateNode : ActionStateNode
    {
        [Header("Rush Settings")]
        [SerializeField] private float growDuration = 1f;
        [SerializeField] private float rushSpeed = 15f;
        [SerializeField] private float shrinkDuration = 1f;
        [SerializeField] private float scaleMultiplier = 1.5f;
        [SerializeField] private GlobalConfigSO globalConfig;
        
        private BaseEnemy enemy;
        private Vector3 originalScale;
        private Vector3 rushDirection;
        private bool isGrowing = true;
        private bool isRushing = false;
        private bool isShrinking = false;
        private Sequence rushSequence;
        
        public override void OnEnter()
        {
            base.OnEnter();
            
            enemy = GetComponent<BaseEnemy>();
            if (enemy == null) return;
            
            originalScale = transform.localScale;
            isFinish = false;
            isGrowing = true;
            isRushing = false;
            isShrinking = false;
            
            // Stop any movement during initial grow phase
            enemy.MyRigidbody2D.linearVelocity = Vector2.zero;
            
            StartRushSequence();
        }
        
        private void StartRushSequence()
        {
            rushSequence = DOTween.Sequence();
            
            // Phase 1: Grow for 1 second (no movement)
            rushSequence.Append(transform.DOScale(originalScale * scaleMultiplier, growDuration))
                .OnComplete(() => {
                    isGrowing = false;
                    RecordPlayerDirection();
                    StartRush();
                });
        }
        
        private void RecordPlayerDirection()
        {
            // Record direction to player when grow phase completes
            var playerTransform = globalConfig.GlobalPlayer.transform;
            if (playerTransform != null)
            {
                rushDirection = (playerTransform.position - transform.position).normalized;
            }
            else
            {
                rushDirection = Vector3.right; // Default direction if player not found
            }
        }
        
        private void StartRush()
        {
            isRushing = true;
            
            // Apply rush velocity
            enemy.MyRigidbody2D.linearVelocity = rushDirection * rushSpeed;
            
            // Wait a moment then start shrinking
            rushSequence.AppendInterval(0.1f)
                .AppendCallback(() => {
                    isRushing = false;
                    isShrinking = true;
                    enemy.MyRigidbody2D.linearVelocity = Vector2.zero;
                })
                .Append(transform.DOScale(originalScale, shrinkDuration))
                .OnComplete(() => {
                    isShrinking = false;
                    FinishNode();
                });
        }
        
        public override void OnExit()
        {
            base.OnExit();
            
            // Clean up DOTween sequence
            rushSequence?.Kill();
            
            // Reset scale and velocity
            if (transform != null)
                transform.localScale = originalScale;
                
            if (enemy?.MyRigidbody2D != null)
                enemy.MyRigidbody2D.linearVelocity = Vector2.zero;
        }
        
        public override string GetDisplayName()
        {
            return "Survivor Rush";
        }
    }
}