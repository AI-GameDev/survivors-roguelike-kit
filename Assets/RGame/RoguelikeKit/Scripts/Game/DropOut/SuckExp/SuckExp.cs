#region

using UnityEngine;

#endregion

namespace RGame.RoguelikeKit
{
    public class SuckExp : IDropOut
    {
        public override void Do()
        {
            var exps = GameObject.FindGameObjectsWithTag("Exp");

            foreach (var item in exps) item.GetComponent<IDropOut>().IsMove = true;
        }
    }
}