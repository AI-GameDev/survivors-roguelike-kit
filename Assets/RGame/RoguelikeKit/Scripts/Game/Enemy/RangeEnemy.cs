using System.Collections.Generic;
using RGame.CommonStat;
using RGame.Framework;
using UnityEngine;
using UnityEngine.Events;

namespace RGame.RoguelikeKit
{
    /// <summary>
    /// Enemy that attacks from range: moves until within range then uses its ranged weapon.
    /// </summary>
    public class RangeEnemy : BaseEnemy
    {
        protected override void Move()
        {
            float range = StatRuntime.GetValue("AttackRange") - 0.5f;
            float distance = Vector3.Distance(_playerTransform.position, transform.position);
            Vector3 direction = (_playerTransform.position - transform.position).normalized;
            
            if (distance > range)
            {
                float speed = StatRuntime.GetValue("Speed") * _globalConfig.MoveSpeedBalanceFactor;

                MyRigidbody2D.linearVelocity = direction * speed;
            }
            else
            {
                MyRigidbody2D.linearVelocity = Vector2.zero;
            }
            if (direction.x != 0)
                transform.localScale = new Vector3(Mathf.Sign(direction.x) * Mathf.Abs(transform.localScale.x), transform.localScale.y, 1);
        }
    }
}
