using UnityEngine;

namespace RGame.RoguelikeKit
{
    public class ReplyHp : IDropOut
    {
        [SerializeField]
        private int _replyHP = 20;
        
        public override void Do()
        {
            _stat.ModifyValue("HP",_replyHP);
        }
    }
}