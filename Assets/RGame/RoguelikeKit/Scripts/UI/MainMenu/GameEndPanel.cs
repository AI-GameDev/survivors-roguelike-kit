using System;
using System.Collections;
using RGame.Framework;
using UnityEngine;

namespace RGame.RoguelikeKit
{
    public class GameEndPanel : MonoBehaviour
    {
        [SerializeField] private VoidEventChannelSO _openGameOverPanel;
        [SerializeField] private GameSceneSO _mainMenuScene;
        [SerializeField] private LoadEventChannelSO _sceneLoader;
        [SerializeField] private VoidEventChannelSO _clearAllBuffs;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private GlobalConfigSO _globalConfig;
        [SerializeField] private VoidEventChannelSO _saveGame;
        [SerializeField] private AttributeSO _attributeSO;
        [SerializeField] private PoolRuntimeSO poolRuntime;
        
        private void OnEnable()
        {
            _openGameOverPanel.RegisterListener(Show);
        }

        private void OnDisable()
        {
            _openGameOverPanel.UnregisterListener(Show);
        }

        private void Show()
        {
            Time.timeScale = 0;
            poolRuntime.RecycleAll();
            _clearAllBuffs.RaiseEvent();
            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = true;

            StartCoroutine(DelayInteractable());
        }
        
        public void Exit()
        {
            Time.timeScale = 1;
            _attributeSO.SetAttribute("GoldCount",_attributeSO.GoldCount + _globalConfig.CurrentGetGold);
            _saveGame.RaiseEvent();
            _sceneLoader.RaiseEvent(_mainMenuScene);
        }

        private IEnumerator DelayInteractable()
        {
            yield return new WaitForSeconds(0.5f);
            _canvasGroup.interactable = true; 
        }
    }
}
