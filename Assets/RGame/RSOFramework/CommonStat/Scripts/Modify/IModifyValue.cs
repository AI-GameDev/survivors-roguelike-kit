#region

using System.Collections.Generic;

#endregion

namespace RGame.CommonStat
{
    public interface IModifyValue
    {
        public int Value { get; set; }

        public int ReferenceValue { get; set; }

        public List<ModifyReference> Modify { get; set; }

        public int GetValue();
        public int GetReferenceValue();
        public void ModifyValue(int _modifyValue);

        public void ReferenceModifyValue(ModifyReference _modify);

        public bool RemoveReference(ModifyReference _modifyReference);

        public void ResetValue();
    }
}