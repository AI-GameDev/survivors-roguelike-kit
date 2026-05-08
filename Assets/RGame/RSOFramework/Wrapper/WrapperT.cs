using System;
using System.Collections.Generic;
using UnityEngine;

namespace RGame.Framework
{
    public class WrapperT<T> : DescriptionBaseSO
    {
        [SerializeField] protected T mValue = default;
        
        public T Value
        {
            get => mValue;
            set 
            {
                if (!EqualityComparer<T>.Default.Equals(mValue, value))
                {
                    T oldValue = mValue;
                    mValue = value;
                    OnValueChanged?.Invoke(oldValue, mValue);
                }
            }
        }
        
        public event Action<T, T> OnValueChanged;
        
        public virtual void Reset()
        {
            Value = default;
        }
        
        public virtual void CopyFrom(WrapperT<T> other)
        {
            if (other != null)
            {
                Value = other.Value;
            }
        }
        
        public virtual WrapperT<T> Clone()
        {
            var clone = CreateInstance<WrapperT<T>>();
            clone.CopyFrom(this);
            return clone;
        }
        
        public virtual bool Equals(WrapperT<T> other)
        {
            if (other == null) return false;
            return EqualityComparer<T>.Default.Equals(Value, other.Value);
        }
        
        public override string ToString()
        {
            return Value?.ToString() ?? "null";
        }

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            
        }
#endif
    }
}
