#region

using System.Collections.Generic;
using UnityEngine;

#endregion

namespace RGame.CommonStat
{
    public class ModifyValuePercentAdd : IModifyValue
    {
        public int Value { get; set; } = 100;
        public int ReferenceValue { get; set; }
        public List<ModifyReference> Modify { get; set; } = new();

        public int GetValue()
        {
            if (Value < 0) Debug.LogWarning("Modify the value added by the percentage to be less than 0!");

            return Value + ReferenceValue;
        }

        public int GetReferenceValue()
        {
            return ReferenceValue;
        }

        public void ModifyValue(int _modifyValue)
        {
            Value += _modifyValue;
        }

        public void ReferenceModifyValue(ModifyReference _modify)
        {
            ReferenceValue += _modify.ModifyValue;

            Modify.Add(_modify);
        }

        public bool RemoveReference(ModifyReference _modifyReference)
        {
            var tempBool = Modify.Remove(_modifyReference);

            if (tempBool)
                ReferenceValue -= _modifyReference.ModifyValue;
            else
                Debug.LogError("ModifyReference Not Find!");

            return tempBool;
        }

        public void ResetValue()
        {
            Modify.Clear();
            Value = 100;
            ReferenceValue = 0;
        }
    }
}