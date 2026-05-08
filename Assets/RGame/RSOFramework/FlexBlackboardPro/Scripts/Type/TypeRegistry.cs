using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace RGame.ScriptableCoreKit
{
    /// <summary>
    /// Static registry scanning all types annotated with BlackboardTypeAttribute.
    /// </summary>
    public static class TypeRegistry
    {
        private static readonly Dictionary<string, Type> _displayNameToValueType = new();
        private static bool _initialized;

        public static IReadOnlyDictionary<string, Type> Registered => _displayNameToValueType;

        public static void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;
            _displayNameToValueType.Clear();

            var commonTypes = new List<Type>
            {
                typeof(int),
                typeof(float),
                typeof(bool),
                typeof(string),

                typeof(Vector2),
                typeof(Vector3),
                typeof(Vector4),
                typeof(Quaternion),
                typeof(Color),
                typeof(Rect),
                typeof(Bounds),
                typeof(Vector2Int),
                typeof(Vector3Int),
                typeof(LayerMask),

                typeof(AnimationCurve),
                typeof(AnimationClip),
                typeof(AudioClip),
                typeof(Font),
                typeof(Material),
                typeof(Mesh),
                typeof(Texture),
                typeof(Texture2D),
                typeof(Sprite),

                typeof(GameObject),
                typeof(Transform),
                typeof(RectTransform),
                typeof(Camera),
                typeof(Light),
                typeof(Rigidbody),
                typeof(Collider),

                typeof(ScriptableObject),
                typeof(UnityEngine.Object)
            };

            foreach (var valueType in commonTypes)
            {
                string displayName = GetDisplayName(valueType);

                _displayNameToValueType[displayName] = valueType;
            }
        }

        private static string GetDisplayName(Type type)
        {
            return type.Name switch
            {
                "Single" => "Float",
                "Int32" => "Int",
                "Boolean" => "Bool",
                _ => type.Name
            };
        }

        public static bool RegisterType(string typeName)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            Type found = null;

            foreach (var asm in assemblies)
            {
                try
                {
                    found = asm.GetTypes().FirstOrDefault(t => t.Name == typeName);
                    if (found != null) break;

                    found = asm.GetType(typeName, false, true);
                    if (found != null) break;
                }
                catch (ReflectionTypeLoadException ex)
                {
                    Debug.LogWarning($"Failed to load some types from assembly {asm.FullName}: {ex.Message}");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Error searching in assembly {asm.FullName}: {ex.Message}");
                }
            }

            if (found == null)
            {
                Debug.LogError($"TypeRegistry: Not Find Type '{typeName}'");
                return false;
            }

            if (!IsSerializableType(found))
            {
                Debug.LogWarning($"TypeRegistry: Type '{typeName}' Error");
            }

            string displayName = GetDisplayName(found);
            _displayNameToValueType[displayName] = found;

            Debug.Log($"TypeRegistry: Register '{displayName}' -> {found.FullName}");
            return true;
        }

        private static bool IsSerializableType(Type type)
        {
            if (type.IsAbstract || type.IsInterface || type.IsGenericType)
                return false;

            if (type.IsSubclassOf(typeof(UnityEngine.Object)))
                return true;

            if (type.IsPrimitive || type == typeof(string))
                return true;

            if (type.IsSerializable)
                return true;

            var unityTypes = new[]
            {
                typeof(Vector2), typeof(Vector3), typeof(Vector4),
                typeof(Quaternion), typeof(Color), typeof(Rect),
                typeof(Bounds), typeof(Vector2Int), typeof(Vector3Int),
                typeof(LayerMask), typeof(AnimationCurve)
            };

            return unityTypes.Contains(type);
        }

        public static string[] GetAllDisplayNames()
        {
            EnsureInitialized();
            return _displayNameToValueType.Keys.OrderBy(x => x).ToArray();
        }

        public static Type GetValueType(string displayName)
        {
            EnsureInitialized();
            return _displayNameToValueType.TryGetValue(displayName, out var type) ? type : null;
        }

        public static IVariableSlot CreateSlotInstance(string key, Type valueType)
        {
            try
            {
                var slotType = typeof(VariableSlot<>).MakeGenericType(valueType);

                var constructor = slotType.GetConstructor(new[] { typeof(string), valueType });
                if (constructor != null)
                {
                    var defaultValue = GetDefaultValue(valueType);
                    var instance = (IVariableSlot)constructor.Invoke(new object[] { key, defaultValue });
                    return instance;
                }

                var keyConstructor = slotType.GetConstructor(new[] { typeof(string) });
                if (keyConstructor != null)
                {
                    var instance = (IVariableSlot)keyConstructor.Invoke(new object[] { key });
                    Debug.Log($"Created slot using key constructor: {key} -> {valueType.Name}");
                    return instance;
                }

                var defaultInstance = (IVariableSlot)Activator.CreateInstance(slotType);
                defaultInstance.Key = key;
                Debug.Log($"Created slot using default constructor: {key} -> {valueType.Name}");
                return defaultInstance;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to create slot instance for {valueType}: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }

        private static object GetDefaultValue(Type type)
        {
            if (type.IsValueType)
            {
                return Activator.CreateInstance(type);
            }

            return null;
        }
    }
}
