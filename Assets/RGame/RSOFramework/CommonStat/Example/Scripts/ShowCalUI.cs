using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace RGame.CommonStat.Example
{
    public class ShowCalUI : MonoBehaviour
    {
        public CommonStatRuntimeSO ValueSO;

        public FormulaCalSO DamageCalSO;
        public TextMeshProUGUI CritDamage;

        private void OnEnable()
        {
            ValueSO.AddAction("Armor",UpdateUI);
            ValueSO.AddAction("Attack",UpdateUI);
            ValueSO.AddAction("CritDamage",UpdateUI);
        }

        private void OnDisable()
        {
            ValueSO.RemoveAction("Armor",UpdateUI);
            ValueSO.RemoveAction("Attack",UpdateUI);
            ValueSO.RemoveAction("CritDamage",UpdateUI);
        }

        private void Start()
        {
            UpdateUI(ValueSO.GetValue("Armor"), ValueSO.GetMaxValue("Armor"));
        }

        private void UpdateUI(int _value,int _maxValue)
        {
            CritDamage.text = "Critical Hit Damage:" + DamageCalSO.Evaluate(ValueSO);
        }
    }
}
