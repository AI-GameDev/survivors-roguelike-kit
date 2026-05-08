using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RGame.CommonStat.Example
{
    public class BtnOneParmModify : MonoBehaviour
    {
        public CommonStatRuntimeSO ValueSO;

        public string ValueName;
        public bool IsModifyMaxValue;
        public int ModifyValue;
        
        public FormulaCalSO FormulaSO;
        public string ParameterName;
        
        private Button mButton;

        private Dictionary<string, double> ParameterDic = new Dictionary<string, double>();

        private void Start()
        {
            mButton = GetComponent<Button>();
            
            mButton.onClick.AddListener(() =>
            {
                ParameterDic[ParameterName] = ModifyValue;
                ValueSO.ModifyValue(ValueName,FormulaSO.Evaluate(ValueSO,ParameterDic),IsModifyMaxValue);
            });
        }
    }
}
