#region

using System;
using UnityEngine;

#endregion

namespace RGame.CommonStat
{
    [Serializable]
    public class SoleValue
    {
        [Tooltip("Used to identify the role of the current Value e.g. Hp, Mp, Speed.")]
        //If you wish you can add a Config to better distinguish the
        public string ValueName;

        public int DefaultValue;

        public bool HasMaxValue;

        public int MaxValue;
    }
}