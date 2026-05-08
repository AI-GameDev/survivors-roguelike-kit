#region

using System;
using UnityEngine;
using UnityEngine.Events;

#endregion

namespace RGame.CommonStat
{
    /// <summary>
    ///     Real-time running data to avoid modifying the original data
    /// </summary>
    [Serializable]
    public class SoleValueRuntime
    {
        public string ValueName;
        private int mBaseMaxValue;

        private int mBaseValue;
        private int mCurrentMaxValue;

        private int mCurrentValue;
        private bool mHasMaxValue;

        private ModifyValueAdd mMaxValueAdd = new();
        private ModifyValuePercentAdd mMaxValuePercentAdd = new();
        private ModifyValuePercentMul mMaxValuePercentMul = new();

        private ModifyValueAdd mValueAdd = new();
        private ModifyValuePercentAdd mValuePercentAdd = new();
        private ModifyValuePercentMul mValuePercentMul = new();

        public UnityAction<int, int> OnValueChanged;

        public SoleValueRuntime(string _valueName, int _defaultValue, bool _hasMaxValue = false, int _maxValue = 0)
        {
            ValueName = _valueName;
            mBaseValue = _defaultValue;
            mHasMaxValue = _hasMaxValue;
            mBaseMaxValue = mHasMaxValue ? _maxValue : 0;

            // Initialize cached values
            UpdateValues();
        }

        /// <summary>
        ///     Gets the cached current value.
        /// </summary>
        public int GetCurrentValue()
        {
            return mCurrentValue;
        }

        /// <summary>
        ///     Gets the cached max value.
        /// </summary>
        public int GetMaxValue()
        {
            if (!mHasMaxValue) return 0;
            return mCurrentMaxValue;
        }

        /// <summary>
        ///     Updates both current and max value.
        /// </summary>
        private void UpdateValues()
        {
            UpdateMaxValue();
            UpdateCurrentValue();
        }

        /// <summary>
        ///     Updates the cached current value.
        /// </summary>
        private void UpdateCurrentValue()
        {
            var baseCurrentValue = mBaseValue + mValueAdd.GetValue();
            var mul = mValuePercentAdd.GetValue() * mValuePercentMul.GetValue();
            mCurrentValue = baseCurrentValue * mul / 10000;

            //The presence of unequal percentage additions results in the
            //current value not being normally directly equal to the maximum value
            if (mHasMaxValue && mCurrentValue > mCurrentMaxValue)
            {
                while (mCurrentValue != mCurrentMaxValue)
                {
                    mValueAdd.ModifyValue(mCurrentMaxValue - mCurrentValue);
                    UpdateCurrentValue();
                }
            }
        }

        /// <summary>
        ///     Updates the cached max value.
        /// </summary>
        private void UpdateMaxValue()
        {
            if (mHasMaxValue)
            {
                var mul = mMaxValuePercentAdd.GetValue() * mMaxValuePercentMul.GetValue();
                mCurrentMaxValue = (mBaseMaxValue + mMaxValueAdd.GetValue()) * mul / 10000;
            }
            else
            {
                mCurrentMaxValue = 0;
            }
        }

        /// <summary>
        ///     Modifies the value (current or max value).
        /// </summary>
        public void ModifyValue(int _modValue, bool _isMaxValue = false)
        {
            if (!_isMaxValue)
                ModifyCurrentValue(_modValue);
            else
                ModifyMaxValue(_modValue);

            OnValueChanged?.Invoke(mCurrentValue, mCurrentMaxValue);
        }

        private void ModifyCurrentValue(int _modValue)
        {
            if (mHasMaxValue && mCurrentValue + _modValue > mCurrentMaxValue) _modValue = mCurrentMaxValue - mCurrentValue;

            mValueAdd.ModifyValue(_modValue);
            UpdateValues();
        }

        private void ModifyMaxValue(int _modValue)
        {
            mMaxValueAdd.ModifyValue(_modValue);
            UpdateValues();
        }

        /// <summary>
        ///     Modifies values with references (default affects max value if applicable).
        /// </summary>
        public ModifyReference ReferenceModifyValue(StatModifyType _modifyType, int _modValue)
        {
            var modifyReference = new ModifyReference
            {
                ModifyType = _modifyType,
                ModifyValue = _modValue
            };

            ApplyReference(modifyReference, _modifyType);
            UpdateValues();
            OnValueChanged?.Invoke(mCurrentValue, mCurrentMaxValue);

            return modifyReference;
        }

        private void ApplyReference(ModifyReference _modifyReference, StatModifyType _modifyType)
        {
            switch (_modifyType)
            {
                case StatModifyType.Add:
                    mValueAdd.ReferenceModifyValue(_modifyReference);
                    if (mHasMaxValue) mMaxValueAdd.ReferenceModifyValue(_modifyReference);
                    break;

                case StatModifyType.PercentAdd:
                    mValuePercentAdd.ReferenceModifyValue(_modifyReference);
                    if (mHasMaxValue) mMaxValuePercentAdd.ReferenceModifyValue(_modifyReference);
                    break;

                case StatModifyType.PercentMult:
                    mValuePercentMul.ReferenceModifyValue(_modifyReference);
                    if (mHasMaxValue) mMaxValuePercentMul.ReferenceModifyValue(_modifyReference);
                    break;

                case StatModifyType.MaxAdd:
                    mMaxValueAdd.ReferenceModifyValue(_modifyReference);
                    break;

                case StatModifyType.MaxPercentAdd:
                    mMaxValuePercentAdd.ReferenceModifyValue(_modifyReference);
                    break;

                case StatModifyType.MaxPercentMult:
                    mMaxValuePercentMul.ReferenceModifyValue(_modifyReference);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(_modifyType), _modifyType, null);
            }
        }

        public void RemoveModifyReference(ModifyReference _modifyReference)
        {
            RemoveReference(_modifyReference);
            UpdateValues();
            
            OnValueChanged?.Invoke(mCurrentValue, mCurrentMaxValue);
        }

        private void RemoveReference(ModifyReference _modifyReference)
        {
            switch (_modifyReference.ModifyType)
            {
                case StatModifyType.Add:
                    mValueAdd.RemoveReference(_modifyReference);
                    if (mHasMaxValue) mMaxValueAdd.RemoveReference(_modifyReference);
                    break;

                case StatModifyType.PercentAdd:
                    mValuePercentAdd.RemoveReference(_modifyReference);
                    if (mHasMaxValue) mMaxValuePercentAdd.RemoveReference(_modifyReference);
                    break;

                case StatModifyType.PercentMult:
                    mValuePercentMul.RemoveReference(_modifyReference);
                    if (mHasMaxValue) mMaxValuePercentMul.RemoveReference(_modifyReference);
                    break;

                case StatModifyType.MaxAdd:
                    mMaxValueAdd.RemoveReference(_modifyReference);
                    break;

                case StatModifyType.MaxPercentAdd:
                    mMaxValuePercentAdd.RemoveReference(_modifyReference);
                    break;

                case StatModifyType.MaxPercentMult:
                    mMaxValuePercentMul.RemoveReference(_modifyReference);
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public void ResetValue()
        {
            mValueAdd.ResetValue();
            mValuePercentAdd.ResetValue();
            mValuePercentMul.ResetValue();
            mMaxValueAdd.ResetValue();
            mMaxValuePercentAdd.ResetValue();
            mMaxValuePercentMul.ResetValue();

            UpdateValues();
            OnValueChanged?.Invoke(mCurrentValue, mCurrentMaxValue);
        }
    }
}