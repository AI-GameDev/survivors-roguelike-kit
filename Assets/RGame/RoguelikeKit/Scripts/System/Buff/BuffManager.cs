using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using RGame.Framework;
using UnityEngine;

namespace RGame.RoguelikeKit
{
    public class BuffManager : MonoBehaviour
    {
        [SerializeField] private AddBuffEvent _addBuffEvent;
        [SerializeField] private RemoveAllBuffEvent _removeAllBuffEvent;
        [SerializeField] private VoidEventChannelSO _clearAllBuffsEvent;
       private readonly Dictionary<BaseBuff, float> mBuffActivateDic = new Dictionary<BaseBuff, float>();
        private readonly List<BaseBuff> mBuffActivateKeys = new List<BaseBuff>();
        private readonly Dictionary<(BaseEnemy, string), BaseBuff> mOwnerBuff = new Dictionary<(BaseEnemy, string), BaseBuff>();
        private readonly List<BaseBuff> mWaitRemoveKey = new List<BaseBuff>();

        private void OnEnable()
        {
            _addBuffEvent.RegisterListener(AddBuff);
            _removeAllBuffEvent.RegisterListener(RemoveBuff);
            _clearAllBuffsEvent.RegisterListener(ClearAllBuffs);
        }

        private void OnDisable()
        {
            _addBuffEvent.UnregisterListener(AddBuff);
            _removeAllBuffEvent.UnregisterListener(RemoveBuff);
            _clearAllBuffsEvent.UnregisterListener(ClearAllBuffs);
        }

        private void FixedUpdate()
        {
            BuffTimer();
        }

        public void AddBuff(BaseEnemy _owner, BaseBuff _buff)
        {
            if (mOwnerBuff.ContainsKey((_owner, _buff.BuffName)))
            {
                var existingBuff = mOwnerBuff[(_owner, _buff.BuffName)];
                
                mBuffActivateDic[existingBuff] = 0f;
            }
            else
            {
                var buff = Instantiate(_buff);
                if (buff == null) return;

                mOwnerBuff[(_owner, buff.BuffName)] = buff;
                mBuffActivateDic.Add(buff, 0f);
                mBuffActivateKeys.Add(buff);
                buff.Owner = _owner;
                buff.Activate();
            }
        }

        public void RemoveBuff(BaseBuff _buff)
        {
            _buff.DeActivate();
            
            mBuffActivateDic.Remove(_buff);
            mBuffActivateKeys.Remove(_buff);
            
            var ownerBuffPair = mOwnerBuff.FirstOrDefault(x => x.Value == _buff);
            if (ownerBuffPair.Value != null) mOwnerBuff.Remove(ownerBuffPair.Key);
        }
        
        public void ClearAllBuffs()
        {
            var allBuffs = mBuffActivateKeys.ToList();
            foreach (var buff in allBuffs)
            {
                RemoveBuff(buff);
            }
        }
        
        private void BuffTimer()
        {
            foreach (var key in mBuffActivateKeys)
            {
                mBuffActivateDic[key] += Time.fixedDeltaTime;

                if (mBuffActivateDic[key] > key.Duration) mWaitRemoveKey.Add(key);

                if (mBuffActivateDic[key] % key.TickInterval < Time.fixedDeltaTime) key.TickEffect();
            }

            foreach (var key in mWaitRemoveKey) RemoveBuff(key);

            mWaitRemoveKey.Clear();
        }
    }
}
