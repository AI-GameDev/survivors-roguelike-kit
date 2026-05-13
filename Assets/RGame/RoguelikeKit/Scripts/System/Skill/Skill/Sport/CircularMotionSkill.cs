#region

using UnityEngine;

#endregion

namespace RGame.RoguelikeKit
{
    public class CircularMotionSkill : SkillBase
    {
        private float _radius = 2;
        private float _velocity = 360f;
        private float _angle;
        private int _damage;
        private float _duration;
        private float _timer;
        private Transform _centerTransform;
        
        private void FixedUpdate()
        {
            _timer += Time.fixedDeltaTime;

            if (_timer >= _duration)
            {
                _timer = 0;
                OnDeath?.Invoke(this);
            }
            
            _angle += _velocity * Time.fixedDeltaTime;

            var rad = _angle * Mathf.Deg2Rad;
            var x = _centerTransform.position.x + _radius * Mathf.Sin(rad);
            var y = _centerTransform.position.y - _radius * Mathf.Cos(rad);

            transform.position = new Vector3(x, y, transform.position.z);

            var direction = (_centerTransform.position - transform.position).normalized;
            var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle + 90);
        }

        public void Init(float radius, float velocity, int damage, float angle, float duration, Transform centerTransform)
        {
            _angle = angle;
            _radius = radius;
            _velocity = velocity;
            _damage = damage;
            _duration = duration;
            _centerTransform = centerTransform;    
            
            transform.position = _centerTransform.position + new Vector3(0, _radius, 0);
        }
        
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("EnemyHit"))
            {
                Attack(other.transform.parent.GetComponent<BaseEnemy>());
                var enemyHit = other.GetComponent<EnemyHit>();
                enemyHit.SetLastSource(Key);
                enemyHit.Hit(_damage);
            }
        }
    }
}