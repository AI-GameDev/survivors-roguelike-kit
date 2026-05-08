using System;
using RGame.Framework;
using UnityEngine;

namespace RGame.RoguelikeKit
{
    public class TreasureBox : MonoBehaviour, IPoolR
    {
        public string Key { get; set; }
        
        [SerializeField] private PoolRuntimeSO _poolRuntime;
        [SerializeField] private VoidEventChannelSO _openTreasureChannel;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("PlayerHit"))
            {
                _openTreasureChannel.RaiseEvent();
                _poolRuntime.Return(this.gameObject);
            }
        }
        
        public void Request()
        {
            
        }

        public void Return()
        {
            
        }
    }
}
