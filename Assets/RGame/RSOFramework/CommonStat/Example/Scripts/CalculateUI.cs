using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RGame.CommonStat.Example
{
    public class CalculateUI : MonoBehaviour
    {
        public CommonStatRuntimeSO ValueSO;

        public FormulaCalSO CalSo;
        
        public string Prefix = "Calculation result: ";
        
        public TextMeshProUGUI ResultText;

        private void Start()
        {
            GetComponent<Button>().onClick.AddListener(() =>
            {
                ResultText.text = Prefix + CalSo.Evaluate(ValueSO);
            });
        }
    }
}
