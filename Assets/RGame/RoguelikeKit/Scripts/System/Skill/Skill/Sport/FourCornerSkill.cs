using UnityEngine;

namespace RGame.RoguelikeKit
{
    public class FourCornerSkill : SkillBase
    {
        private float _velocity;
        private int _damage;
        private float _duration = 1;
        private float _timer;
        private Vector3 _dir;
        
        private void FixedUpdate()
        {
            _timer += Time.fixedDeltaTime;

            if (_timer >= _duration)
            {
                _timer = 0;
                OnDeath?.Invoke(this);
            }
            
            transform.position += _dir * (_velocity * Time.fixedDeltaTime);
        }

        public void Init(float velocity, int damage, Vector3 dir)
        {
            _velocity = velocity;
            _damage = damage;
            _dir = dir;
        }
        
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("EnemyHit"))
            {
                Attack(other.transform.parent.GetComponent<BaseEnemy>());
                other.GetComponent<EnemyHit>().Hit(_damage);
            }
        }
    }
}
