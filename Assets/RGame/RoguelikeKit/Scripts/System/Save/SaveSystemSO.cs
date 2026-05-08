              #region

using RGame.Framework;
using UnityEngine;

#endregion

namespace RGame.RoguelikeKit
{
    [CreateAssetMenu(fileName = "SaveSystem", menuName = "RGame/RoguelikeKit/Settings/SaveSystem")]
    public class SaveSystemSO : ScriptableObject
    {
        [SerializeField] private VoidEventChannelSO _saveData;
        [SerializeField] private SettingsSO _currentSettings;
        [SerializeField] private AttributeSO _attribute;
        [SerializeField] private CharacterSaveSO _characterSave;
        
        public string saveFilename = "RoguelikeKit.RGame";
        public string backupSaveFilename = "RoguelikeKit.RGame.bak";
        [HideInInspector] public SaveGame Save = new();

        private void OnEnable()
        {
            _saveData.RegisterListener(SaveDataToDisk);
        }

        private void OnDisable()
        {
            _saveData.UnregisterListener(SaveDataToDisk);
        }

        public void LoadSaveDataFromDisk()
        {
            if (FileManager.LoadFromFile(saveFilename, out var json))
            {
                Save.LoadFromJson(json);
                LoadSettings();
                LoadPowerUp();
                LoadCharacter();
            }
            else
                Debug.LogError("Load Save Error");
        }

        public void SaveDataToDisk()
        {
            SaveSettings();
            SavePowerUp();
            SaveCharacter();
            
            if (FileManager.MoveFile(saveFilename, backupSaveFilename))
                if (!FileManager.WriteToFile(saveFilename, Save.ToJson()))
                    Debug.LogError("Save Error");
        }

        public void WriteEmptySaveFile()
        {
            FileManager.WriteToFile(saveFilename, "");
        }

        private void SaveSettings()
        {
            Save.SaveSettings(_currentSettings);
        }
        
        private void SavePowerUp()
        {
            Save.SaveAttributes(_attribute);
        }

        private void SaveCharacter()
        {
            Save.SaveCharacter(_characterSave);
        }
        
        private void LoadSettings()
        {
            _currentSettings.LoadSavedSettings(Save);
        }
        
        private void LoadPowerUp()
        {
            _attribute.LoadSavedAttributes(Save);
        }

        private void LoadCharacter()
        {
            _characterSave.LoadCharacter(Save);
        }
    }
}