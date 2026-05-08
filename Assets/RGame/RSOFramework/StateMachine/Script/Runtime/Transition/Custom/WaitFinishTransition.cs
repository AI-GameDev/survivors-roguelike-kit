using UnityEngine;

namespace RGame.ScriptableCoreKit
{
    [System.Serializable]
    public class WaitFinishTransition : BaseTransition
    {
        public override bool ShouldTransition(float deltaTime, ActionStateNode node = null)
        {
            return node.isFinish;
        }
    }
}
