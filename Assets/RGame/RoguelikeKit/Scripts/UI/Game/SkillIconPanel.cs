using System;
using RGame.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace RGame.RoguelikeKit
{
    public class SkillIconPanel : MonoBehaviour
    {
        [SerializeField] private AddSkillChannel _addIconEventChannel;
        [SerializeField] private GameObject[] _skillIcons;

        private int _attackSkillIconIndex = 0;
        private int _attributeSkillIndex;
        private const int ATTRIBUTE_INDEX_START = 6;

        private void Awake()
        {
            _attributeSkillIndex = ATTRIBUTE_INDEX_START;
        }

        private void OnEnable()
        {
            _addIconEventChannel.RegisterListener(AddIcon);
        }

        private void OnDisable()
        {
            _addIconEventChannel.UnregisterListener(AddIcon);
        }

        private void AddIcon(SkillDataSO skillData)
        {
            switch (skillData.SkillType)
            {
                case SkillType.AttackSkill:
                    SetSkillIcon(ref _attackSkillIconIndex, skillData);
                    break;
                case SkillType.AttributeSkill:
                    SetSkillIcon(ref _attributeSkillIndex, skillData);
                    break;
            }
        }

        private void SetSkillIcon(ref int index, SkillDataSO skillData)
        {
            if (index >= _skillIcons.Length)
            {
                Debug.LogWarning("SkillIconPanel: Not enough skill icon slots!");
                return;
            }

            Image image = _skillIcons[index].GetComponent<Image>();
            if (image != null)
            {
                image.sprite = skillData.SkillIcon;
                image.color = Color.white;
                index++;
            }
            else
            {
                Debug.LogError($"SkillIconPanel: Missing Image component on skill icon at index {index}");
            }
        }
    }
}