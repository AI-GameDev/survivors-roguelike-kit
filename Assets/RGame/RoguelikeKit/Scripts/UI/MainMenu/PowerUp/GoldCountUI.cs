using System;
using RGame.Framework;
using TMPro;
using UnityEngine;

namespace RGame.RoguelikeKit
{
    public class GoldCountUI : MonoBehaviour
    {
        [SerializeField] private AttributeSO _attributeSO;
        [SerializeField] private TextMeshProUGUI _goldCountText;
        [SerializeField] private VoidEventChannelSO _updateGoldCountUI;

        private void OnEnable()
        {
            _updateGoldCountUI.RegisterListener(UpdateUI);
        }

        private void OnDisable()
        {
            _updateGoldCountUI.UnregisterListener(UpdateUI);
        }

        private void Start()
        {
            UpdateUI();
        }

        private void UpdateUI()
        {
            _goldCountText.text = _attributeSO.GoldCount.ToString();
        }
    }
}
