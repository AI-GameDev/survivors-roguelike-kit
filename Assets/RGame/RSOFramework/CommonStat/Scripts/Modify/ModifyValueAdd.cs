#region

using System.Collections.Generic;
using UnityEngine;

#endregion

namespace RGame.CommonStat
{
    public class ModifyValueAdd : IModifyValue
    {
        //Responding to direct modifications
        public int Value { get; set; }

        public int ReferenceValue { get; set; }

        public List<ModifyReference> Modify { get; set; } = new();

        public int GetValue()
        {
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
            Value = 0;
            ReferenceValue = 0;
        }
    }
}