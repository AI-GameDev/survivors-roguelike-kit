using System;
using UnityEngine;

namespace RGame.ScriptableCoreKit
{
    /// <summary>
    /// MonoBehaviour wrapper that exposes hierarchy lookup and caches a local table.
    /// </summary>
    public class BlackboardOwner : MonoBehaviour, IBlackboard
    {
        [SerializeField] private BlackboardTable localTable;
        [SerializeField] private BlackboardTable globalFallback;

        private IBlackboard Local => localTable;
        private IBlackboard Global => globalFallback;

        public event Action<string, object> OnChanged;

        private void Awake()
        {
            if (Local != null) Local.OnChanged  += HandleChange;
            if (Global != null) Global.OnChanged += HandleChange;
        }
        private void OnDestroy()
        {
            if (Local != null) Local.OnChanged  -= HandleChange;
            if (Global != null) Global.OnChanged -= HandleChange;
        }
        private void HandleChange(string key, object val) => OnChanged?.Invoke(key, val);

        public bool Contains(string key) => Local?.Contains(key) == true || Global?.Contains(key) == true;

        public T Get<T>(string key, T fallback = default)
        {
            if (Local != null && Local.Contains(key)) return Local.Get(key, fallback);
            return Global != null ? Global.Get(key, fallback) : fallback;
        }

        public void Set<T>(string key, T value)
        {
            if (Local == null) return;
            Local.Set(key, value);
        }

        public void Remove(string key)
        {
            if (Local == null) return;
            Local.Remove(key);
        }

        public void Rename(string oldKey, string newKey)
        {
            if (Local == null) return;
            Local.Rename(oldKey, newKey);
        }
    }
}
