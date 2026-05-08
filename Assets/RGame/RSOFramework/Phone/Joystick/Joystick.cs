using System;
using RGame.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RGame.RoguelikeKit // Keeping your namespace
{
    /// <summary>
    /// Manages the behavior of a virtual joystick that appears at the touch/click position.
    /// The handle's movement range is determined by the size of the joystickBackground.
    /// </summary>
    public class FloatingJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Header("Joystick Visuals")]
        [Tooltip("The parent RectTransform for all joystick UI elements. This will be moved to the touch position.")]
        [SerializeField]
        private RectTransform joystickArea; // The entire area that moves to the touch position

        [Tooltip("The background of the joystick. The handle will be constrained within this area.")]
        [SerializeField]
        private Image joystickBackground;

        [Tooltip("The handle of the joystick that the user drags.")]
        [SerializeField]
        private Image joystickHandle;

        [SerializeField] private Vector2WrapperSO joystickVector2WrapperSo;
        
        [Header("Joystick Settings")]
        [Tooltip("The dead zone in the center of the joystick where input is considered zero. Percentage of joystick background's effective radius.")]
        [SerializeField]
        private float deadZone = 0.1f; // Percentage of the dynamic handle range
        
        private Canvas _canvas; // Parent canvas for coordinate calculations
        private Camera _cam;    // Camera for screen to world point conversion (if using ScreenSpaceCamera or WorldSpace)
        
        private void Awake()
        {
            if (joystickArea == null)
            {
                Debug.LogError("FloatingJoystick: Joystick Area (RectTransform) is not assigned.");
                enabled = false;
                return;
            }

            if (joystickBackground == null)
            {
                Debug.LogError("FloatingJoystick: Joystick Background (Image) is not assigned. Handle range cannot be determined.");
                enabled = false;
                return;
            }

            if (joystickHandle == null)
            {
                Debug.LogError("FloatingJoystick: Joystick Handle (Image) is not assigned.");
                enabled = false;
                return;
            }
            
            _canvas = GetComponentInParent<Canvas>();
            if (_canvas == null)
            {
                Debug.LogError("FloatingJoystick: This component must be a child of a Canvas.");
                enabled = false;
                return;
            }
            
            if (_canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                _cam = _canvas.worldCamera;
                if (_cam == null)
                {
                    _cam = Camera.main;
                    if (_cam == null)
                    {
                        Debug.LogError("FloatingJoystick: Canvas is not ScreenSpaceOverlay and no camera is assigned or found (Camera.main).");
                        enabled = false;
                        return;
                    }
                }
            }
            
            joystickBackground.gameObject.SetActive(false);
        }

        /// <summary>
        /// Called when the user first presses down on this UI element (the JoystickInputArea).
        /// </summary>
        /// <param name="eventData">Data related to the pointer event.</param>
        public void OnPointerDown(PointerEventData eventData)
        {
            joystickBackground.gameObject.SetActive(true);
            
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                joystickArea.parent as RectTransform,
                eventData.position,
                (_canvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : _cam,
                out Vector2 localPoint);

            joystickArea.localPosition = localPoint;

            joystickBackground.rectTransform.anchoredPosition = Vector2.zero; 
            joystickHandle.rectTransform.anchoredPosition = Vector2.zero;     
            joystickVector2WrapperSo.Value = Vector2.zero;                                     
        }

        /// <summary>
        /// Called when the user drags their finger/mouse after pressing down.
        /// </summary>
        /// <param name="eventData">Data related to the pointer event.</param>
        public void OnDrag(PointerEventData eventData)
        {
            if (!joystickArea.gameObject.activeSelf) return;

            Vector2 currentPosition;
      
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                joystickBackground.rectTransform,
                eventData.position,
                (_canvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : _cam,
                out currentPosition);
            
            Vector2 direction = currentPosition;
            
            float dynamicHandleRadius = Mathf.Min(joystickBackground.rectTransform.rect.width / 2f, joystickBackground.rectTransform.rect.height / 2f);
            
            if (dynamicHandleRadius <= 0) dynamicHandleRadius = 0.001f;
            
            float currentDragDistance = direction.magnitude;
            Vector2 clampedPosition = direction;

            if (currentDragDistance > dynamicHandleRadius)
            {
                clampedPosition = direction.normalized * dynamicHandleRadius;
            }

            joystickHandle.rectTransform.anchoredPosition = clampedPosition;
            
            float deadZoneRadius = dynamicHandleRadius * Mathf.Clamp01(deadZone); 

            if (clampedPosition.magnitude < deadZoneRadius)
            {
                joystickVector2WrapperSo.Value = Vector2.zero;
            }
            else
            {
                float effectiveRadius = dynamicHandleRadius - deadZoneRadius;

                if (effectiveRadius <= Mathf.Epsilon)
                {
                    joystickVector2WrapperSo.Value = Vector2.zero;
                }
                else
                {
                    Vector2 vectorFromDeadZoneEdge = clampedPosition.normalized * (clampedPosition.magnitude - deadZoneRadius);
                    joystickVector2WrapperSo.Value = vectorFromDeadZoneEdge / effectiveRadius;
                    
                    if (joystickVector2WrapperSo.Value.magnitude > 1.0f)
                    {
                        joystickVector2WrapperSo.Value = joystickVector2WrapperSo.Value.normalized;
                    }
                }
            }
        }

        /// <summary>
        /// Called when the user releases their finger/mouse.
        /// </summary>
        /// <param name="eventData">Data related to the pointer event.</param>
        public void OnPointerUp(PointerEventData eventData)
        {
            joystickBackground.gameObject.SetActive(false);
            joystickVector2WrapperSo.Value = Vector2.zero;            
            if(joystickHandle != null) joystickHandle.rectTransform.anchoredPosition = Vector2.zero;
        }

        private void OnDisable()
        {
            if (joystickArea != null && joystickArea.gameObject.activeSelf)
            {
                joystickArea.gameObject.SetActive(false);
            }
            joystickVector2WrapperSo.Value = Vector2.zero;
        }
    }
}