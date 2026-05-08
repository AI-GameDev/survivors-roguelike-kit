using System;
using System.Collections.Generic;
using RGame.Framework;
using UnityEngine;

namespace RGame.RoguelikeKit
{
    public class PowerUpData : DescriptionBaseSO
    {
        [Header("PowerUp")] [Space(5)] 
        public List<PowerUpItemData> PowerUpItems;

        private Dictionary<string, PowerUpItemData> mPowerUpDic = new Dictionary<string, PowerUpItemData>();

        private bool mIsInit;
        
        private void OnEnable()
        {
            mPowerUpDic.Clear();
        }

        private void Initialization()
        {
            if (mIsInit) return;
            mIsInit = true;

            for (int i = 0; i < PowerUpItems.Count; i++)
            {
                mPowerUpDic.Add(PowerUpItems[i].AttributeName,PowerUpItems[i]);
            }
        }
        
        public PowerUpItemData GetPowerUp(string _attributeName)
        {
            Initialization();

            if (mPowerUpDic.TryGetValue(_attributeName, out var _powerUpItem))
            {
                return _powerUpItem;
            }
            else
            {
                Debug.LogError("AttributeName Not Find:" + _attributeName);
            }

            return null;
        }
    }

    public class PowerUpItemData
    {
        public string Description;
        public int MaxLevel;
        public string AttributeName;
        public int IncreaseValue;
        
        public int CurrentLevel { get; set; }
    }
}
