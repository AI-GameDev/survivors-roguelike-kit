using System;
using RGame.CommonStat;
using RGame.Framework;
using UnityEngine;

namespace RGame.RoguelikeKit
{
    public class RecoveryAttribute : MonoBehaviour
    {
        [SerializeField] private CommonStatRuntimeSO mPlayerStat;

        private float mTimer;

        private void FixedUpdate()
        {
            mTimer += Time.fixedDeltaTime;

            if (mTimer > 1)
            {
                mTimer = 0;
                Recovery();
            }
        }

        private void Recovery()
        {
            var recoveryValue = mPlayerStat.GetValue("Recovery");
            
            mPlayerStat.ModifyValue("HP",recoveryValue);
        }
    }
}
