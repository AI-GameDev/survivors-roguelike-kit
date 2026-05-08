using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace RGame.RoguelikeKit
{
    public class UpgradeSkillItem : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private TextMeshProUGUI _name;
        [SerializeField] private TextMeshProUGUI _description;
        [SerializeField] private TextMeshProUGUI _level;
        private SkillDataSO _skillData;
        
        public UnityAction<SkillDataSO> UpgradeSkillAction;
        
        public void SetUI(SkillDataSO skillData)
        {
            _icon.sprite = skillData.SkillIcon;
            _name.text = skillData.Key;
            
            _level.text = "Level:" + skillData.Level;
            _skillData = skillData;
            
            switch (skillData.SkillType)
            {
                case SkillType.AttackSkill:
                    AttackSkillUI(skillData);
                    break;
                case SkillType.AttributeSkill:
                    _description.text = $"Increases {skillData.AttributeType.ToString()} by {skillData.UpgradeAttributeModValue[skillData.Level]}";
                    break;
                case SkillType.MoneyBag:
                    MoneyBagUI(skillData);
                    break;
            }
        }

        private void AttackSkillUI(SkillDataSO skillData)
        {
            if (skillData.Level == 0)
            {
                _description.text = "Activate Skill";
                return;
            }
            
            switch (skillData.UpgradeAttribute[skillData.Level - 1].skillAttributeType)
            {
                case UpgradeSkillAttributeType.DamageAdd:
                    _description.text = $"Increases skill damage by {skillData.UpgradeAttribute[skillData.Level - 1].IntValue}%. Crush your enemies!";
                    break;

                case UpgradeSkillAttributeType.CDLessPercent:
                    _description.text = $"Reduces cooldown by {skillData.UpgradeAttribute[skillData.Level - 1].IntValue}%. Attack faster!";
                    break;

                case UpgradeSkillAttributeType.ProjectilesCountAdd:
                    _description.text = $"Fires {skillData.UpgradeAttribute[skillData.Level - 1].IntValue} more projectiles. More is always better!";
                    break;

                case UpgradeSkillAttributeType.VelocityAddPercent:
                    _description.text = $"Increases projectile speed by {skillData.UpgradeAttribute[skillData.Level - 1].IntValue}%. Reach enemies quicker!";
                    break;

                case UpgradeSkillAttributeType.AreaAddPercent:
                    _description.text = $"Expands attack area by {skillData.UpgradeAttribute[skillData.Level - 1].IntValue}%. Hit more enemies at once!";
                    break;
            }
        }

        private void MoneyBagUI(SkillDataSO skillData)
        {
            _name.text = "Money Bag";
            _level.text = ""; 
            _description.text = $"Contains {skillData.MoneyAmount} coins. Instant wealth!";
        }

        public void UpgradeSkill()
        {
            UpgradeSkillAction?.Invoke(_skillData);
        }
    }
}