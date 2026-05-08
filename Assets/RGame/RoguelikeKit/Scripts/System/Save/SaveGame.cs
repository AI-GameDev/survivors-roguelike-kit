#region

using System;
using RGame.RoguelikeKit;
using UnityEngine;
using UnityEngine.Localization;

#endregion

namespace RGame.RoguelikeKit
{
    [Serializable]
    public class SaveGame
    {
        public float MasterVolume;
        public float MusicVolume;
        public float SfxVolume;
        public int ResolutionsIndex;
        public bool IsFullscreen;
        public Locale LanguageLocale;
        public bool IsShowDamage;

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
        public int GoldCount = 5000;

        public bool CharacterTwo;
        public bool CharacterThree;
        
        public void SaveSettings(SettingsSO settings)
        {
            MasterVolume = settings.MasterVolume;
            MusicVolume = settings.MusicVolume;
            SfxVolume = settings.SfxVolume;
            ResolutionsIndex = settings.ResolutionsIndex;
            IsFullscreen = settings.IsFullscreen;
            LanguageLocale = settings.LanguageLocale;
            IsShowDamage = settings.IsShowDamage;
        }

        public void SaveAttributes(AttributeSO attributeSo)
        {
            Might = attributeSo.Might;
            HPMax = attributeSo.HPMax;
            Recovery = attributeSo.Recovery;
            Armor = attributeSo.Armor;
            MoveSpeed = attributeSo.MoveSpeed;
            SkillSpeed = attributeSo.SkillSpeed;
            Amount = attributeSo.Amount;
            Cooldown = attributeSo.Cooldown;
            Area = attributeSo.Area;
            Duration = attributeSo.Duration;
            Magnet = attributeSo.Magnet;
            Curse = attributeSo.Curse;
            Growth = attributeSo.Growth;
            Greed = attributeSo.Greed;
            GoldCount = attributeSo.GoldCount;
        }

        public void SaveCharacter(CharacterSaveSO characterSave)
        {
            CharacterTwo = characterSave.CharacterTwo;
            CharacterThree = characterSave.CharacterThree;
        }
        
        public string ToJson()
        {
            return JsonUtility.ToJson(this);
        }

        public void LoadFromJson(string json)
        {
            JsonUtility.FromJsonOverwrite(json, this);
        }
    }
}