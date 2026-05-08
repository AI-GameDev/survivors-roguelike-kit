#region

using System;
using RGame.Framework;
using UnityEngine;
using UnityEngine.Localization.Settings;

#endregion

namespace RGame.RoguelikeKit
{
    public class SettingsSystem : MonoBehaviour
    {
        [SerializeField] private VoidEventChannelSO mSaveSettingsEvent;
        [SerializeField] private VoidEventChannelSO mSave;
        
        [SerializeField] private FloatEventChannelSO mChangeMasterVolumeEventChannel;
        [SerializeField] private FloatEventChannelSO mChangeSfxVolumeEventChannel;
        [SerializeField] private FloatEventChannelSO mChangeMusicVolumeEventChannel;

        [SerializeField] private SettingsSO mCurrentSettings;

        private void Awake()
        {
            Application.targetFrameRate = 240;
            Time.fixedDeltaTime = 1.0f / 240.0f;
        }

        private void Start()
        {
            SetCurrentSettings();
        }

        private void OnEnable()
        {
            mSaveSettingsEvent.RegisterListener(SaveSettings);
        }

        private void OnDisable()
        {
            mSaveSettingsEvent.UnregisterListener(SaveSettings);
        }

        /// <summary>
        ///     Set the current settings.
        /// </summary>
        private void SetCurrentSettings()
        {
            mChangeMusicVolumeEventChannel.RaiseEvent(mCurrentSettings.MusicVolume); // Raise event for volume change
            mChangeSfxVolumeEventChannel.RaiseEvent(mCurrentSettings.SfxVolume); // Raise event for volume change
            mChangeMasterVolumeEventChannel.RaiseEvent(mCurrentSettings.MasterVolume); // Raise event for volume change

            var currentResolution = Screen.currentResolution; // Get a default resolution in case saved resolution doesn't exist in the resolution list
            if (mCurrentSettings.ResolutionsIndex < Screen.resolutions.Length)
                currentResolution = Screen.resolutions[mCurrentSettings.ResolutionsIndex];
            Screen.SetResolution(currentResolution.width, currentResolution.height, mCurrentSettings.IsFullscreen);

            LocalizationSettings.SelectedLocale = mCurrentSettings.LanguageLocale;
        }

        private void SaveSettings()
        {
            SetCurrentSettings();
            mSave.RaiseEvent();
        }
    }
}