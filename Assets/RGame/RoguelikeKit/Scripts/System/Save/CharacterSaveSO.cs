using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

namespace RGame.RoguelikeKit
{
    [Serializable]
    public class CharacterSaveConfig
    {
        public bool CharacterTwo;
        public bool CharacterThree;
    }
    
    [CreateAssetMenu(fileName = "CharacterSave", menuName = "RGame/RoguelikeKit/Character/Character Save SO")]
    public class CharacterSaveSO : ScriptableObject
    {
        [SerializeField] private CharacterSaveConfig mChracterSaveConfig;

        public bool CharacterTwo => mChracterSaveConfig.CharacterTwo;
        public bool CharacterThree => mChracterSaveConfig.CharacterThree;

        public void SaveCharacter(bool characterTwo, bool characterThree)
        {
            mChracterSaveConfig.CharacterTwo = characterTwo;
            mChracterSaveConfig.CharacterThree = characterThree;
        }

        public void LoadCharacter(SaveGame _savedFile)
        {
            mChracterSaveConfig.CharacterTwo = _savedFile.CharacterTwo;
            mChracterSaveConfig.CharacterThree = _savedFile.CharacterThree;
        }
    }
}
