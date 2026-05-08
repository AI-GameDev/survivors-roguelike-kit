using System;
using UnityEngine;

namespace RGame.ScriptableCoreKit
{
    /// <summary>
    /// Generic strongly‑typed variable slot.
    /// </summary>
    [Serializable]
    public class VariableSlot<T> : IVariableSlot
    {
        public VariableSlot()
        {
        }

        public VariableSlot(string key, T initial = default)
        {
            this.key = key;
            this.value = initial;
        }

        [SerializeField] private string key;
        [SerializeField] private T value;

        public string Key
        {
            get => key;
            set => key = value; 
        }

        public Type ValueType => typeof(T);

        public object Value
        {
            get => value;
            set
            {
                if (value == null)
                {
                    TypedValue = default(T);
                }
                else if (value is T directValue)
                {
                    TypedValue = directValue;
                }
                else
                {
                    try
                    {
                        TypedValue = (T)Convert.ChangeType(value, typeof(T));
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Cannot convert {value.GetType()} to {typeof(T)} for key '{Key}': {ex.Message}");
                        TypedValue = default(T);
                    }
                }
            }
        }

        public T TypedValue
        {
            get => value;
            set => this.value = value;
        }
    }
}
