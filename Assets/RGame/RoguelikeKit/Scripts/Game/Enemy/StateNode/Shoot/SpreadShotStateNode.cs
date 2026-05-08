using UnityEngine;

namespace RGame.RoguelikeKit
{
    /// <summary>
    /// Spread shot - fan pattern of bullets
    /// </summary>
    public class SpreadShotStateNode : BaseBulletShootState
    {
        [Header("Spread Settings")]
        [SerializeField] private int bulletCount = 5;
        [SerializeField] private float spreadAngle = 60f;
        
        protected override void PerformShoot()
        {
            Vector2 baseDirection = GetPlayerDirection();
            float baseAngle = Mathf.Atan2(baseDirection.y, baseDirection.x) * Mathf.Rad2Deg;
            
            float startAngle = baseAngle - spreadAngle / 2f;
            float angleStep = spreadAngle / (bulletCount - 1);
            
            for (int i = 0; i < bulletCount; i++)
            {
                float currentAngle = startAngle + (angleStep * i);
                Vector2 direction = new Vector2(
                    Mathf.Cos(currentAngle * Mathf.Deg2Rad),
                    Mathf.Sin(currentAngle * Mathf.Deg2Rad)
                );
                
                CreateBullet(direction);
            }
        }
        
        public override string GetDisplayName()
        {
            return "Spread Shot";
        }
    }
}
