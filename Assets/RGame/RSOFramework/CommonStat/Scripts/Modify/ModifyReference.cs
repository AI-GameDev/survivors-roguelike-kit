namespace RGame.CommonStat
{
    public enum StatModifyType
    {
        Add,
        PercentAdd,
        PercentMult,
        MaxAdd,
        MaxPercentAdd,
        MaxPercentMult
    }

    public class ModifyReference
    {
        public StatModifyType ModifyType;
        public int ModifyValue;
    }
}