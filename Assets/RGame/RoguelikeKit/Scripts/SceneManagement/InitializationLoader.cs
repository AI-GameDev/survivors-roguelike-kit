#region

using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

#endregion

namespace RGame.RoguelikeKit
{
    /// <summary>
    ///     This class is responsible for starting the game by loading the persistent managers scene
    ///     and raising the event to load the Main Menu
    /// </summary>
    public class InitializationLoader : MonoBehaviour
    {
        [SerializeField] private GameSceneSO mManagersScene;
        [SerializeField] private GameSceneSO mMenuToLoad;

        [Header("Broadcasting on")] [SerializeField]
        private AssetReference mMenuLoadChannel;

        private void Start()
        {
            // Load the persistent managers scene
            mManagersScene.sceneReference
                .LoadSceneAsync(LoadSceneMode.Additive)
                .Completed += LoadEventChannel;
        }

        private void LoadEventChannel(AsyncOperationHandle<SceneInstance> _obj)
        {
            mMenuLoadChannel.LoadAssetAsync<LoadEventChannelSO>().Completed += LoadMainMenu;
        }

        private void LoadMainMenu(AsyncOperationHandle<LoadEventChannelSO> _obj)
        {
            _obj.Result.RaiseEvent(mMenuToLoad, true);

            SceneManager.UnloadSceneAsync(0); // Initialization is the only scene in BuildSettings, thus it has index 0
        }
    }
}