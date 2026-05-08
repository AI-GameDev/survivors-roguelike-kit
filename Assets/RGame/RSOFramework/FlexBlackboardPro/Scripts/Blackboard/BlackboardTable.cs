using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace RGame.ScriptableCoreKit
{ 
    /// <summary>
    /// ScriptableObject implementation storing a list of variable slots.
    /// Implements IBlackboard with add / remove / rename helpers.
    /// </summary>
    [CreateAssetMenu(fileName = "BlackboardTable", menuName = "RGame/CoreKit/FlexBlackboard/Blackboard Table")]
    public class BlackboardTable : ScriptableObject, IBlackboard
    {
        [SerializeReference] public List<IVariableSlot> slots = new();
        
        private readonly Dictionary<string, IVariableSlot> _map = new();
        public event Action<string, object> OnChanged;

        private void OnEnable()
        {
            TypeRegistry.EnsureInitialized();
            RebuildMap();
        }

        private void OnValidate()
        {
            RebuildMap();
        }

        private void RebuildMap()
        {
            _map.Clear();
            
            for (int i = slots.Count - 1; i >= 0; i--)
            {
                var slot = slots[i];
                if (slot == null || string.IsNullOrEmpty(slot.Key))
                {
                    slots.RemoveAt(i);
                    continue;
                }

                _map.Add(slot.Key, slot);
            }
        }

        public int AddSlot(string key, Type valueType)
        {
            if (valueType == null) 
            {
                Debug.LogError("Cannot add slot with null type");
                return 0;
            }
            
            if (string.IsNullOrEmpty(key))
            {
                Debug.LogError("Cannot add slot with empty key");
                return 0;
            }
            
            var slot = TypeRegistry.CreateSlotInstance(key, valueType);
            if (slot != null)
            {
                Add(slot);
                
                #if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(this);
                UnityEditor.AssetDatabase.SaveAssets();
                #endif
                
                return 2;
            }
            else
            {
                Debug.LogError($"Failed to create slot for key '{key}' and type {valueType}");
                return 0;
            }
        }

        public void Add(IVariableSlot slot)
        {
            if (slot == null || string.IsNullOrEmpty(slot.Key)) 
            {
                Debug.LogError("Cannot add null or invalid slot");
                return;
            }
            
            slots.Add(slot);
            
            while(_map.ContainsKey(slot.Key))
            {
                slot.Key = slot.Key + "1";
            }
            
            _map.Add(slot.Key, slot);
            OnChanged?.Invoke(slot.Key, slot.Value);
            
            Debug.Log($"Added slot: Key='{slot.Key}', SlotType={slot.GetType().Name}, ValueType={slot.ValueType?.Name}");
        }

        // 其他方法保持不变...
        public bool Contains(string key) => _map.ContainsKey(key);
        
        public bool TryGetValue(string key, out object value)
        {
            if (_map.TryGetValue(key, out var slot))
            {
                value = slot.Value;
                return true;
            }
            value = null;
            return false;
        }

        public T Get<T>(string key, T fallback = default)
        {
            return _map.TryGetValue(key, out var slot) && slot is VariableSlot<T> typed
                ? typed.TypedValue
                : fallback;
        }

        public void Set<T>(string key, T value)
        {
            if (!_map.TryGetValue(key, out var slot))
            {
                slot = TypeRegistry.CreateSlotInstance(key, typeof(T));
                if (slot != null)
                {
                    slots.Add(slot);
                    _map.Add(key, slot);
                }
                else
                {
                    Debug.LogError($"Failed to create slot for key '{key}' and type {typeof(T)}");
                    return;
                }
            }

            if (slot is VariableSlot<T> typedSlot)
            {
                typedSlot.TypedValue = value;
                OnChanged?.Invoke(key, value);
                
                #if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(this);
                #endif
            }
            else
            {
                Debug.LogError($"Type mismatch for key '{key}'. Expected {typeof(T)}, got {slot?.ValueType}");
            }
        }

        public void Remove(string key)
        {
            if (!_map.TryGetValue(key, out var slot)) return;
            
            slots.Remove(slot);
            _map.Remove(key);
            OnChanged?.Invoke(key, null);
            
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            #endif
        }

        public void Rename(string oldKey, string newKey)
        {
            if (string.IsNullOrEmpty(newKey) || _map.ContainsKey(newKey) || !_map.TryGetValue(oldKey, out var slot)) 
                return;

            _map.Remove(oldKey);
            slot.Key = newKey;
            _map.Add(newKey, slot);
            OnChanged?.Invoke(oldKey, null);
            OnChanged?.Invoke(newKey, slot.Value);
            
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            #endif
        }

        public IReadOnlyCollection<string> Keys => _map.Keys;
        public IReadOnlyList<IVariableSlot> Slots => slots.AsReadOnly();

        [ContextMenu("Force Rebuild Map")]
        public void ForceRebuildMap()
        {
            RebuildMap();
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            #endif
        }

        [ContextMenu("Debug Slots")]
        public void DebugSlots()
        {
            Debug.Log($"=== BlackboardTable Debug Info ===");
            Debug.Log($"Total slots: {slots.Count}");
            Debug.Log($"Map entries: {_map.Count}");
            
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot != null)
                {
                    Debug.Log($"[{i}] Key: '{slot.Key}' | SlotType: {slot.GetType().Name} | ValueType: {slot.ValueType?.Name} | Value: {slot.Value}");
                }
                else
                {
                    Debug.Log($"[{i}] NULL SLOT");
                }
            }
        }
        
        [ContextMenu("Test Add AnimationClip")]
        public void TestAddAnimationClip()
        {
            AddSlot("TestClip", typeof(AnimationClip));
        }
    }
}
