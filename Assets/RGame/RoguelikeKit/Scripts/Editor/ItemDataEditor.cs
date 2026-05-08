using UnityEditor;
using UnityEngine;

namespace RGame.RoguelikeKit
{
    /// <summary>
    /// Custom editor for ItemData scriptable objects to handle attribute modifications
    /// </summary>
    [CustomEditor(typeof(ItemData))]
    public class ItemDataEditor : Editor
    {
        private SerializedProperty upgradeAttributesProperty;

        private void OnEnable()
        {
            upgradeAttributesProperty = serializedObject.FindProperty("UpgradeAttribute");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var itemData = (ItemData)target;

            DrawBaseAttributeFields(itemData);
            EditorGUILayout.Space();
            
            EditorGUILayout.LabelField("Upgrade Attributes", EditorStyles.boldLabel);
            DrawUpgradeAttributesSection(itemData);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawBaseAttributeFields(ItemData itemData)
        {
            var baseAttributeProperty = serializedObject.FindProperty("BaseAttribute");
            
            EditorGUILayout.PropertyField(
                baseAttributeProperty.FindPropertyRelative("AttributeType"),
                new GUIContent("Attribute Type")
            );

            if (itemData.baseSkillAttribute.skillAttributeType != UpgradeSkillAttributeType.ProjectilesCountAdd)
            {
                EditorGUILayout.PropertyField(
                    baseAttributeProperty.FindPropertyRelative("Value"),
                    new GUIContent("Value")
                );
            }
            else
            {
                EditorGUILayout.PropertyField(
                    baseAttributeProperty.FindPropertyRelative("IntValue"),
                    new GUIContent("IntValue")
                );
            }
        }

        private void DrawUpgradeAttributesSection(ItemData itemData)
        {
            var newSize = EditorGUILayout.IntField("Upgrade Attribute Count", upgradeAttributesProperty.arraySize);
            newSize = Mathf.Clamp(newSize, 0, int.MaxValue);
            upgradeAttributesProperty.arraySize = newSize;
            serializedObject.ApplyModifiedProperties();

            for (var i = 0; i < newSize; i++)
            {
                var attributeModProperty = upgradeAttributesProperty.GetArrayElementAtIndex(i);
                var attributeMod = itemData.UpgradeAttribute[i];

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                {
                    EditorGUILayout.PropertyField(
                        attributeModProperty.FindPropertyRelative("AttributeType"),
                        new GUIContent("Attribute Type")
                    );

                    if (attributeMod.skillAttributeType != UpgradeSkillAttributeType.ProjectilesCountAdd)
                    {
                        EditorGUILayout.PropertyField(
                            attributeModProperty.FindPropertyRelative("Value"),
                            new GUIContent("Value")
                        );
                    }
                    else
                    {
                        EditorGUILayout.PropertyField(
                            attributeModProperty.FindPropertyRelative("IntValue"),
                            new GUIContent("IntValue")
                        );
                    }
                }
                EditorGUILayout.EndVertical();
            }
        }
    }
}
