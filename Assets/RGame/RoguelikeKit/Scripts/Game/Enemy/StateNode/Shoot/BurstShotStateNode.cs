using System.Collections;
using UnityEngine;

namespace RGame.RoguelikeKit
{
    /// <summary>
    /// Burst shot - multiple bullets in sequence toward player
    /// </summary>
    public class BurstShotStateNode : BaseBulletShootState
    {
        [Header("Burst Settings")]
        [SerializeField] private int burstCount = 3;
        [SerializeField] private float burstInterval = 0.2f;
        
        protected override IEnumerator ShootSequence()
        {
            Vector2 baseDirection = GetPlayerDirection();
            
            // Fire burst shots
            for (int i = 0; i < burstCount; i++)
            {
                CreateBullet(baseDirection);
                if (i < burstCount - 1)
                    yield return new WaitForSeconds(burstInterval);
            }
            
            hasShot = true;
            
            // Wait for cooldown
            yield return new WaitForSeconds(cooldownDuration);
            
            FinishNode();
        }
        
        protected override void PerformShoot()
        {
            // Implemented in ShootSequence override
        }
        
        public override string GetDisplayName()
        {
            return "Burst Shot";
        }
    }
}
