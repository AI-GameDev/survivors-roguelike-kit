using System;
using RGame.CommonStat;
using RGame.Framework;
using UnityEngine;
using UnityEngine.Events;

namespace RGame.RoguelikeKit
{
    public class EnemyHit : MonoBehaviour
    {
        [SerializeField] private HitAnimationSO _hitAnimationSO;
        [SerializeField] private DissolveAnimationSO _dissolveAnimationSO;
        [SerializeField] private SpriteRenderer _spriteRenderers;
        [SerializeField] private PoolRuntimeSO poolRuntimeSo;
        private CommonStatRuntimeSO _selfStat;
        
        public UnityAction OnDeath { get; set; }
        public UnityAction OnDeathEnd { get; set; }
        
        private bool _isDead;

        private void OnEnable()
        {
            _dissolveAnimationSO.ResetMaterial(_spriteRenderers);
            _hitAnimationSO.ResetMaterial(_spriteRenderers.material);
        }

        public void SetStat(CommonStatRuntimeSO _stat)
        {
            _selfStat = _stat;
            OnDeathEnd += DieEnd;
        }

        private void OnDestroy()
        {
            OnDeathEnd -= DieEnd;
            //_hitAnimationSO.Remove(_spriteRenderers);
            //_dissolveAnimationSO.Remove(_spriteRenderers);
        }

        public void Hit(int damage)
        {
            if(_isDead) return;
            
            _selfStat.ModifyValue("HP",damage * -1);

            var go = poolRuntimeSo.Request("DamagePopup");
            go.GetComponent<DamagePopup>().Show(transform.position,damage);
            
            if (_selfStat.GetValue("HP") <= 0)
            {
                _isDead = true;
                OnDeath?.Invoke();
                
                _dissolveAnimationSO.Play(_spriteRenderers, OnDeathEnd);
            }
            else
            {
                _hitAnimationSO.Play(_spriteRenderers);
            }
        }

        private void DieEnd()
        {
            _isDead = false;
        }
    }
}
