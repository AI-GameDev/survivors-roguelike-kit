using UnityEngine;

namespace RGame.RoguelikeKit
{
    /// <summary>
    /// Single shot toward player
    /// </summary>
    public class SingleShotState : BaseBulletShootState
    {
        protected override void PerformShoot()
        {
            Vector2 direction = GetPlayerDirection();
            CreateBullet(direction);
        }
        
        public override string GetDisplayName()
        {
            return "Single Shot";
        }
    }
}
