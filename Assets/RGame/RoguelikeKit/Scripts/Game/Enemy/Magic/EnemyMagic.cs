using System;
using RGame.Framework;
using UnityEngine;

namespace RGame.RoguelikeKit
{
    public class EnemyMagic : MonoBehaviour, IPoolR
    {
        [SerializeField] private GlobalConfigSO globalConfig;
        [SerializeField] private PoolRuntimeSO poolRuntime;
        [SerializeField] private float velocity;

        private int _damage;
        private Vector3 _moveDir;
        private float _timer;
        private readonly float _delayReturn = 5f;

        private string _ownerEnemyKey;

        public void SetOwner(string enemyKey) { _ownerEnemyKey = enemyKey; }

        public void Init(int damage,Vector3 moveDir)
        {
            _damage = damage;
        }
        
        private void FixedUpdate()
        {
            _timer += Time.fixedDeltaTime;

            if (_timer < 0.08f)
            {
                _moveDir = (globalConfig.GlobalPlayer.transform.position - transform.position).normalized;
            }
            
            transform.position += velocity * Time.fixedDeltaTime * _moveDir;

            if (_timer >= _delayReturn)
            {
                poolRuntime.Return(gameObject);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("PlayerHit"))
            {
                var playerHit = other.GetComponent<PlayerHit>();
                playerHit.SetLastAttacker(_ownerEnemyKey, "Range");
                playerHit.OnHit(_damage);
                poolRuntime.Return(gameObject);
            }
        }

        public string Key { get; set; }
        public void Request()
        {
        }

        public void Return()
        {
            _timer = 0;
        }
    }
}
