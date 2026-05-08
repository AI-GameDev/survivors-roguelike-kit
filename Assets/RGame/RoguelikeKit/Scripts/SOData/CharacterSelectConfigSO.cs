#region

using RGame.Framework;
using UnityEngine;

#endregion

namespace RGame.RoguelikeKit
{
    [CreateAssetMenu(menuName = "RGame/RoguelikeKit/UI/CharacterSelectConfig", fileName = "CharacterSelectConfig")]
    public class CharacterSelectConfigSO : DescriptionBaseSO
    {
        public string CharacterName;
        public Sprite CharacterImage;
        public Sprite BeginSkillImage;
        public GameObject CharacterPrefab;
        public int SellingPrice;
    }
}