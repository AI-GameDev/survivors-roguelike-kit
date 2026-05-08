using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace RGame.RoguelikeKit
{
    public class PowerUpDescription : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI mTitle;
        [SerializeField] private TextMeshProUGUI mDescription;
        [SerializeField] private TextMeshProUGUI mGoldCount;
        
        private UnityAction mUpgradeAction;
        
        public void SelectPowerUp(string _title, string _description, int _requireGold, UnityAction _upgrade)
        {
            mUpgradeAction = null;
            mUpgradeAction += _upgrade;

            mTitle.text = _title;
            mDescription.text = _description;
            mGoldCount.text = _requireGold.ToString();
        }

        public void OnBuyButtonOnclick()
        {
            mUpgradeAction?.Invoke();
        }
    }
}
