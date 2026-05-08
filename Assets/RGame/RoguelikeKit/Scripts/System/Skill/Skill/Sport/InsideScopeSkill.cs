using System;
using System.Collections.Generic;
using UnityEngine;

namespace RGame.RoguelikeKit
{
    public class InsideScopeSkill : SkillBase
    {
        public float AttackTimer = 1f;
        private List<EnemyHit> _enemyHits = new List<EnemyHit>();
        private float _duration;
        private int _damage;
        private int _attackCount;
        private Transform _playerTransform;

        private void OnEnable()
        {
            _enemyHits.Clear();
        }

        private void FixedUpdate()
        {
            AttackTimer += Time.fixedDeltaTime;

            if (AttackTimer >= 1)
            {
                AttackTimer = 0;
                _attackCount++;
                for (int i = 0; i < _enemyHits.Count; i++)
                {
                    if (_enemyHits[i] != null)
                    {
                        Attack(_enemyHits[i].transform.parent.GetComponent<BaseEnemy>());
                        _enemyHits[i].Hit(_damage);
                    }
                }
            }

            if (_attackCount == 4 && AttackTimer >= 0.95f)
            {
                _attackCount = 0;
                AttackTimer = 1;
                OnDeath?.Invoke(this);
            }
            
            transform.position = _playerTransform.position;
        }

        public void Init(int damage, Transform playerTransform)
        {
            _damage = damage;
            _playerTransform = playerTransform;
        }
        
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("EnemyHit"))
            {
                EnemyHit enemyHit = other.GetComponent<EnemyHit>();
                if (enemyHit != null)
                {
                    _enemyHits.Add(enemyHit);
                }
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag("EnemyHit"))
            {
                EnemyHit enemyHit = other.GetComponent<EnemyHit>();
                if (enemyHit != null)
                {
                    _enemyHits.Remove(enemyHit);
                }
            }
        }
    }
}
