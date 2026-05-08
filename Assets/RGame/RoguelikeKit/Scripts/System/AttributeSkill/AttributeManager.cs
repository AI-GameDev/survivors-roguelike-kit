using System;
using RGame.CommonStat;
using UnityEngine;

namespace RGame.RoguelikeKit
{
    public class AttributeManager : MonoBehaviour
    {
        [SerializeField] private CommonStatRuntimeSO _powerUpStat;
        [SerializeField] private CommonStatRuntimeSO _playerStat;

        private void Awake()
        {
            InitAttribute();
        }

        private void Start()
        {
            
            
        }

        private void InitAttribute()
        {
            _playerStat.ModifyValue("HP",_powerUpStat.GetValue("HPMax"),true);
            _playerStat.ModifyValue("HP",_powerUpStat.GetValue("HPMax"));
            
            AttributeAddToPlayerStat("Might");
            AttributeAddToPlayerStat("Armor");
            AttributeAddToPlayerStat("Recovery");
            AttributeAddToPlayerStat("Cooldown",-1);
            AttributeAddToPlayerStat("Area");
            AttributeAddToPlayerStat("SkillSpeed");
            AttributeAddToPlayerStat("Duration");
            AttributeAddToPlayerStat("Amount");
            AttributeAddToPlayerStat("Magnet");
            AttributeAddToPlayerStat("MoveSpeed");
            AttributeAddToPlayerStat("Growth");
            AttributeAddToPlayerStat("Greed");
            AttributeAddToPlayerStat("Curse");
        }
        
        private void AttributeAddToPlayerStat(string attributeName, int multiplier = 1)
        {
            _playerStat.ModifyValue(attributeName, _powerUpStat.GetValue(attributeName) * multiplier);
        }
    }
}
