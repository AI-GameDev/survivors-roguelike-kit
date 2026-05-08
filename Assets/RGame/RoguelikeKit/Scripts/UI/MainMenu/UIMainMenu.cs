#region

using UnityEngine;
using UnityEngine.Events;

#endregion

namespace RGame.RoguelikeKit
{
    public class UIMainMenu : MonoBehaviour
    {
        public UnityAction ExitButtonAction;
        public UnityAction NewGameButtonAction;
        public UnityAction PowerUpButtonAction;
        public UnityAction SettingsButtonAction;

        public void NewGameButton()
        {
            NewGameButtonAction.Invoke();
        }

        public void SettingsButton()
        {
            SettingsButtonAction.Invoke();
        }

        public void PowerUpButton()
        {
            PowerUpButtonAction.Invoke();
        }

        public void ExitButton()
        {
            ExitButtonAction.Invoke();
        }
    }
}