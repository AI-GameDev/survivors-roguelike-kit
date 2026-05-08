using System;
using UnityEngine;

namespace RGame.Framework
{
    /// <summary>
    /// Represents a single pool configuration for a GameObject prefab
    /// </summary>
    [Serializable]
    public class SolePool
    {
        [SerializeField] private GameObject _prefab;
        [SerializeField] private int _preloadAmount = 1;
        
        public GameObject Prefab => _prefab;
        public int PreloadAmount => _preloadAmount;

        public SolePool(GameObject prefab, int preloadAmount)
        {
            _prefab = prefab;
            _preloadAmount = preloadAmount;
        }
    }
}
