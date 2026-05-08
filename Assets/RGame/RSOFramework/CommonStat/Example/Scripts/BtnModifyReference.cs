using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RGame.CommonStat.Example
{
    public class BtnModifyReference : MonoBehaviour
    {
        public CommonStatRuntimeSO ValueSO;

        public string ValueName;
        public bool IsMaxValue;
        public StatModifyType ModifyType;
        public int ModifyValue;

        private Button mButton;
        private TextMeshProUGUI mText;
        
        private ModifyReference mModifyReference;

        private bool mIsUse;
        
        private void Start()
        {
            mButton = GetComponent<Button>();
            mText = GetComponentInChildren<TextMeshProUGUI>();
            
            mButton.onClick.AddListener(() =>
            {
                if (mIsUse)
                {
                    ValueSO.RemoveReferenceModifyValue(ValueName,mModifyReference);

                    mText.text = mText.text.Replace("Equipped", "Unequipped");

                    mIsUse = false;
                }
                else
                {
                    mModifyReference = ValueSO.ReferenceModifyValue(ValueName,ModifyType,ModifyValue);
                    
                    mText.text = mText.text.Replace("Unequipped", "Equipped");

                    mIsUse = true;
                }
            });
        }
    }
}
