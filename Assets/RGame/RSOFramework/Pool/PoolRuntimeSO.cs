using System.Collections.Generic;
using RGame.Framework;
using UnityEngine;
using System;
using RGame.RoguelikeKit;

namespace RGame.Framework
{
    [CreateAssetMenu(fileName = "New Pool Runtime", menuName = "RGame/Framework/Pool/Pool Runtime")]
    public class PoolRuntimeSO : DescriptionBaseSO
    {
        [Tooltip("Define pool configurations for different prefabs")]
        [SerializeField] private List<PoolDefinition> _poolDefinitions = new List<PoolDefinition>();
        
        private readonly Dictionary<string, SolePool> _poolConfigs = new Dictionary<string, SolePool>();
        private readonly Dictionary<string, Queue<GameObject>> _pooledObjects = new Dictionary<string, Queue<GameObject>>();
        private readonly Dictionary<string, Transform> _poolContainers = new Dictionary<string, Transform>();
        private readonly Dictionary<string, HashSet<GameObject>> _activeObjects = new Dictionary<string, HashSet<GameObject>>();
        
        private Transform _rootTransform;
        private bool _isInitialized;

        private void OnDisable()
        {
            _poolConfigs.Clear();
            _pooledObjects.Clear();
            _poolContainers.Clear();
            _activeObjects.Clear();
            _rootTransform = null;
            _isInitialized = false;
        }

        public void Initialize(Transform root)
        {
            if (_isInitialized)
                return;
            
            _rootTransform = root ?? throw new ArgumentNullException(nameof(root), "Root transform cannot be null");
            
            foreach (var definition in _poolDefinitions)
            {
                if (string.IsNullOrEmpty(definition.Key))
                {
                    Debug.LogError($"[{nameof(PoolRuntimeSO)}] Pool definition has an empty key");
                    continue;
                }
                
                if (definition.Pool?.Prefab == null)
                {
                    Debug.LogError($"[{nameof(PoolRuntimeSO)}] Pool definition '{definition.Key}' has a null prefab");
                    continue;
                }
                
                _poolConfigs[definition.Key] = definition.Pool;
            }
            
            foreach (var kvp in _poolConfigs)
            {
                string key = kvp.Key;
                SolePool pool = kvp.Value;
                
                CreatePoolContainer(key);
                PreloadObjects(key, pool);
            }
            
            _isInitialized = true;
        }
        
        private void CreatePoolContainer(string key)
        {
            GameObject container = new GameObject($"Pool_{key}");
            container.transform.SetParent(_rootTransform);
            _poolContainers[key] = container.transform;
            _pooledObjects[key] = new Queue<GameObject>();
            _activeObjects[key] = new HashSet<GameObject>();
        }
        
        private void PreloadObjects(string key, SolePool pool)
        {
            for (int i = 0; i < pool.PreloadAmount; i++)
            {
                CreatePooledObject(key, pool.Prefab);
            }
        }

        public GameObject Request(string key)
        {
            if (!EnsureInitialized())
                return null;
            
            if (!ValidatePoolExists(key))
                return null;
            
            GameObject obj = GetObjectFromPool(key);
            ActivateObject(obj, key);
            _activeObjects[key].Add(obj);
            
            return obj;
        }
        
        public void Return(GameObject obj)
        {
            if (!EnsureInitialized())
                return;
            
            string key = obj.GetComponent<IPoolR>().Key;
            
            if (!ValidatePoolExists(key))
                return;
            
            if (obj == null)
            {
                Debug.LogError($"[{nameof(PoolRuntimeSO)}] Cannot return null object to pool '{key}'");
                return;
            }
            
            _activeObjects[key].Remove(obj);
            DeactivateObject(obj, key);
            ReturnObjectToPool(obj, key);
        }

        public void RecycleAll(string key)
        {
            if (!ValidatePoolExists(key))
                return;

            var objectsToRecycle = new List<GameObject>(_activeObjects[key]);
            
            foreach (var obj in objectsToRecycle)
            {
                if (obj != null)
                {
                    Return(obj);
                }
            }
        }

        public void RecycleAll()
        {
            var keys = _activeObjects.Keys;
            foreach (var key in keys)
            {
                RecycleAll(key);
            }
        }

        public IEnumerable<GameObject> GetActiveObjects(string key)
        {
            if (_activeObjects.TryGetValue(key, out var objects))
            {
                return objects;
            }
            return Array.Empty<GameObject>();
        }

        public bool Register(PoolDefinition poolDefinition)
        {
            if (!EnsureInitialized())
                return false;
            
            if (poolDefinition == null)
            {
                Debug.LogError($"[{nameof(PoolRuntimeSO)}] Cannot register null pool definition");
                return false;
            }
            
            string key = poolDefinition.Key;
            SolePool pool = poolDefinition.Pool;
            
            if (string.IsNullOrEmpty(key))
            {
                Debug.LogError($"[{nameof(PoolRuntimeSO)}] Cannot register pool with null or empty key");
                return false;
            }
            
            if (pool?.Prefab == null)
            {
                Debug.LogError($"[{nameof(PoolRuntimeSO)}] Cannot register pool '{key}' with null prefab");
                return false;
            }
            
            if (_poolConfigs.ContainsKey(key))
            {
                Debug.LogWarning($"[{nameof(PoolRuntimeSO)}] Pool with key '{key}' already exists. Unregister it first to replace.");
                return false;
            }
            
            _poolConfigs[key] = pool;
            CreatePoolContainer(key);
            PreloadObjects(key, pool);

            return true;
        }
        
        public bool Unregister(string key)
        {
            if (!EnsureInitialized())
                return false;
            
            if (!ValidatePoolExists(key))
                return false;
            
            while (_pooledObjects[key].Count > 0)
            {
                GameObject obj = _pooledObjects[key].Dequeue();
                if (obj != null)
                {
                    Destroy(obj);
                }
            }
            
            if (_poolContainers[key] != null)
            {
                Destroy(_poolContainers[key].gameObject);
            }
            
            _poolConfigs.Remove(key);
            _pooledObjects.Remove(key);
            _poolContainers.Remove(key);
            _activeObjects.Remove(key);
            
            return true;
        }
        
        private GameObject GetObjectFromPool(string key)
        {
            GameObject obj;
            
            if (_pooledObjects[key].Count > 0)
            {
                obj = _pooledObjects[key].Dequeue();
            }
            else
            {
                obj = CreatePooledObject(key, _poolConfigs[key].Prefab);
            }
            
            return obj;
        }
        
        private GameObject CreatePooledObject(string key, GameObject prefab)
        {
            if (!ValidateObjectContainIPoolR(prefab))
            {
                return null;
            }
            
            GameObject obj = Instantiate(prefab, _poolContainers[key], true);
            obj.GetComponent<IPoolR>().Key = key;
            obj.name = $"{prefab.name}_Pooled";
            obj.SetActive(false);
            
            return obj;
        }
        
        private void ActivateObject(GameObject obj, string key)
        {
            obj.SetActive(true);
            obj.GetComponent<IPoolR>().Request();
        }
        
        private void DeactivateObject(GameObject obj, string key)
        {
            obj.GetComponent<IPoolR>().Return();
            obj.SetActive(false);
        }
        
        private void ReturnObjectToPool(GameObject obj, string key)
        {
            try
            {
                obj.transform.SetParent(_poolContainers[key]);
                _pooledObjects[key].Enqueue(obj);
            }
            catch
            {
            }
        }
        
        private bool EnsureInitialized()
        {
            if (!_isInitialized)
            {
                GameObject obj = new GameObject("PoolObjects");
                _rootTransform = obj.transform;
                GameObject.DontDestroyOnLoad(obj);
                Initialize(obj.transform);
            }
            
            return true;
        }
        
        private bool ValidatePoolExists(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                Debug.LogError($"[{nameof(PoolRuntimeSO)}] Pool key cannot be null or empty");
                return false;
            }
            
            if (!_pooledObjects.ContainsKey(key))
            {
                Debug.LogError($"[{nameof(PoolRuntimeSO)}] Pool with key '{key}' does not exist");
                return false;
            }
            
            return true;
        }

        private bool ValidateObjectContainIPoolR(GameObject obj)
        {
            var iPoolR = obj.GetComponent<IPoolR>();

            if (iPoolR == null)
            {
                Debug.LogError(obj.name + " is not a IPoolR");
                return false;
            }
            
            return true;
        }
    }
}
