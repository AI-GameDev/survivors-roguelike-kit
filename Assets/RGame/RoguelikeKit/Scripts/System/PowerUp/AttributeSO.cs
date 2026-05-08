#region

using System;
using System.Reflection;
using UnityEngine;

#endregion

namespace RGame.RoguelikeKit
{
    [Serializable]
    public class AttributeConfig
    {
        public int Might;
        public int HPMax;
        public int Recovery;
        public int Armor;
        public int MoveSpeed;
        public int SkillSpeed;
        public int Amount;
        public int Cooldown;
        public int Area;
        public int Duration;
        public int Magnet;
        public int Curse;
        public int Growth;
        public int Greed;
        public int GoldCount;
    }

    [CreateAssetMenu(fileName = "PowerUps", menuName = "RGame/RoguelikeKit/PowerUps/Attribute SO")]
    public class AttributeSO : ScriptableObject
    {
        [SerializeField] private AttributeConfig mAttributeConfig;

        public int Might => mAttributeConfig.Might;
        public int HPMax => mAttributeConfig.HPMax;
        public int Recovery => mAttributeConfig.Recovery;
        public int Armor => mAttributeConfig.Armor;
        public int MoveSpeed => mAttributeConfig.MoveSpeed;
        public int SkillSpeed => mAttributeConfig.SkillSpeed;
        public int Amount => mAttributeConfig.Amount;
        public int Cooldown => mAttributeConfig.Cooldown;
        public int Area => mAttributeConfig.Area;
        public int Duration => mAttributeConfig.Duration;
        public int Magnet => mAttributeConfig.Magnet;
        public int Curse => mAttributeConfig.Curse;
        public int Growth => mAttributeConfig.Growth;
        public int Greed => mAttributeConfig.Greed;

        public int GoldCount => mAttributeConfig.GoldCount;
        
        public void SaveAttributes(
            int might, int hpMax, int recovery, int armor, int moveSpeed,
            int skillSpeed, int amount, int cooldown, int area, int duration,
            int magnet, int curse, int growth, int greed, int goldCount
        )
        {
            mAttributeConfig.Might = might;
            mAttributeConfig.HPMax = hpMax;
            mAttributeConfig.Recovery = recovery;
            mAttributeConfig.Armor = armor;
            mAttributeConfig.MoveSpeed = moveSpeed;
            mAttributeConfig.SkillSpeed = skillSpeed;
            mAttributeConfig.Amount = amount;
            mAttributeConfig.Cooldown = cooldown;
            mAttributeConfig.Area = area;
            mAttributeConfig.Duration = duration;
            mAttributeConfig.Magnet = magnet;
            mAttributeConfig.Curse = curse;
            mAttributeConfig.Growth = growth;
            mAttributeConfig.Greed = greed;
            mAttributeConfig.GoldCount = goldCount;
        }

        public void LoadSavedAttributes(SaveGame savedFile)
        {
            mAttributeConfig.Might = savedFile.Might;
            mAttributeConfig.HPMax = savedFile.HPMax;
            mAttributeConfig.Recovery = savedFile.Recovery;
            mAttributeConfig.Armor = savedFile.Armor;
            mAttributeConfig.MoveSpeed = savedFile.MoveSpeed;
            mAttributeConfig.SkillSpeed = savedFile.SkillSpeed;
            mAttributeConfig.Amount = savedFile.Amount;
            mAttributeConfig.Cooldown = savedFile.Cooldown;
            mAttributeConfig.Area = savedFile.Area;
            mAttributeConfig.Duration = savedFile.Duration;
            mAttributeConfig.Magnet = savedFile.Magnet;
            mAttributeConfig.Curse = savedFile.Curse;
            mAttributeConfig.Growth = savedFile.Growth;
            mAttributeConfig.Greed = savedFile.Greed;
            mAttributeConfig.GoldCount = savedFile.GoldCount;
        }

        public int GetAttribute(string attributeName)
        {
            FieldInfo field = typeof(AttributeConfig).GetField(attributeName);
            if (field == null)
            {
                Debug.LogError($"Attribute '{attributeName}' does not exist in PowerUpConfig.");
                return 0;
            }

            return (int)field.GetValue(mAttributeConfig);
        }

        public void SetAttribute(string attributeName, int value)
        {
            FieldInfo field = typeof(AttributeConfig).GetField(attributeName);
            if (field == null)
            {
                Debug.LogError($"Attribute '{attributeName}' does not exist in PowerUpConfig.");
                return;
            }

            field.SetValue(mAttributeConfig, value);
        }
    }
}