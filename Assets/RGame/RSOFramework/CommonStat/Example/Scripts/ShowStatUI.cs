using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace RGame.CommonStat.Example
{
    public class ShowStatUI : MonoBehaviour
    {
        public CommonStatRuntimeSO ValueSO;

        public string ValueName;

        public bool IsMaxValue = true;
        
        private TextMeshProUGUI mText;

        private void Awake()
        {
            mText = GetComponent<TextMeshProUGUI>();
        }

        private void OnEnable()
        {
            ValueSO.AddAction(ValueName,UpdateUI);
        }

        private void OnDisable()
        {
            ValueSO.RemoveAction(ValueName,UpdateUI);
        }

        private void Start()
        {
            UpdateUI(ValueSO.GetValue(ValueName), ValueSO.GetMaxValue(ValueName));
        }

        private void UpdateUI(int _value,int _maxValue)
        {
            if (IsMaxValue)
            {
                mText.text = $"{ValueName}: {_value}/{_maxValue}";
            }
            else
            {
                mText.text = $"{ValueName}: {_value}";
            }
        }
    }
}
