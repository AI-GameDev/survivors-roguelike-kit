#region

using System.Collections.Generic;
using RGame.Framework;
using UnityEngine;

#endregion

namespace RGame.CommonStat
{
    /// <summary>
    ///     ScriptableObject for defining a formula and calculating its result.
    /// </summary>
    [CreateAssetMenu(fileName = "NewFormulaCal", menuName = "RGame/CommonStat/FormulaCal")]
    public class FormulaCalSO : DescriptionBaseSO
    {
        [TextArea] public string Formula;

        /// <summary>
        ///     Calculate the final value based on the formula and provided runtime values.
        /// </summary>
        public int Evaluate(CommonStatRuntimeSO _runtimeValues, Dictionary<string, double> parameters = null)
        {
            var valueDic = new Dictionary<string, (int, int)>();

            foreach (var item in _runtimeValues.RuntimeValues) valueDic.Add(item.Key, (item.Value.GetCurrentValue(), item.Value.GetMaxValue()));

            return FormulaParser.Evaluate(Formula, valueDic, parameters);
        }
    }
}