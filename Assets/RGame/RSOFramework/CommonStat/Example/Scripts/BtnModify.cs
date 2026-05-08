using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RGame.CommonStat.Example
{
    public class BtnModify : MonoBehaviour
    {
        public CommonStatRuntimeSO ValueSO;

        public string ValueName;
        public bool IsModifyMaxValue;
        public int ModifyValue;

        public bool IsUseFormula;
        public FormulaCalSO FormulaSO;
        
        private Button mButton;

        private void Start()
        {
            mButton = GetComponent<Button>();
            
            mButton.onClick.AddListener(() =>
            {
                if (IsUseFormula)
                {
                    ValueSO.ModifyValue(ValueName,FormulaSO.Evaluate(ValueSO),IsModifyMaxValue);
                }
                else
                {
                    ValueSO.ModifyValue(ValueName,ModifyValue,IsModifyMaxValue);
                }
            });
        }
    }
}
