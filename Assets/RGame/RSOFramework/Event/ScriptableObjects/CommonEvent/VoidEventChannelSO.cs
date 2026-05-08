#region

using UnityEngine;
using UnityEngine.Events;

#endregion

namespace RGame.Framework
{
    [CreateAssetMenu(menuName = "RGame/Framework/Events/Common/Void Event Channel")]
    public class VoidEventChannelSO : DescriptionBaseSO
    {
        private event UnityAction mOnEventRaised;

        public virtual bool RaiseEvent()
        {
            if (mOnEventRaised != null)
            {
                mOnEventRaised.Invoke();
                return true;
            }

            return false;
        }

        internal void RegisterListener(UnityAction _listener)
        {
            mOnEventRaised += _listener;
        }

        internal void UnregisterListener(UnityAction _listener)
        {
            mOnEventRaised -= _listener;
        }
    }
}