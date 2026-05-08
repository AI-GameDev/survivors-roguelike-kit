#region

using UnityEngine;

#endregion

namespace RGame.RoguelikeKit
{
    public class BouncingSkill : SkillBase
    {
        private Vector3 _moveDir;
        private float _velocity;
        private int _damage;
        private float _duration;
        
        private float _timer;
        
        private void FixedUpdate()
        {
            _timer += Time.fixedDeltaTime;

            if (_timer >= _duration)
            {
                _timer = 0;
                OnDeath?.Invoke(this);
            }
            
            transform.position += _moveDir * (_velocity * Time.fixedDeltaTime);
            
            var viewPos = Camera.main.WorldToViewportPoint(transform.position);

            if (viewPos.x <= 0.05f || viewPos.x >= 0.95f)
            {
                _moveDir.x = -_moveDir.x;
                viewPos.x = Mathf.Clamp(viewPos.x, 0.05f, 0.95f);
                transform.position = Camera.main.ViewportToWorldPoint(viewPos);

            }

            if (viewPos.y <= 0.05f || viewPos.y >= 0.95f)
            {
                _moveDir.y = -_moveDir.y;
                viewPos.y = Mathf.Clamp(viewPos.y, 0.05f, 0.95f);
                transform.position = Camera.main.ViewportToWorldPoint(viewPos); 
            }
        }

        public void Init(float velocity, int damage, float duration)
        {
            _velocity = velocity;
            _damage = damage;
            _duration = duration;
            
            _moveDir = new Vector2(Random.Range(-1,1f), Random.Range(-1,1f)).normalized;
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