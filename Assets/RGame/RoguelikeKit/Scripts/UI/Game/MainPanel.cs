using System;
using RGame.Framework;
using UnityEngine;

namespace RGame.RoguelikeKit
{
    public class MainPanel : MonoBehaviour
    {
        [SerializeField] private PlayerSpawnChannelSO mOnSpawnPlayerEventChannel;

        private void Awake()
        {
            mOnSpawnPlayerEventChannel.RegisterListener(ActiveThis);
            
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            mOnSpawnPlayerEventChannel.UnregisterListener(ActiveThis);
        }

        private void ActiveThis(Player player)
        {
            gameObject.SetActive(true);
        }
    }
}
