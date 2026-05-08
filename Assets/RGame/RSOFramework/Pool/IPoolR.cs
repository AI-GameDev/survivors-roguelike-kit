using UnityEngine;

namespace RGame.Framework
{
    public interface IPoolR
    {
        public string Key { get; set; }
        void Request();
        void Return();
    }
}
