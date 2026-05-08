using System;
using UnityEngine;

namespace RGame.ScriptableCoreKit
{
    /// <summary>
    /// Public blackboard contract shared by tables & components.
    /// </summary>
    public interface IBlackboard
    {
        bool Contains(string key);
        T    Get<T>(string key, T fallback = default);
        void Set<T>(string key, T value);
        void Remove(string key);
        void Rename(string oldKey, string newKey);
        event Action<string, object> OnChanged; // key, new value
    }
}
