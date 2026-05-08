using System;
using UnityEngine;

namespace RGame.Framework
{
    /// <summary>
    /// Definition for a pool that can be serialized in the inspector
    /// </summary>
    [Serializable]
    public class PoolDefinition
    {
        [SerializeField] private string _key;
        [SerializeField] private SolePool _pool;
        
        public string Key => _key;
        public SolePool Pool => _pool;

        public PoolDefinition(string key, SolePool pool)
        {
            _key = key;
            _pool = pool;
        }
    }
}
