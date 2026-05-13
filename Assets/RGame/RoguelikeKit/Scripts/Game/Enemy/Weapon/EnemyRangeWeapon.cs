using RGame.CommonStat;
using RGame.Framework;
using UnityEngine;

namespace RGame.RoguelikeKit
{
    /// <summary>
    /// Ranged weapon that spawns and launches a projectile at the player.
    /// </summary>
    public class EnemyRangeWeapon : BaseEnemyWeapon
    {
        [SerializeField] private PoolRuntimeSO _poolRuntime;
        [SerializeField] private string _magicKey;

        protected override void FixedUpdate()
        {
            base.FixedUpdate();

            float range = _stat.GetValue("AttackRange");

            if (Vector3.Distance(transform.position, _globalConfig.GlobalPlayer.transform.position) <= range)
            {
                _isPlayerInRange = true;
            }
            else
            {
                _isPlayerInRange = false;
            }
        }

        protected override void PerformAttack()
        {
            if (_poolRuntime == null || string.IsNullOrEmpty(_magicKey))
                return;

            GameObject projectile = _poolRuntime.Request(_magicKey);
            projectile.transform.position = transform.position;
            EnemyMagic magic = projectile.GetComponent<EnemyMagic>();
            magic?.SetOwner(OwnerEnemyKey);
           // magic?.Init(_stat.GetValue("Attack"));
        }
    }
}
