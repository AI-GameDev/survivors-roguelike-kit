using UnityEngine;

namespace RGame.RoguelikeKit
{
    /// <summary>
    /// Circle shot - 360 degree bullet pattern
    /// </summary>
    public class CircleShotSateNode : BaseBulletShootState
    {
        [Header("Circle Settings")]
        [SerializeField] private int bulletCount = 8;
        
        protected override void PerformShoot()
        {
            float angleStep = 360f / bulletCount;
            
            for (int i = 0; i < bulletCount; i++)
            {
                float angle = angleStep * i;
                Vector2 direction = new Vector2(
                    Mathf.Cos(angle * Mathf.Deg2Rad),
                    Mathf.Sin(angle * Mathf.Deg2Rad)
                );
                
                CreateBullet(direction);
            }
        }
        
        public override string GetDisplayName()
        {
            return "Circle Shot";
        }
    }
}
