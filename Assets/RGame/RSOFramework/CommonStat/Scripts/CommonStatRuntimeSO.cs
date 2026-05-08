#region

using System;
using System.Collections.Generic;
using RGame;
using RGame.Framework;
using UnityEngine;
using UnityEngine.Events;

#endregion

namespace RGame.CommonStat
{
    /// <summary>
    /// Used to manage all values in the runtime
    /// An instance can be created for each object that needs independent runtime values
    /// </summary>
    [CreateAssetMenu(menuName = "RGame/CommonStat/Value/Runtime Values")]
    public class CommonStatRuntimeSO : DescriptionBaseSO
    {
        [SerializeField] private ValueConfigSO mValueConfig;

        // Runtime dictionary of values
        // Please do not visit it unless there are special circumstances
        public readonly Dictionary<string, SoleValueRuntime> RuntimeValues = new();

        private readonly Dictionary<string, int> RuntimeBaseValues = new();

        private bool mIsInitialize;
        
        private void OnDisable()
        {
            mIsInitialize = false;
        }

        private void Initialize()
        {
            if (mIsInitialize) return;
            
            mIsInitialize = true;
            
            RuntimeValues.Clear();
            RuntimeBaseValues.Clear();
            
            if (mValueConfig != null)
                foreach (var value in mValueConfig.ValueDefinitions)
                {
                    if (!RuntimeValues.ContainsKey(value.ValueName))
                    {
                        var runtimeValue = new SoleValueRuntime(
                            value.ValueName,
                            value.DefaultValue,
                            value.HasMaxValue,
                            value.MaxValue
                        );

                        RuntimeValues.Add(value.ValueName, runtimeValue);
                        RuntimeBaseValues.Add(value.ValueName, value.DefaultValue);
                    }
                    else
                    {
                        throw new Exception($"Duplicate value name found: {value.ValueName}");
                    }
                }
            else
                throw new Exception("ValueConfigSO is not assigned!");
        }
        
        public int GetValue(string _valueName)
        {
            Initialize();
            
            if (RuntimeValues.TryGetValue(_valueName, out var valueRuntime)) return valueRuntime.GetCurrentValue();

            throw new Exception($"Value not found: {_valueName}");
        }

        public int GetMaxValue(string _valueName)
        {
            Initialize();
            
            if (RuntimeValues.TryGetValue(_valueName, out var valueRuntime)) return valueRuntime.GetMaxValue();

            throw new Exception($"Value not found: {_valueName}");
        }

        /// <summary>
        /// Make changes to properties directly
        /// 
        /// </summary>
        /// <param name="_isModifyMaxValue">Whether to make changes to DefaultValue or to MaxValue.</param>
        public void ModifyValue(string _valueName, int _modifyValue, bool _isModifyMaxValue = false)
        {
            Initialize();
            
            if (RuntimeValues.TryGetValue(_valueName, out var valueRuntime))
                valueRuntime.ModifyValue(_modifyValue, _isModifyMaxValue);
            else
                throw new Exception($"Value not found: {_valueName}");
        }

        public ModifyReference ReferenceModifyValue(string _valueName, StatModifyType _modifyType, int _modifyValue)
        {
            Initialize();
            
            if (RuntimeValues.TryGetValue(_valueName, out var valueRuntime))
                return valueRuntime.ReferenceModifyValue(_modifyType, _modifyValue);
            throw new Exception($"Value not found: {_valueName}");
        }

        public void RemoveReferenceModifyValue(string _valueName, ModifyReference _modifyReference)
        {
            Initialize();
            
            if (RuntimeValues.TryGetValue(_valueName, out var valueRuntime))
                valueRuntime.RemoveModifyReference(_modifyReference);
            else
                throw new Exception($"Value not found: {_valueName}");
        }

        /// <summary>
        /// The two int are Value and MaxValue.
        /// </summary>
        public void AddAction(string _valueName, UnityAction<int, int> _unityAction)
        {
            Initialize();
            
            if (RuntimeValues.TryGetValue(_valueName, out var valueRuntime))
                valueRuntime.OnValueChanged += _unityAction;
            else
                throw new Exception($"Value not found: {_valueName}");
        }
        
        public void RemoveAction(string _valueName, UnityAction<int, int> _unityAction)
        {
            Initialize();
            
            if (RuntimeValues.TryGetValue(_valueName, out var valueRuntime))
                valueRuntime.OnValueChanged -= _unityAction;
            else
                throw new Exception($"Value not found: {_valueName}");
        }

        /// <summary>
        /// Reset value
        /// Remove all ModifyValue, Value are set to the default base value
        /// </summary>
        /// <param name="_isModifyMaxValue">Whether to make changes to DefaultValue or to MaxValue.</param>
        public void ResetValue(string _valueName)
        {
            Initialize();
            
            if (RuntimeValues.TryGetValue(_valueName, out var valueRuntime))
                valueRuntime.ResetValue();
            else
                throw new Exception($"Value not found: {_valueName}");
        }

        public void ResetAllValues()
        {
            Initialize();

            var values = RuntimeValues.Values;

            foreach (var value in values)
            {
                value.ResetValue();
            }
        }
        
        /// <summary>
        /// Dynamically register a new value
        /// </summary>
        public void RegisterValue(string _valueName, SoleValue _initialValue)
        {
            Initialize();
            
            if (!RuntimeValues.ContainsKey(_valueName))
            {
                var runtimeValue = new SoleValueRuntime(
                    _initialValue.ValueName,
                    _initialValue.DefaultValue,
                    _initialValue.HasMaxValue,
                    _initialValue.MaxValue
                );

                RuntimeValues.Add(_valueName, runtimeValue);
            }
            else
            {
                throw new Exception($"Value {_valueName} is already registered.");
            }
        }

        /// <summary>
        /// Dynamically removing a value
        /// </summary>
        public void UnregisterValue(string _valueName)
        {
            Initialize();
            
            if (RuntimeValues.ContainsKey(_valueName))
                RuntimeValues.Remove(_valueName);
            else
                throw new Exception($"Value {_valueName} is not registered.");
        }

        public void SetValueConfigSO(ValueConfigSO valueConfigSO)
        {
            mValueConfig = valueConfigSO;
            
            Initialize();
        }
        
        /// <summary>
        /// Get all runtime values (for debugging or presentation)
        /// </summary>
        public Dictionary<string, SoleValueRuntime> GetAllValues()
        {
            Initialize();
            
            return new Dictionary<string, SoleValueRuntime>(RuntimeValues);
        }
    }
}