#region

using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

#endregion

namespace RGame.RoguelikeKit
{
    public class SettingItem : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI valueText;
        [SerializeField] private Button leftButton;
        [SerializeField] private Button rightButton;

        private int currentValue;
        private string[] displayTexts;
        private bool isBoolean;
        private int maxValue;
        private int minValue;
        private UnityAction<int> onValueChanged;

        public void Initialize(int defaultValue, int min, int max, bool isBool, UnityAction<int> callback)
        {
            minValue = min;
            maxValue = max;
            isBoolean = isBool;
            onValueChanged = callback;

            SetValue(defaultValue);

            leftButton.onClick.AddListener(OnLeftButtonClick);
            rightButton.onClick.AddListener(OnRightButtonClick);
        }

        public void Initialize(int defaultValue, string[] texts, UnityAction<int> callback)
        {
            displayTexts = texts;
            minValue = 0;
            maxValue = texts.Length - 1;
            isBoolean = false;
            onValueChanged = callback;

            SetValue(defaultValue);

            leftButton.onClick.AddListener(OnLeftButtonClick);
            rightButton.onClick.AddListener(OnRightButtonClick);
        }

        private void OnLeftButtonClick()
        {
            if (isBoolean)
            {
                SetValue(0);
            }
            else
            {
                var newValue = Mathf.Max(minValue, currentValue - 1);
                SetValue(newValue);
            }
        }

        private void OnRightButtonClick()
        {
            if (isBoolean)
            {
                SetValue(1);
            }
            else
            {
                var newValue = Mathf.Min(maxValue, currentValue + 1);
                SetValue(newValue);
            }
        }

        private void SetValue(int value)
        {
            currentValue = value;

            if (displayTexts != null && displayTexts.Length > 0)
                valueText.text = displayTexts[Mathf.Clamp(currentValue, 0, displayTexts.Length - 1)];
            else if (isBoolean)
                valueText.text = currentValue == 1 ? "ON" : "OFF";
            else
                valueText.text = currentValue.ToString();

            onValueChanged?.Invoke(currentValue);
        }

        public void UpdateValue(int value)
        {
            SetValue(value);
        }
    }
}