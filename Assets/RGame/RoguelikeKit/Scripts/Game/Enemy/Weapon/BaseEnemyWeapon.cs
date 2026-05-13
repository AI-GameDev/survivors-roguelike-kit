using RGame.CommonStat;
using RGame.Framework;
using UnityEngine;

namespace RGame.RoguelikeKit
{
    /// <summary>
    /// Base class for enemy weapons: handles cooldown, player detection, and firing logic.
    /// </summary>
    public abstract class BaseEnemyWeapon : MonoBehaviour
    {
        [SerializeField] protected GlobalConfigSO _globalConfig;

        protected PlayerHit _playerHit;
        protected CommonStatRuntimeSO _stat;
        protected bool _isPlayerInRange;
        private float _attackTimer;

        private string _ownerEnemyKey;
        protected string OwnerEnemyKey
        {
            get
            {
                if (_ownerEnemyKey == null)
                {
                    var owner = GetComponentInParent<BaseEnemy>();
                    _ownerEnemyKey = owner != null ? owner.Key : string.Empty;
                }
                return _ownerEnemyKey;
            }
        }

        /// <summary>
        /// Assigns the stat runtime for damage and cooldown values.
        /// </summary>
        public void SetStat(CommonStatRuntimeSO stat)
        {
            _stat = stat;
        }

        protected virtual void FixedUpdate()
        {
            if (_playerHit == null && _globalConfig.GlobalPlayer != null)
            {
                _playerHit = _globalConfig.GlobalPlayer.GetComponentInChildren<PlayerHit>();
            }

            _attackTimer -= Time.fixedDeltaTime;

            if (_isPlayerInRange && _attackTimer <= 0f && _stat != null)
            {
                _attackTimer = _stat.GetValue("AttackCD") * 0.01f;
                PerformAttack();
            }
        }
        
        /// <summary>
        /// Executes the weapon-specific attack behavior.
        /// </summary>
        protected abstract void PerformAttack();
    }
}
