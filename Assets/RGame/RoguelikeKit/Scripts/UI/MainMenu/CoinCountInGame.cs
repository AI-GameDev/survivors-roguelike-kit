using System;
using TMPro;
using UnityEngine;

namespace RGame.RoguelikeKit
{
    public class CoinCountInGame : MonoBehaviour
    {
        [SerializeField]
        private GlobalConfigSO _globalConfig;
        [SerializeField]
        private TextMeshProUGUI _coinCountText;

        private void FixedUpdate()
        {
            _coinCountText.text = _globalConfig.CurrentGetGold.ToString();
        }
    }
}
