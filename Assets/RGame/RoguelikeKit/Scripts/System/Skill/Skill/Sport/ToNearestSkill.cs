using System;
using System.Collections.Generic;
using RGame.Framework;
using UnityEngine;

namespace RGame.RoguelikeKit
{
    public class ToNearestSkill : SkillBase
    {
        private int _damage;
        private Vector3 _moveDir;
        private float _velocity;
        private float _timer;

        private void OnEnable()
        {
            _timer = 0;
        }

        private void FixedUpdate()
        {
            _timer += Time.fixedDeltaTime;
            
            if(_timer > 5) OnDeath?.Invoke(this);
            
            transform.position += _moveDir * (_velocity * Time.fixedDeltaTime);
        }

        public void Init(float velocity, int damage, Vector3 targetPos)
        {
            _damage = damage;
            _velocity = velocity;
            
            _moveDir = (targetPos - transform.position).normalized;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("EnemyHit"))
            {
                Attack(other.transform.parent.GetComponent<BaseEnemy>());
                var enemyHit = other.GetComponent<EnemyHit>();
                enemyHit.SetLastSource(Key);
                enemyHit.Hit(GetDamage());
                OnDeath?.Invoke(this);
            }
        }

        private int GetDamage()
        {
            return _damage;
        }
    }
}
