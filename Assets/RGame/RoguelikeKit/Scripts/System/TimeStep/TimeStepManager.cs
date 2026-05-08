using System;
using System.Collections.Generic;
using RGame.Framework;
using UnityEngine;
using UnityEngine.Events;

namespace RGame.RoguelikeKit
{
    public class TimerData
    {
        public float Timer;
        public FloatWrapper EndTime;
        public UnityAction Callback;
        
        public TimerData(FloatWrapper endTime, UnityAction callback)
        {
            EndTime = endTime;
            Callback = callback;
        }
    }
    
    public class TimeStepManager : MonoBehaviour
    {
        [SerializeField] private VoidEventChannelSO mFixedUpdateChannel;
        [SerializeField] private AddTimerChannel _addTimerChannel;
        
        private List<TimerData> _activeTimers = new List<TimerData>();

        private void OnEnable()
        {
            _addTimerChannel.RegisterListener(AddTimer);
        }

        private void OnDisable()
        {
            _addTimerChannel.UnregisterListener(AddTimer);
        }
        
        private void FixedUpdate()
        {
            mFixedUpdateChannel.RaiseEvent();

            for (int i = 0; i < _activeTimers.Count; i++)
            {
                _activeTimers[i].Timer += Time.fixedDeltaTime;

                if (_activeTimers[i].Timer > _activeTimers[i].EndTime.Value)
                {
                    _activeTimers[i].Timer -= _activeTimers[i].EndTime.Value;
                    _activeTimers[i].Callback?.Invoke();
                }
            }
        }
        
        private void AddTimer(FloatWrapper endTime, UnityAction callback)
        {
            _activeTimers.Add(new TimerData(endTime, callback));
        }
    }
}
