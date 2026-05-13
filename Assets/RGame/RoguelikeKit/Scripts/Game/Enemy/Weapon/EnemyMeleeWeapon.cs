using System;
using RGame.CommonStat;
using UnityEngine;

namespace RGame.RoguelikeKit
{
    /// <summary>
    /// Melee weapon that directly applies damage to the player.
    /// </summary>
    public class EnemyMeleeWeapon : BaseEnemyWeapon
    {
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("PlayerHit"))
                _isPlayerInRange = true;
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag("PlayerHit"))
                _isPlayerInRange = false;
        }
        
        protected override void PerformAttack()
        {
            if (_playerHit == null) return;
            _playerHit.SetLastAttacker(OwnerEnemyKey, "Melee");
            _playerHit.OnHit(_stat.GetValue("Attack"));
        }
    }
}
