#region

using System;
using RGame.CommonStat;
using RGame.Framework;
using UnityEngine;

#endregion

namespace RGame.RoguelikeKit
{
    public class PlayerMovement : MonoBehaviour
    {
        [SerializeField] private InputReader inputReader;
        [SerializeField] private GlobalConfigSO globalConfigSO;
        [SerializeField] private CommonStatRuntimeSO stat;
        [SerializeField] private Vector2WrapperSO joystickInputWrapperSo;
        
        private Player _player;
        private PlayerHit _playerHit;
        private Vector2 _inputMove;
        private Rigidbody2D _myRigidbody;
        private bool _isDie;
        private void Awake()
        {
            _myRigidbody = GetComponent<Rigidbody2D>();
            _playerHit = GetComponentInChildren<PlayerHit>();
            _player = GetComponentInChildren<Player>();
        }

        private void FixedUpdate()
        {
            if (_isDie) return;
            
            Vector2 move = _inputMove;
            
            if (move == Vector2.zero) move = joystickInputWrapperSo.Value;
            
            move.Normalize();
            
            if (move.x != 0 || move.y != 0)
            {
                _player.MoveDirection = move;
            }
            
            _myRigidbody.linearVelocity = move * (stat.GetValue("MoveSpeed") * globalConfigSO.MoveSpeedBalanceFactor * 0.5f);
            
            if (move.x != 0) transform.localScale = new Vector3(Mathf.Sign(move.x), 1, 1);
        }

        private void OnEnable()
        {
            inputReader.MoveEvent += Move;
            _playerHit.OnDie += Die;
        }

        private void OnDisable()
        {
            inputReader.MoveEvent -= Move;
            _playerHit.OnDie -= Die;
        }

        private void Move(Vector2 _movement)
        {
            _inputMove = _movement;
        }

        private void Die()
        {
            _isDie = true;
        }
    }
}