#region

using System;
using DG.Tweening;
using RGame.CommonStat;
using RGame.Framework;
using UnityEngine;

#endregion

namespace RGame.RoguelikeKit
{
    public abstract class IDropOut : MonoBehaviour , IPoolR
    {
        [HideInInspector] public bool IsMove;
        [SerializeField] protected GlobalConfigSO _globalConfig;
        [SerializeField] protected CommonStatRuntimeSO _stat;
        [SerializeField] protected PoolRuntimeSO _pool;
        private Transform mPlayerTransform;

        private Sequence _sequence;
        private SpriteRenderer _spriteRenderer;
        
        public string Key { get; set; }

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }
        
        private void FixedUpdate()
        {
            if (mPlayerTransform == null)
            {
                if (_globalConfig.GlobalPlayer == null) return;
                mPlayerTransform = _globalConfig.GlobalPlayer.transform;
            }
            
            if (Vector3.Distance(mPlayerTransform.position, transform.position) < _stat.GetValue("Magnet") * 0.015f) IsMove = true;

            if (IsMove)
            {
                var dir = (mPlayerTransform.position - transform.position).normalized;

                transform.position += dir * (Time.deltaTime * 5.5f);

                if (Vector3.Distance(mPlayerTransform.position, transform.position) < 0.1f)
                {
                    Do();
                    
                    _pool.Return(this.gameObject);
                }
            }
        }
        
        public abstract void Do();
        
        private void OnDisable()
        {
            if (_sequence != null && _sequence.IsActive())
            {
                _sequence.Kill();
                _sequence = null;
            }

            IsMove = false;
        }

        public void Request()
        {
            if (!gameObject.activeInHierarchy) return;
            
            var color = _spriteRenderer.color;
            color.a = 0;
            _spriteRenderer.color = color;

            _sequence = DOTween.Sequence();
            
            _sequence.Append(_spriteRenderer.DOFade(1, 0.2f));
        }

        public void Return()
        {
           
        }

        private void OnGameEnd()
        {
            _pool.Return(this.gameObject);
        }
    }
}