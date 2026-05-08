using System;
using RGame.CommonStat;
using RGame.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace RGame.RoguelikeKit
{
    public class SkillBase : MonoBehaviour, IPoolR
    {
        [SerializeField] protected AddBuffEvent _addBuffEvent;
        [SerializeField] protected BaseBuff _baseBuff;
        
        public UnityAction<SkillBase> OnDeath { get; set; }
        
        public string Key { get; set; }

        public void Request()
        {
            SkillRequest();
        }

        public void Return()
        {
            
        }
        
        public virtual void SkillRequest(){}

        protected void Attack(BaseEnemy meleeEnemy)
        {
            if (_addBuffEvent != null && _baseBuff != null)
            {
                _addBuffEvent.RaiseEvent(meleeEnemy, _baseBuff);
            }
        }
    }
}
