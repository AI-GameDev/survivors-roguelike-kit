using RGame.Framework;
using RGame.RoguelikeKit;
using UnityEngine;

namespace RGame.RoguelikeKit
{
    public abstract class BaseBuff : DescriptionBaseSO
    {
        public float Duration;
        
        public float TickInterval = 999;

        public RemoveAllBuffEvent RemoveAllBuff;
        
        [HideInInspector] public string BuffName;
        [HideInInspector] public BaseEnemy Owner;
        
        public abstract void Activate();

        public abstract void DeActivate();

        /// <summary>
        /// Tick
        /// </summary>
        public virtual void TickEffect()
        {
        }
    }
}