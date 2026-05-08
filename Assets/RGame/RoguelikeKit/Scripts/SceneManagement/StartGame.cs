#region

using UnityEngine;

#endregion

namespace RGame.RoguelikeKit
{
    /// <summary>
    ///     This class contains the function to call when play button is pressed
    /// </summary>
    public class StartGame : MonoBehaviour
    {
        [SerializeField] private bool mShowLoadScreen;

        [Header("Broadcasting on")] [SerializeField]
        private LoadEventChannelSO mLoadLevel;

        [Header("Select Scene")] [SerializeField]
        private GameSceneEventChannel mGameSceneLoad;

        private void Start()
        {
            mGameSceneLoad.OnEventRaised += StartNewGame;
        }

        private void OnDestroy()
        {
            mGameSceneLoad.OnEventRaised -= StartNewGame;
        }

        private void StartNewGame(GameSceneSO _scene)
        {
            mLoadLevel.RaiseEvent(_scene, mShowLoadScreen);
        }
    }
}