using System;
using RGame.CommonStat;
using RGame.Framework;
using UnityEngine;

namespace RGame.RoguelikeKit
{
    public class ArmorAttribute : MonoBehaviour
    {
        [SerializeField] private CommonStatRuntimeSO mSkillStatSO;
        [SerializeField] private CommonStatRuntimeSO mPowerUpStatSO;
        [SerializeField] private IntWrapperEventChannelSO mHitChannelSO;

        private void OnEnable()
        {
            mHitChannelSO.RegisterListener(DamageReduction);
        }

        private void OnDisable()
        {
            mHitChannelSO.UnregisterListener(DamageReduction);
        }

        private void DamageReduction(IntWrapper _intWrapper)
        {
            var reductionValue = mSkillStatSO.GetValue("Armor") + mPowerUpStatSO.GetValue("Armor");
            
            _intWrapper.Value -= reductionValue;
        }
    }
}
