using RGame.Framework;
using UnityEngine;
using UnityEngine.Events;

namespace RGame.RoguelikeKit
{
    /// <summary>
    /// ScriptableObject event channel for map generation events
    /// </summary>
    [CreateAssetMenu(menuName = "RGame/RoguelikeKit/Events/Map/GeneratorMapChannel")]
    public class GeneratorMapChannelSO : DescriptionBaseSO
    {
        public event UnityAction<MapConfigSO, Transform> OnEventRaised;

        public void RaiseEvent(MapConfigSO config, Transform parent)
        {
            OnEventRaised?.Invoke(config, parent);
        }
    }
}