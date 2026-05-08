using UnityEngine;

namespace RGame.RoguelikeKit
{
    public class Purse : IDropOut
    {
        [SerializeField] private int GoldCount = 50;
        
        public override void Do()
        {
            _globalConfig.CurrentGetGold += GoldCount;
        }
    }
}
