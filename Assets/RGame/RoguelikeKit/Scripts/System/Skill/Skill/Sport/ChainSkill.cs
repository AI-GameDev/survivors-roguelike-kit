using System;
using UnityEngine;

namespace RGame.RoguelikeKit
{
    public class ChainSkill : SkillBase
    {
        private float _velocity;
        private int _damage;
        private Vector3 _dir;
        private EnemySystem _enemySystem;
        private BaseEnemy _targetBaseEnemy;
        private int _attackCount;
        
        private void OnEnable()
        {
            _targetBaseEnemy = null;
            _attackCount = 0;
        }

        private void FixedUpdate()
        {
            if (_targetBaseEnemy == null)
            {
                FindEnemy();
                return;
            }
            
            CheckToTarget();
            
            _dir = (_targetBaseEnemy.transform.position - transform.position).normalized;
            
            transform.position += _dir * (_velocity * Time.fixedDeltaTime);
        }

        public void Init(float velocity, int damage, EnemySystem enemySystem)
        {
            _velocity = velocity;
            _damage = damage;
            _enemySystem = enemySystem;
            FindEnemy();
        }
        
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("EnemyHit"))
            {
                Attack(other.transform.parent.GetComponent<BaseEnemy>());
                other.GetComponent<EnemyHit>().Hit(_damage);
            }
        }

        private void CheckToTarget()
        {
            if (Vector2.Distance(transform.position, _targetBaseEnemy.transform.position) <= 0.2f)
            {
                FindEnemy();
            }
        }
        
        private void FindEnemy()
        {
            var enemies = _enemySystem.GetNearestEnemies(transform.position,12,8);

            if (enemies.Count != 0)
            {
                for (int i = 0; i < enemies.Count; i++)
                {
                    if (enemies[i] != _targetBaseEnemy)
                    {
                        _targetBaseEnemy = enemies[i];
                        _attackCount++;
                        
                        if(_attackCount >= 4) OnDeath?.Invoke(this);
                        
                        return;
                    }
                }
            }
            
            OnDeath?.Invoke(this);
        }
    }
}
