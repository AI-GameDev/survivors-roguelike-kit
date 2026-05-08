using UnityEngine;

namespace RGame.RoguelikeKit
{
    /// <summary>
    /// Enemy that locks its movement vector toward the player when spawned
    /// and keeps charging in that direction until death or pooling.
    /// </summary>
    public class ChargeEnemy : BaseEnemy
    {
        [SerializeField] private float returnTime = 8f;
        
        private Vector2 _moveDir;
        private float _timer;

        protected override void FixedUpdate()
        {
            base.FixedUpdate();
            
            if (_globalConfig.GlobalPlayer != null && _timer < 2.5f)
            {
                Vector3 playerPos = _globalConfig.GlobalPlayer.transform.position;
                _moveDir = (playerPos - transform.position).normalized;
            }
            
            _timer += Time.fixedDeltaTime;
            if (_timer >= returnTime)
            {
                _poolRuntime.Return(gameObject);
            }
        }

        /// <summary>
        /// Called by PoolRuntimeSO right after the object is taken from the pool.
        /// Captures the current player direction for a one-way charge.
        /// </summary>
        public override void Request()
        {
            
        }

        /// <summary>
        /// Reset when returned to pool.
        /// </summary>
        public override void Return()
        {
            _timer = 0;
            _moveDir = Vector2.zero;
        }

        /// <inheritdoc/>
        protected override void Move()
        {
            float speed = StatRuntime.GetValue("Speed") * _globalConfig.MoveSpeedBalanceFactor;
            MyRigidbody2D.linearVelocity = _moveDir * speed;
            
            if (_moveDir.x != 0)
                transform.localScale = new Vector3(Mathf.Sign(_moveDir.x), 1, 1);
        }
    }
}