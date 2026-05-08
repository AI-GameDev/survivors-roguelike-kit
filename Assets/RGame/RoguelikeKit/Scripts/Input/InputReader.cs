#region

using RGame.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

#endregion

namespace RGame.RoguelikeKit
{
    [CreateAssetMenu(fileName = "InputReader", menuName = "RGame/RoguelikeKit/Input/Input Reader")]
    public class InputReader : DescriptionBaseSO, GameInput.IGameplayActions, GameInput.IMenusActions
    {
        private GameInput mGameInput;

        private void OnEnable()
        {
            if (mGameInput == null)
            {
                mGameInput = new GameInput();

                mGameInput.Menus.SetCallbacks(this);
                mGameInput.Gameplay.SetCallbacks(this);
            }
        }

        private void OnDisable()
        {
            DisableAllInput();
        }

        public void OnMove(InputAction.CallbackContext context)
        {
            if (context.phase == InputActionPhase.Performed)
                MoveEvent.Invoke(context.ReadValue<Vector2>());
            if (context.phase == InputActionPhase.Canceled)
                MoveEvent.Invoke(Vector2.zero);
        }

        public void OnPause(InputAction.CallbackContext context)
        {
            if (context.phase == InputActionPhase.Performed)
                MenuPauseEvent.Invoke();
        }

        public void OnCancel(InputAction.CallbackContext context)
        {
            if (context.phase == InputActionPhase.Performed)
                MenuCloseEvent.Invoke();
        }

        public void OnConfirm(InputAction.CallbackContext context)
        {
            if (context.phase == InputActionPhase.Performed)
                MenuClickButtonEvent.Invoke();
        }

        public void OnMouseMove(InputAction.CallbackContext context)
        {
            if (context.phase == InputActionPhase.Performed)
                MenuMouseMoveEvent.Invoke();
        }

        public void OnUnpause(InputAction.CallbackContext context)
        {
            if (context.phase == InputActionPhase.Performed)
                MenuUnpauseEvent.Invoke();
        }

        public void OnChangeTab(InputAction.CallbackContext context)
        {
            if (context.phase == InputActionPhase.Performed)
                TabSwitched.Invoke(context.ReadValue<float>());
        }

        public void OnPoint(InputAction.CallbackContext context)
        {
        }

        public void OnClick(InputAction.CallbackContext context)
        {
        }

        public void OnSubmit(InputAction.CallbackContext context)
        {
        }

        public void OnRightClick(InputAction.CallbackContext context)
        {
        }

        public void OnMoveSelection(InputAction.CallbackContext context)
        {
        }

        public void OnNavigate(InputAction.CallbackContext context)
        {
        }

        public void EnableGameplayInput()
        {
            mGameInput.Menus.Disable();
            mGameInput.Gameplay.Enable();
        }

        public void EnableMenuInput()
        {
            mGameInput.Gameplay.Disable();
            mGameInput.Menus.Enable();
        }

        public void DisableAllInput()
        {
            mGameInput.Gameplay.Disable();
            mGameInput.Menus.Disable();
        }

        public bool LeftMouseDown()
        {
            return Mouse.current.leftButton.isPressed;
        }


        public void OnCloseInventory(InputAction.CallbackContext context)
        {
            CloseInventoryEvent.Invoke();
        }

        #region GamePlay

        public event UnityAction<Vector2> MoveEvent = delegate { };
        public event UnityAction MoveCancelEvent = delegate { };

        // Shared between menus
        public event UnityAction MoveSelectionEvent = delegate { };

        // Menus
        public event UnityAction MenuMouseMoveEvent = delegate { };
        public event UnityAction MenuClickButtonEvent = delegate { };
        public event UnityAction MenuUnpauseEvent = delegate { };
        public event UnityAction MenuPauseEvent = delegate { };
        public event UnityAction MenuCloseEvent = delegate { };
        public event UnityAction OpenInventoryEvent = delegate { }; // Used to bring up the inventory
        public event UnityAction CloseInventoryEvent = delegate { }; // Used to bring up the inventory
        public event UnityAction<float> TabSwitched = delegate { };

        #endregion
    }
}