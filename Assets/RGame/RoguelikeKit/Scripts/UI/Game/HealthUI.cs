using System;
using RGame.CommonStat;
using RoguelikeKit;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace RGame.RoguelikeKit
{
    public class HealthUI : MonoBehaviour
    {
        [SerializeField] private CommonStatRuntimeSO mStat;
        [SerializeField] private Image mHPBar;
        [SerializeField] private Image mHpTransitionBar;
        
        private BarAnimator mHpBar;
        private UnityAction<int, int> mHPChange;
        
        private void Start()
        {
            mHpBar = new BarAnimator(mHPBar, mHpTransitionBar, 0.5f);

            mHPChange += mHpBar.SetValue;
            mStat.AddAction("HP", mHPChange);
        }
        
        private void OnDestroy()
        {
            mHpBar.Kill();
            mStat.RemoveAction("HP", mHPChange);
            mHPChange -= mHpBar.SetValue;
        }
    }
}
