#region

using System;
using DG.Tweening;
using RGame.CommonStat;
using RGame.Framework;
using RGame.MLAgents;
using UnityEngine;
using UnityEngine.Events;

#endregion

namespace RGame.RoguelikeKit
{
    public class PlayerHit : MonoBehaviour
    {
        [SerializeField] private CommonStatRuntimeSO mStat;
        [SerializeField] private HitAnimationSO mHitAnimationSO;
        [SerializeField] private SpriteRenderer[] mSpriteRenderers;
        [SerializeField] private SpriteRenderer _shadowSpriteRenderer;
        [SerializeField] private VoidEventChannelSO _playerDieEventChannel;
        [SerializeField] private VoidEventChannelSO _onGameOverEventChannel;

        public UnityAction OnDie;

        private bool _isDie;

        public string LastAttackerEnemyKey { get; private set; }
        public string LastAttackKind { get; private set; }

        public void SetLastAttacker(string enemyKey, string attackKind)
        {
            LastAttackerEnemyKey = enemyKey;
            LastAttackKind = attackKind;
        }

        public void OnHit(int damage)
        {
            if (_isDie) return;

            damage = (int)(damage * mStat.GetValue("Armor") * 0.01f);

            if (damage == 0)
            {
                damage = 1;
            }

            mStat.ModifyValue("HP",damage * -1);

            MLBalanceHook.NotifyDamageTaken(damage, LastAttackerEnemyKey, LastAttackKind);

            if (mStat.GetValue("HP") <= 0)
            {
                OnDie?.Invoke();
                _isDie = true;
                
                foreach (var spriteRenderer in mSpriteRenderers)
                {
                    var material = spriteRenderer.material;
                    
                    material.DOFloat(0f, "_Alpha", 0.2f)
                        .SetEase(Ease.Linear);
                }

                _shadowSpriteRenderer.DOFade(0, 0.8f).OnComplete(() =>
                {
                    _onGameOverEventChannel.RaiseEvent();
                });

                _playerDieEventChannel.RaiseEvent();
            }
            else
            {
                foreach (var spriteRenderer in mSpriteRenderers)
                {
                    mHitAnimationSO.Play(spriteRenderer);
                }
            }
        }

        private void OnDestroy()
        {
            foreach (var spriteRenderer in mSpriteRenderers)
            {
                mHitAnimationSO.Remove(spriteRenderer);
            }
        }
    }
}