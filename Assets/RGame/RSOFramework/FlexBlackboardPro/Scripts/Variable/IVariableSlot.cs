using System;
using UnityEngine;

namespace RGame.ScriptableCoreKit
{
    /// <summary>
    /// Base interface for all variable slots.
    /// </summary>
    public interface IVariableSlot
    {
        string Key { get; set; }
        Type ValueType { get; }
        object Value { get; set; }
    }
}
