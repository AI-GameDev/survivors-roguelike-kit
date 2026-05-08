#region

using UnityEngine.Events;

#endregion

namespace RGame.Framework
{
    public abstract class ScriptableEventT<T> : DescriptionBaseSO
    {
        protected event UnityAction<T> mOnEventRaised;

        public virtual bool RaiseEvent(T _value)
        {
            if (mOnEventRaised != null)
            {
                mOnEventRaised.Invoke(_value);
                return true;
            }

            return false;
        }

        public void RegisterListener(UnityAction<T> _listener)
        {
            mOnEventRaised += _listener;
        }

        public void UnregisterListener(UnityAction<T> _listener)
        {
            mOnEventRaised -= _listener;
        }
    }
    
    public abstract class ScriptableEventT<T1, T2> : DescriptionBaseSO
    {
        protected event UnityAction<T1, T2> mOnEventRaised;

        public virtual bool RaiseEvent(T1 value1, T2 value2)
        {
            if (mOnEventRaised != null)
            {
                mOnEventRaised.Invoke(value1, value2);
                return true;
            }

            return false;
        }

        public void RegisterListener(UnityAction<T1, T2> listener)
        {
            mOnEventRaised += listener;
        }

        public void UnregisterListener(UnityAction<T1, T2> listener)
        {
            mOnEventRaised -= listener;
        }
    }
    
    public abstract class ScriptableEventT<T1, T2, T3> : DescriptionBaseSO
    {
        protected event UnityAction<T1, T2, T3> mOnEventRaised;

        public virtual bool RaiseEvent(T1 value1, T2 value2, T3 value3)
        {
            if (mOnEventRaised != null)
            {
                mOnEventRaised.Invoke(value1, value2, value3);
                return true;
            }

            return false;
        }

        public void RegisterListener(UnityAction<T1, T2, T3> listener)
        {
            mOnEventRaised += listener;
        }

        public void UnregisterListener(UnityAction<T1, T2, T3> listener)
        {
            mOnEventRaised -= listener;
        }
    }
}