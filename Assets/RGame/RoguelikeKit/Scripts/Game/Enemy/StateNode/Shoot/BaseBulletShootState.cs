using System.Collections;
using RGame.CommonStat;
using RGame.Framework;
using RGame.ScriptableCoreKit;
using UnityEngine;

namespace RGame.RoguelikeKit
{
   /// <summary>
    /// Base class for bullet shooting states
    /// </summary>
    public abstract class BaseBulletShootState : ActionStateNode
    {
        [Header("Bullet Settings")]
        [SerializeField] protected string magicKey = "EnemyBullet";
        [SerializeField] protected float cooldownDuration = 2f;
        [SerializeField] protected PoolRuntimeSO poolRuntime;
        [SerializeField] protected GlobalConfigSO globalConfig;
        
        protected BaseEnemy enemy;
        protected CommonStatRuntimeSO stat;
        protected bool hasShot = false;
        
        public override void OnEnter()
        {
            base.OnEnter();
            
            enemy = GetComponent<BaseEnemy>();
            if (enemy != null)
            {
                stat = enemy.StatRuntime;
            }
            
            isFinish = false;
            hasShot = false;
            
            // Start shooting coroutine
            if (enemy != null)
                enemy.StartCoroutine(ShootSequence());
        }
        
        protected virtual IEnumerator ShootSequence()
        {
            // Perform the shooting
            PerformShoot();
            hasShot = true;
            
            // Wait for cooldown
            yield return new WaitForSeconds(cooldownDuration);
            
            // Finish the state
            FinishNode();
        }
        
        protected abstract void PerformShoot();
        
        protected GameObject CreateBullet(Vector2 direction)
        {
            if (poolRuntime == null) return null;
            
            GameObject projectile = poolRuntime.Request(magicKey);
            if (projectile == null) return null;
            
            projectile.transform.position = transform.position;
            
            EnemyMagic magic = projectile.GetComponent<EnemyMagic>();
            if (magic != null)
            {
                float damage = stat?.GetValue("Attack") ?? 1f;
              //  magic.Init(damage, direction);
            }
            
            return projectile;
        }
        
        protected Vector2 GetPlayerDirection()
        {
            var playerTransform = globalConfig.GlobalPlayer.transform;
            if (playerTransform != null)
            {
                return (playerTransform.position - transform.position).normalized;
            }
            return Vector2.right; // Default direction
        }
    }
}
