using System.Collections;
using UnityEngine;

namespace RGame.RoguelikeKit
{
    /// <summary>
    /// Spiral shot - rotating spread pattern
    /// </summary>
    public class SpiralShotStateNode : BaseBulletShootState
    {
        [Header("Spiral Settings")]
        [SerializeField] private int spiralCount = 3;
        [SerializeField] private float spiralInterval = 0.15f;
        [SerializeField] private int bulletsPerSpiral = 6;
        [SerializeField] private float rotationOffset = 30f;
        
        protected override IEnumerator ShootSequence()
        {
            for (int spiral = 0; spiral < spiralCount; spiral++)
            {
                float baseRotation = spiral * rotationOffset;
                float angleStep = 360f / bulletsPerSpiral;
                
                for (int i = 0; i < bulletsPerSpiral; i++)
                {
                    float angle = baseRotation + (angleStep * i);
                    Vector2 direction = new Vector2(
                        Mathf.Cos(angle * Mathf.Deg2Rad),
                        Mathf.Sin(angle * Mathf.Deg2Rad)
                    );
                    
                    CreateBullet(direction);
                }
                
                if (spiral < spiralCount - 1)
                    yield return new WaitForSeconds(spiralInterval);
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
            return "Spiral Shot";
        }
    }
}
