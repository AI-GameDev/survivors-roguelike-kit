using RGame.Framework;
using UnityEditor;
using UnityEngine;

namespace RGame.RoguelikeKit
{
    /// <summary>
    /// Custom editor for SkillDataSO to handle different skill type configurations
    /// </summary>
    [CustomEditor(typeof(SkillDataSO))]
    public class SkillDataSOEditor : DescriptionBaseSOEditor
    {
        public override void OnInspectorGUI()
        { 
            serializedObject.Update();
            SkillDataSO skillData = (SkillDataSO)target;
            
            DrawCommonProperties();
            
            if (skillData.SkillType == SkillType.AttackSkill)
            {
                DrawAttackSkillProperties();
            }
            else if (skillData.SkillType == SkillType.AttributeSkill)
            {
                DrawAttributeSkillProperties();
            }
            else if (skillData.SkillType == SkillType.MoneyBag)
            {
                DrawMoneyBagProperties();
            }

            serializedObject.ApplyModifiedProperties();
            DrawSceneReferences();
        }

        private void DrawCommonProperties()
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("Description"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("SkillType"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("SkillIcon"));
        }

        private void DrawAttackSkillProperties()
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("Key"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("MixAttributeSkillKey"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("SkillPrefab"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("Velocity"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("Duration"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("CD"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("Damage"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("Amount"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("Area"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("UpgradeAttribute"));
        }

        private void DrawAttributeSkillProperties()
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("AttributeType"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("UpgradeAttributeModValue"));
        }

        private void DrawMoneyBagProperties()
        {
            EditorGUILayout.LabelField("Money Bag Properties", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Money bags are automatically configured with 50 coins.", MessageType.Info);
            
            // Show money amount as read-only
            GUI.enabled = false;
            EditorGUILayout.IntField("Money Amount", 50);
            GUI.enabled = true;
        }
    }
}