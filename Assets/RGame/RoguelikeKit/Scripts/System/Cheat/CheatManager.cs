using System;
using RGame.CommonStat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RGame.RoguelikeKit
{
    public class CheatManager : MonoBehaviour
    {
        public bool IsOn;
        [SerializeField] protected CommonStatRuntimeSO _stat;
        [SerializeField] protected GlobalConfigSO _globalConfig;
        [SerializeField] protected CanvasGroup _canvasGroup; 
        private void Update()
        {
            if (!IsOn) return;
            
            int level = _stat.GetValue("Level");
            if (Keyboard.current.uKey.wasPressedThisFrame && _globalConfig.GlobalPlayer != null && _canvasGroup.alpha == 0)
            {
                _stat.ModifyValue("Exp", (level-1) * 50 + 100);
            }
        }
    }
}
