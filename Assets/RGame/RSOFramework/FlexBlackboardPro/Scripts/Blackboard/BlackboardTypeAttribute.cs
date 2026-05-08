using System;

namespace RGame.ScriptableCoreKit
{
    /// <summary>
    /// Attribute used by TypeRegistry to auto-register new slot types.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class BlackboardTypeAttribute : Attribute
    {
        public string DisplayName { get; }
        public BlackboardTypeAttribute(string displayName) => DisplayName = displayName;
    }
}