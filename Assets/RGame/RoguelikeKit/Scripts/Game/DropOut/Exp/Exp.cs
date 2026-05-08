using RGame.CommonStat;
using UnityEngine;

namespace RGame.RoguelikeKit
{
    public class Exp : IDropOut
    {
        [SerializeField] private int _exp = 1;
        
        public override void Do()
        {
            _stat.ModifyValue("Exp", (int)Mathf.Round(_exp * _stat.GetValue("Growth") * 0.01f));
        }
    }
}