using System.Collections.Generic;
using UnityEngine;

namespace RGame.ScriptableCoreKit
{
    /// <summary>
    /// Entry state that automatically transitions to the first connected state
    /// </summary>
    public class EntryStateNode : ActionStateNode
    {
        public override string GetStateName()
        {
            return "Entry";
        }

        public override string GetDisplayName()
        {
            return "Entry";
        }
    }
}