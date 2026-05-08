using RGame.Framework;
using UnityEngine;
using UnityEngine.Events;

namespace RGame.RoguelikeKit
{
    /// <summary>
    /// Event channel for game scene loading events
    /// </summary>
    [CreateAssetMenu(menuName = "RGame/RoguelikeKit/Events/Scene/GameScene Event Channel")]
    public class GameSceneEventChannel : DescriptionBaseSO
    {
        public UnityAction<GameSceneSO> OnEventRaised;

        public void RaiseEvent(GameSceneSO locationToLoad)
        {
            if (OnEventRaised == null)
            {
                Debug.LogWarning("Scene loading requested but no listeners found");
                return;
            }

            OnEventRaised.Invoke(locationToLoad);
        }
    }
}