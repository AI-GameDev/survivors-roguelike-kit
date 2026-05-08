#region

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RGame.CommonStat;
using RGame.Framework;
using UnityEngine;
using UnityEngine.Events;

#endregion

namespace RGame.RoguelikeKit
{
    /// <summary>
    /// Enemy that engages in melee attacks by chasing the player continuously.
    /// Enhanced with instant teleport mechanics when too far from player.
    /// </summary>
    public class MeleeEnemy : BaseEnemy
    {
        [Header("Teleport Settings")]
        [SerializeField] private float teleportCooldown = 5f;
        private float teleportArcAngle = 90f;
        
        private const float TELEPORT_DISTANCE = 20f;
        private const float TRIGGER_DISTANCE = 20f;
        
        private float _teleportCooldownTimer = 5f;
        
        protected override void Move()
        {
            // Update cooldown timer
            if (_teleportCooldownTimer > 0f)
            {
                _teleportCooldownTimer -= Time.deltaTime;
            }
            
            Vector3 playerPosition = _playerTransform.position;
            Vector3 currentPosition = transform.position;
            float distanceToPlayer = Vector3.Distance(currentPosition, playerPosition);
            
            // Check if should teleport (distance > 16 and cooldown finished)
            if (distanceToPlayer > TRIGGER_DISTANCE && _teleportCooldownTimer <= 0f)
            {
                TeleportToPlayer();
                _teleportCooldownTimer = teleportCooldown; // Start cooldown
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
            float speed = StatRuntime.GetValue("Speed") * _globalConfig.MoveSpeedBalanceFactor;

            MyRigidbody2D.linearVelocity = direction * speed;
            
            // Flip sprite based on movement direction
            if (direction.x != 0)
                transform.localScale = new Vector3(Mathf.Sign(direction.x) * Mathf.Abs(transform.localScale.x), transform.localScale.y, 1);
        }
        
        private void TeleportToPlayer()
        {
            Vector3 playerPosition = _playerTransform.position;
            Vector2 playerMoveDirection = _globalConfig.GlobalPlayer.MoveDirection;
            
            // If player is not moving, use random direction
            if (playerMoveDirection.magnitude < 0.1f)
            {
                float randomAngle = UnityEngine.Random.Range(0f, 360f);
                playerMoveDirection = new Vector2(Mathf.Cos(randomAngle * Mathf.Deg2Rad), 
                                                 Mathf.Sin(randomAngle * Mathf.Deg2Rad));
            }
            else
            {
                playerMoveDirection = playerMoveDirection.normalized;
            }
            
            // Add random angle variation within the arc
            float randomAngleOffset = UnityEngine.Random.Range(-teleportArcAngle / 2f, teleportArcAngle / 2f);
            float baseAngle = Mathf.Atan2(playerMoveDirection.y, playerMoveDirection.x) * Mathf.Rad2Deg;
            float finalAngle = (baseAngle + randomAngleOffset) * Mathf.Deg2Rad;
            
            // Calculate final teleport direction (same direction as player movement)
            Vector2 teleportDirection = new Vector2(Mathf.Cos(finalAngle), Mathf.Sin(finalAngle));
            
            // Calculate teleport position (in front of player movement)
            Vector3 teleportPosition = playerPosition + (Vector3)(teleportDirection * TELEPORT_DISTANCE);
            
            // Apply teleport
            transform.position = teleportPosition;
            
            // Optional: Add teleport effect here
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
    }
}