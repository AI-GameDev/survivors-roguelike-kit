using RGame.CommonStat;
using RGame.ScriptableCoreKit;
using UnityEngine;

namespace RGame.RoguelikeKit
{
    /// <summary>
    /// Advanced movement state with teleport capability
    /// </summary>
    public class TeleportMovementState : ActionStateNode
    {
        [Header("Movement Settings")]
        [SerializeField] private float moveSpeedMultiplier = 1f;
        [SerializeField] private bool flipSpriteBasedOnDirection = true;
        
        [Header("Teleport Settings")]
        [SerializeField] private float triggerDistance = 16f;
        [SerializeField] private float teleportDistance = 8f;
        [SerializeField] private float teleportCooldown = 3f;
        [SerializeField] private float teleportArcAngle = 45f;
        [SerializeField] private GlobalConfigSO globalConfig;
        
        private BaseEnemy enemy;
        private Transform playerTransform;
        private Rigidbody2D rigidBody;
        private CommonStatRuntimeSO statRuntime;
        private float teleportCooldownTimer;
        
        public override void OnEnter()
        {
            base.OnEnter();
            
            enemy = GetComponent<BaseEnemy>();
            if (enemy != null)
            {
                rigidBody = enemy.MyRigidbody2D;
                statRuntime = enemy.StatRuntime;
            }
            
            // Find player
            GameObject player = globalConfig.GlobalPlayer.gameObject;
            if (player != null)
                playerTransform = player.transform;
                
            isFinish = false;
            teleportCooldownTimer = 0f;
        }
        
        public override void OnUpdate(float deltaTime)
        {
            base.OnUpdate(deltaTime);
            
            if (enemy == null || rigidBody == null || playerTransform == null)
                return;
                
            // Update cooldown timer
            if (teleportCooldownTimer > 0f)
                teleportCooldownTimer -= deltaTime;
                
            PerformMovement();
        }
        
        private void PerformMovement()
        {
            Vector3 playerPosition = playerTransform.position;
            Vector3 currentPosition = transform.position;
            float distanceToPlayer = Vector3.Distance(currentPosition, playerPosition);
            
            // Check if should teleport
            if (distanceToPlayer > triggerDistance && teleportCooldownTimer <= 0f)
            {
                TeleportToPlayer();
                teleportCooldownTimer = teleportCooldown;
            }
            else
            {
                // Normal movement behavior
                NormalMovement(playerPosition, currentPosition);
            }
        }
        
        private void NormalMovement(Vector3 playerPosition, Vector3 currentPosition)
        {
            Vector3 direction = (playerPosition - currentPosition).normalized;
            float baseSpeed = statRuntime?.GetValue("Speed") ?? 5f;
            float finalSpeed = baseSpeed * moveSpeedMultiplier;
            
            rigidBody.linearVelocity = direction * finalSpeed;
            
            // Flip sprite based on movement direction
            if (flipSpriteBasedOnDirection && direction.x != 0)
            {
                float scaleX = Mathf.Sign(direction.x) * Mathf.Abs(transform.localScale.x);
                transform.localScale = new Vector3(scaleX, transform.localScale.y, transform.localScale.z);
            }
        }
        
        private void TeleportToPlayer()
        {
            Vector3 playerPosition = playerTransform.position;
            
            // Get player movement direction (if available)
            Vector2 playerMoveDirection = Vector2.zero;
            
            // Try to get player movement from a player controller component
            var playerController = playerTransform.GetComponent<Rigidbody2D>();
            if (playerController != null)
                playerMoveDirection = playerController.linearVelocity.normalized;
            
            // If player is not moving, use random direction
            if (playerMoveDirection.magnitude < 0.1f)
            {
                float randomAngle = Random.Range(0f, 360f);
                playerMoveDirection = new Vector2(
                    Mathf.Cos(randomAngle * Mathf.Deg2Rad),
                    Mathf.Sin(randomAngle * Mathf.Deg2Rad)
                );
            }
            
            // Add random angle variation within the arc
            float randomAngleOffset = Random.Range(-teleportArcAngle / 2f, teleportArcAngle / 2f);
            float baseAngle = Mathf.Atan2(playerMoveDirection.y, playerMoveDirection.x) * Mathf.Rad2Deg;
            float finalAngle = (baseAngle + randomAngleOffset) * Mathf.Deg2Rad;
            
            // Calculate final teleport direction
            Vector2 teleportDirection = new Vector2(Mathf.Cos(finalAngle), Mathf.Sin(finalAngle));
            
            // Calculate teleport position
            Vector3 teleportPosition = playerPosition + (Vector3)(teleportDirection * teleportDistance);
            
            // Apply teleport
            transform.position = teleportPosition;
            
            // Stop movement briefly after teleport
            rigidBody.linearVelocity = Vector2.zero;
            
            // Optional: Add teleport effect
            OnTeleport(teleportPosition);
        }
        
        /// <summary>
        /// Called when enemy teleports. Override this to add visual/audio effects.
        /// </summary>
        /// <param name="teleportPosition">The position enemy teleported to</param>
        protected virtual void OnTeleport(Vector3 teleportPosition)
        {
            // Override in derived classes to add teleport effects
            // Example: spawn particles, play sound, etc.
        }
        
        public override void OnExit()
        {
            base.OnExit();
            
            // Stop movement when exiting state
            if (rigidBody != null)
                rigidBody.linearVelocity = Vector2.zero;
        }
        
        public override string GetDisplayName()
        {
            return "Teleport Movement";
        }
    }
}
