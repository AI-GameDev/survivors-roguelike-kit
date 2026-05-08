#region  
using System; 
using System.Collections.Generic; 
using RGame.Framework; 
using UnityEngine; 
using UnityEngine.Serialization;  
#endregion  

namespace RGame.RoguelikeKit 
{     
    public enum SkillType     
    {         
        AttackSkill,         
        AttributeSkill,
        MoneyBag
    }      

    public enum AttributeType     
    {         
        Might,         
        Armor,         
        HPMax,         
        Recovery,         
        Cooldown,         
        Area,         
        SkillSpeed,         
        Duration,         
        Amount,         
        MoveSpeed,         
        Magnet,         
        Growth,         
        Greed,         
        Curse     
    }      

    public enum SkillCurrentState     
    {         
        Normal,         
        WaitMix,         
        Mixed     
    }          

    [CreateAssetMenu(menuName = "RGame/RoguelikeKit/Skill/SkillData", fileName = "Skill")]     
    public class SkillDataSO : DescriptionBaseSO     
    {         
        public SkillType SkillType;         
        public string Key;         
        public string MixAttributeSkillKey;         
        public AttributeType AttributeType;         
        public GameObject SkillPrefab;          

        public Sprite SkillIcon;         
        public float Velocity = 2.5f;         
        public float Duration = 2f;         
        public float CD = 3;         
        public int Damage = 3;         
        public int Amount = 1;         
        public int Area = 0;         
        public List<AttackSkillAttributeMod> UpgradeAttribute = new();         
        public List<int> UpgradeAttributeModValue = new();                  

        public int Level { get; set; }         
        public FloatWrapper CurrentCD { get; set; }         
        public SkillCurrentState CurrentState { get; set; } = SkillCurrentState.Normal;

        // Money bag specific properties
        public int MoneyAmount { get; set; } = 50;  // Fixed 50 coins for money bag

        public SkillDataSO Copy()         
        {             
            SkillDataSO newSkillDataSo = CreateInstance<SkillDataSO>();                          

            newSkillDataSo.SkillType = SkillType;             
            newSkillDataSo.AttributeType = AttributeType;             
            newSkillDataSo.Key = Key;             
            newSkillDataSo.MixAttributeSkillKey = MixAttributeSkillKey;             
            newSkillDataSo.SkillPrefab = SkillPrefab;             
            newSkillDataSo.SkillIcon = SkillIcon;             
            newSkillDataSo.Velocity = this.Velocity;             
            newSkillDataSo.Duration = this.Duration;             
            newSkillDataSo.CD = this.CD;             
            newSkillDataSo.Damage = this.Damage;             
            newSkillDataSo.Amount = this.Amount;                          
            newSkillDataSo.UpgradeAttribute = new List<AttackSkillAttributeMod>(this.UpgradeAttribute);             
            newSkillDataSo.UpgradeAttributeModValue = new List<int>(this.UpgradeAttributeModValue);
            newSkillDataSo.MoneyAmount = this.MoneyAmount;                         

            return newSkillDataSo;         
        }          

        public bool CheckLevelToMax()         
        {             
            switch (SkillType)             
            {                 
                case SkillType.AttackSkill:                     
                    return Level == UpgradeAttribute.Count + 1;                                   

                case SkillType.AttributeSkill:                     
                    return Level == UpgradeAttributeModValue.Count;

                case SkillType.MoneyBag:
                    return true; 
            }                          

            return false;         
        }

        // Helper method to check if this is a money bag
        public bool IsMoneyBag()
        {
            return SkillType == SkillType.MoneyBag;
        }

        // Create a money bag instance
        public static SkillDataSO CreateMoneyBag(Sprite moneyBagIcon)
        {
            SkillDataSO moneyBag = CreateInstance<SkillDataSO>();
            moneyBag.SkillType = SkillType.MoneyBag;
            moneyBag.Key = "MoneyBag";
            moneyBag.SkillIcon = moneyBagIcon;
            moneyBag.MoneyAmount = 50;
            moneyBag.Description = "Money Bag - Contains 50 coins";
            return moneyBag;
        }
    } 
}