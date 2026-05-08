using System;
using RGame.CommonStat;
using RGame.Framework;
using RoguelikeKit;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace RGame.RoguelikeKit
{
    public class LevelUI : MonoBehaviour
    {
        [SerializeField] private Image mLevelBar;
        [SerializeField] private TextMeshProUGUI _levelText;
        [SerializeField] private VoidEventChannelSO _updateLevelUI;
        [SerializeField] private CommonStatRuntimeSO _stat;
        [SerializeField] private ExpConfig expConfig;
        
        private void Start()
        {
            _updateLevelUI.RegisterListener(UpdateUI);
        }

        private void OnDestroy()
        {
            _updateLevelUI.UnregisterListener(UpdateUI);
        }

        private void UpdateUI()
        {
            int currentLevel = _stat.GetValue("Level");

            float currentExp = _stat.GetValue("Exp");
            float requiredExp = expConfig.GetExperienceForLevel(currentLevel);
            float line = currentExp / requiredExp;
            
            mLevelBar.fillAmount = line;

            _levelText.text = "Level " + currentLevel;
        }
    }
}
