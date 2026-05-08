#region

using System;
using UnityEngine;
using UnityEngine.Localization;

#endregion

namespace RGame.RoguelikeKit
{
    [Serializable]
    public class SettingConfig
    {
        public float MasterVolume;
        public float MusicVolume;
        public float SfxVolume;
        public int ResolutionsIndex;
        public bool IsFullscreen;
        public Locale LanguageLocale;
        public bool IsShowDamage;
    }

    [CreateAssetMenu(fileName = "Settings", menuName = "RGame/RoguelikeKit/Settings/Create new settings SO")]
    public class SettingsSO : ScriptableObject
    {
        [SerializeField] private SettingConfig mSettingConfig;

        public float MasterVolume => mSettingConfig.MasterVolume;
        public float MusicVolume => mSettingConfig.MusicVolume;
        public float SfxVolume => mSettingConfig.SfxVolume;
        public int ResolutionsIndex => mSettingConfig.ResolutionsIndex;
        public bool IsFullscreen => mSettingConfig.IsFullscreen;
        public bool IsShowDamage => mSettingConfig.IsShowDamage;
        public Locale LanguageLocale => mSettingConfig.LanguageLocale;

        public void SaveSettings(float _newMusicVolume, float _newSfxVolume, float _newMasterVolume,
            int _newResolutionsIndex, bool _fullscreenState, Locale _locale, bool _isShowDamage)
        {
            mSettingConfig.MasterVolume = _newMasterVolume;
            mSettingConfig.MusicVolume = _newMusicVolume;
            mSettingConfig.SfxVolume = _newSfxVolume;
            mSettingConfig.ResolutionsIndex = _newResolutionsIndex;
            mSettingConfig.IsFullscreen = _fullscreenState;
            mSettingConfig.IsShowDamage = _isShowDamage;
            mSettingConfig.LanguageLocale = _locale;
        }

        public void LoadSavedSettings(SaveGame _savedFile)
        {
            mSettingConfig.MasterVolume = _savedFile.MasterVolume;
            mSettingConfig.MusicVolume = _savedFile.MusicVolume;
            mSettingConfig.SfxVolume = _savedFile.SfxVolume;
            mSettingConfig.ResolutionsIndex = _savedFile.ResolutionsIndex;
            mSettingConfig.IsFullscreen = _savedFile.IsFullscreen;
            mSettingConfig.LanguageLocale = _savedFile.LanguageLocale;
            mSettingConfig.IsShowDamage = _savedFile.IsShowDamage;
        }
    }
}