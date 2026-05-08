#region

using System.Collections.Generic;
using RGame;
using RGame.Framework;
using UnityEngine;

#endregion

namespace RGame.CommonStat
{
    /// <summary>
    ///     A list of all values
    ///     Each object that has a value needs to create a configure-all-values form.
    /// </summary>
    [CreateAssetMenu(menuName = "RGame/CommonStat/Value/Value Config")]
    public class ValueConfigSO : DescriptionBaseSO
    {
        public List<SoleValue> ValueDefinitions;
    }
}