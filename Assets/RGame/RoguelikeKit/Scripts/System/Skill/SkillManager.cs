using System.Collections.Generic;
using RGame.CommonStat;
using RGame.Framework;
using UnityEngine;

namespace RGame.RoguelikeKit
{
    public class SkillManager : MonoBehaviour
    {
        [SerializeField] private PoolRuntimeSO _pool;
        [SerializeField] private CommonStatRuntimeSO _playerStat;
        [SerializeField] private SkillDataSO[] _skillData;
        [SerializeField] private GlobalConfigSO _globalConfig;  // Reference to global config for money
        [SerializeField] private Sprite _moneyBagIcon;  // Money bag icon sprite
        
        [SerializeField] private AddTimerChannel _addTimerChannel;
        [SerializeField] private StringEventChannelSO _upgradeSkillChannel;
        [SerializeField] private VoidEventChannelSO _levelUpgradeChannel;
        [SerializeField] private UpgradeSkillUIChannel _upgradeSkillUIChannel;
        [SerializeField] private AddSkillChannel _addSkillIconChannel;
        [SerializeField] private UpgradeSkillUIChannel _openTreasurePanelChannel;
        [SerializeField] private VoidEventChannelSO _openTreasureChannel;
        
        private Dictionary<string, SkillDataSO> _skillDataDictionary = new Dictionary<string, SkillDataSO>();
        private Dictionary<string, SkillCast> _skillCastDictionary = new Dictionary<string, SkillCast>();
        [SerializeField]private HashSet<string> _activeSkills = new HashSet<string>();

        private int _activeAttackSkill;
        private int _activeAttributeSkill;
        
        private void Awake()
        {
            var skillCasts = GetComponentsInChildren<SkillCast>();

            foreach (var cast in skillCasts)
            {
                _skillCastDictionary[cast.Key] = cast;
            }
            
            foreach (var skillDataSo in _skillData)
            {
                if (skillDataSo.SkillType is SkillType.AttributeSkill)
                    skillDataSo.Key = skillDataSo.AttributeType.ToString();
                _skillDataDictionary.Add(skillDataSo.Key, skillDataSo.Copy());
            }
            
            _upgradeSkillChannel.RegisterListener(UpgradeSkill);
            _levelUpgradeChannel.RegisterListener(OnCharacterUpgrade);
            _openTreasureChannel.RegisterListener(OpenTreasurePanel);
        }

        public void UpgradeSkill(string key)
        {
            // Handle money bag selection
            if (key == "MoneyBag")
            {
                _globalConfig.CurrentGetGold += 50;
                return;
            }

            var skillData = _skillDataDictionary[key];
            
            if (skillData.Level == 0)
            {
                _activeSkills.Add(key);

                switch (skillData.SkillType)
                {
                    case SkillType.AttackSkill:
                        _activeAttackSkill++;
                        break;
                    case SkillType.AttributeSkill:
                        _activeAttributeSkill++;
                        break;
                }
                
                _addSkillIconChannel.RaiseEvent(skillData);
                
                if (skillData.SkillType is SkillType.AttackSkill)
                {
                    _pool.Register(new PoolDefinition(skillData.Key, new SolePool(skillData.SkillPrefab, 1)));
                    skillData.CurrentCD = ScriptableObject.CreateInstance<FloatWrapper>();
                    UpdateSkillEndTime(key);
                    _addTimerChannel.RaiseEvent(skillData.CurrentCD, () => { CreateSkill(skillData.Key);});
                }
            }
            else
            {
                if (_skillDataDictionary[key].CheckLevelToMax())
                {
                    _skillCastDictionary[key].MixSkill();
                    return;
                }
                
                UpgradeAttackSkillAttribute(key);
            }
            
            UpgradeAttributeSkillAttribute(key);
            UpdateSkillEndTime(key);
            skillData.Level++;
            
            foreach (var activeSkillKey in _activeSkills)
            {
                if (_skillDataDictionary[activeSkillKey].SkillType is SkillType.AttackSkill &&
                    _skillDataDictionary[activeSkillKey].CheckLevelToMax() &&
                    _activeSkills.Contains(_skillDataDictionary[activeSkillKey].MixAttributeSkillKey))
                {
                    Debug.Log("key:" + activeSkillKey + "," + 
                              _skillDataDictionary[activeSkillKey].Level 
                              + "," + skillData.MixAttributeSkillKey);
                    _skillDataDictionary[activeSkillKey].CurrentState = SkillCurrentState.WaitMix;
                }
            }
        }

        private void CreateSkill(string key)
        {
            _skillCastDictionary[key].Cast(_skillDataDictionary[key]);
        }

        private void UpdateSkillEndTime(string key)
        {
            if (_activeSkills.Contains(key))
            {
                var skillData = _skillDataDictionary[key];

                if (skillData.SkillType is SkillType.AttributeSkill)
                {
                    if (skillData.AttributeType is AttributeType.Cooldown)
                    {
                        foreach (var value in _skillDataDictionary.Values)
                        {
                            if(value.SkillType is not SkillType.AttributeSkill && _activeSkills.Contains(value.Key))
                                value.CurrentCD.Value *= _playerStat.GetValue("Cooldown") * 0.01f;
                        }
                    }
                }
                else
                {
                    skillData.CurrentCD.Value = skillData.CD * _playerStat.GetValue("Cooldown") * 0.01f;
                }
            }
        }

        private void UpgradeAttackSkillAttribute(string key)
        {
            var skillData = _skillDataDictionary[key];

            if (skillData.SkillType is SkillType.AttackSkill)
            {
                var attributeMod = skillData.UpgradeAttribute[skillData.Level - 1];
                
                switch (attributeMod.skillAttributeType)
                {
                    case UpgradeSkillAttributeType.DamageAdd:
                        skillData.Damage += attributeMod.IntValue;
                        break;
                    case UpgradeSkillAttributeType.CDLessPercent:
                        skillData.CD -= skillData.CD * attributeMod.IntValue * 0.01f;
                        break;
                    case UpgradeSkillAttributeType.ProjectilesCountAdd:
                        skillData.Amount += attributeMod.IntValue;
                        break;
                    case UpgradeSkillAttributeType.VelocityAddPercent:
                        skillData.Velocity += skillData.Velocity * attributeMod.IntValue * 0.01f;
                        break;
                    case UpgradeSkillAttributeType.AreaAddPercent:
                        skillData.Area += attributeMod.IntValue;
                        break;
                }
            }
        }

        private void UpgradeAttributeSkillAttribute(string key)
        {
            var skillData = _skillDataDictionary[key];

            if (skillData.SkillType is SkillType.AttributeSkill)
            {
                var attributeMod = skillData.UpgradeAttributeModValue[skillData.Level];

                if (skillData.AttributeType is AttributeType.HPMax)
                {
                    _playerStat.ModifyValue("HP",attributeMod,true);
                }
                else
                {
                    _playerStat.ModifyValue(skillData.AttributeType.ToString(), attributeMod);
                }
            }
        }
        
        private void OnCharacterUpgrade()
        {
            List<SkillDataSO> skillDataList = new List<SkillDataSO>(3);
            List<SkillDataSO> availableSkills = new List<SkillDataSO>();
            
            // Collect available skills
            foreach (var skill in _skillDataDictionary.Values)
            {
                if (skill.CheckLevelToMax()) continue;
                if(skill.SkillType is SkillType.AttributeSkill && _activeAttributeSkill == 6 && !_activeSkills.Contains(skill.Key)) continue;
                if(skill.SkillType is SkillType.AttackSkill && _activeAttackSkill == 6 && !_activeSkills.Contains(skill.Key)) continue;
                
                availableSkills.Add(skill);
            }
            
            // Add random skills up to available skills count
            int count = Mathf.Min(3, availableSkills.Count);
            while (skillDataList.Count < count)
            {
                SkillDataSO randomSkill = availableSkills[UnityEngine.Random.Range(0, availableSkills.Count)];

                if (!skillDataList.Contains(randomSkill))
                {
                    skillDataList.Add(randomSkill);
                }
            }

            // Fill remaining slots with money bags if needed
            while (skillDataList.Count < 3)
            {
                SkillDataSO moneyBag = SkillDataSO.CreateMoneyBag(_moneyBagIcon);
                skillDataList.Add(moneyBag);
            }

            _upgradeSkillUIChannel.RaiseEvent(skillDataList);
        }

        private void OpenTreasurePanel()
        {
            int random = Random.Range(0, 6);

            int count = 0;

            if (random > 3)
            {
                count = 1;
            }
            else if (random > 0)
            {
                count = 3;
            }
            else
            {
                count = 5;
            }

            List<SkillDataSO> skillDataList = new List<SkillDataSO>(count);
            
            foreach (var skill in _skillDataDictionary.Values)
            {
                if (skill.CurrentState is SkillCurrentState.WaitMix)
                {
                    count--;
                    skillDataList.Add(skill);

                    if (count == 0) break;
                }
            }

            // 如果所有位置都被 WaitMix 技能占满，直接返回
            if (count == 0)
            {
                _openTreasurePanelChannel.RaiseEvent(skillDataList);
                return;
            }

            List<SkillDataSO> availableSkills = new List<SkillDataSO>();
            Dictionary<string, int> maxUpgradePossible = new Dictionary<string, int>();
            
            foreach (var skill in _skillDataDictionary.Values)
            {
                if (skill.CheckLevelToMax())
                {
                    continue;
                }
                
                if (skill.SkillType is SkillType.AttributeSkill && _activeAttributeSkill == 6 && !_activeSkills.Contains(skill.Key)) continue;
                if (skill.SkillType is SkillType.AttackSkill && _activeAttackSkill == 6 && !_activeSkills.Contains(skill.Key)) continue;

                if (_activeSkills.Contains(skill.Key))
                {
                    availableSkills.Add(skill);

                    // Calculate how many times this skill can still be upgraded
                    int maxLevel = 0;
                    if (skill.SkillType == SkillType.AttackSkill)
                    {
                        maxLevel = skill.UpgradeAttribute.Count + 1;
                    }
                    else if (skill.SkillType == SkillType.AttributeSkill)
                    {
                        maxLevel = skill.UpgradeAttributeModValue.Count;
                    }

                    int remainingUpgrades = maxLevel - skill.Level;
                    maxUpgradePossible[skill.Key] = remainingUpgrades;
                }
            }

            // Track how many times each skill has been selected
            Dictionary<string, int> skillSelectionCount = new Dictionary<string, int>();

            // 如果没有可用技能，用钱袋填充剩余位置
            if (availableSkills.Count == 0)
            {
                while (count > 0)
                {
                    count--;
                    SkillDataSO moneyBag = SkillDataSO.CreateMoneyBag(_moneyBagIcon);
                    skillDataList.Add(moneyBag);
                }
            }
            else
            {
                // Fill treasure panel with skills first, then money bags if needed
                while (count > 0 && availableSkills.Count > 0)
                {
                    count--;

                    // Select random skill from available list
                    SkillDataSO randomSkill = availableSkills[UnityEngine.Random.Range(0, availableSkills.Count)];
                    skillDataList.Add(randomSkill);

                    // Track how many times this skill has been selected
                    if (!skillSelectionCount.ContainsKey(randomSkill.Key))
                        skillSelectionCount[randomSkill.Key] = 0;
                    skillSelectionCount[randomSkill.Key]++;

                    // Check if this skill has reached its maximum possible upgrades
                    if (skillSelectionCount[randomSkill.Key] >= maxUpgradePossible[randomSkill.Key])
                    {
                        availableSkills.Remove(randomSkill);
                    }
                }

                // Fill remaining slots with money bags if no more skills available
                while (count > 0)
                {
                    count--;
                    SkillDataSO moneyBag = SkillDataSO.CreateMoneyBag(_moneyBagIcon);
                    skillDataList.Add(moneyBag);
                }
            }

            _openTreasurePanelChannel.RaiseEvent(skillDataList);
        }

        private void OnDestroy()
        {
            _upgradeSkillChannel.UnregisterListener(UpgradeSkill);
            _levelUpgradeChannel.UnregisterListener(OnCharacterUpgrade);
            _openTreasureChannel.UnregisterListener(OpenTreasurePanel);
        }
    }
}