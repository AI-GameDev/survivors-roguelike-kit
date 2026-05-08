 using System;
 using System.Collections;
 using System.Collections.Generic;
using RGame.CommonStat;
using RGame.Framework;
using UnityEngine;

namespace RGame.RoguelikeKit
{
    public class LevelManager : MonoBehaviour
    {
        [SerializeField] private GlobalConfigSO _globalConfig;
        [SerializeField] private CommonStatRuntimeSO _stat;
        [SerializeField] private VoidEventChannelSO _updateLevelUIChannel;
        [SerializeField] private VoidEventChannelSO _levelUpgradeChannel;
        [SerializeField] private PoolRuntimeSO _poolRuntime;
        [SerializeField] private ExpConfig expConfig;

        private void Start()
        {
            _globalConfig.OnGameStart();
            _stat.ResetAllValues();
            _stat.AddAction("Exp",ExpChange);
        }

        private void ExpChange(int value, int maxValue)
        {
            int level = _stat.GetValue("Level");

            if (value >= expConfig.GetExperienceForLevel(level))
            {
                _stat.ModifyValue("Exp", expConfig.GetExperienceForLevel(level) * -1);
                _stat.ModifyValue("Level", 1);
                _levelUpgradeChannel.RaiseEvent();
            }

            _updateLevelUIChannel.RaiseEvent();
        }
    }
}
