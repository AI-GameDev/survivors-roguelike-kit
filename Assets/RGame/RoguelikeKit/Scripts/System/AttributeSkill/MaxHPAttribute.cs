using System;
using RGame.CommonStat;
using UnityEngine;

namespace RGame.RoguelikeKit
{
    public class MaxHPAttribute : MonoBehaviour
    {
        [SerializeField] private CommonStatRuntimeSO mPowerUpStatSO;
        [SerializeField] private CommonStatRuntimeSO mPlayerStat;

        private void Start()
        {
            var maxAddValue = mPowerUpStatSO.GetValue("MaxHP");
            
            mPlayerStat.ModifyValue("MaxHP",maxAddValue,true);
            
            //mPlayerStat.ModifyValue("HP");
        }
    }
}
