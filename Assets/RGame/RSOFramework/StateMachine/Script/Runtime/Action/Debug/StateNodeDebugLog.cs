#region

using UnityEngine;

#endregion

namespace RGame.ScriptableCoreKit
{
    public class StateNodeDebugLog : ActionStateNode
    {
        [SerializeField] private string logText;

        public override void OnEnter()
        {
            base.OnEnter();

            Debug.Log(logText);
        }

        public override string GetStateName()
        {
            return "Debug Log";
        }

        public override string GetDisplayName()
        {
            return "Debug Log";
        }
    }
}