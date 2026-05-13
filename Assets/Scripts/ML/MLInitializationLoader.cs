#region

using RGame.RoguelikeKit;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

#endregion

namespace RGame.MLAgents
{
    /// <summary>
    ///     ML-Agents 학습용 빠른 진입 로더.
    ///     MainMenu를 거치지 않고 PersistentManagers를 additive 로드한 뒤
    ///     LevelAndCharacterSO를 기본값으로 채우고 LoadLevel 채널을 직접 raise한다.
    ///     기존 InitializationLoader는 손대지 않으며, 이 컴포넌트는 별도 ML 진입 씬에서만 사용한다.
    /// </summary>
    public class MLInitializationLoader : MonoBehaviour
    {
        [Header("Scenes")]
        [SerializeField] private GameSceneSO mManagersScene;
        [SerializeField] private GameSceneSO mGameLevelScene;

        [Header("Broadcasting on")]
        [SerializeField] private AssetReference mLoadLevelChannel;

        [Header("Run selection (overwritten at runtime)")]
        [SerializeField] private LevelAndCharacterSO mRunSelection;
        [SerializeField] private CharacterSelectConfigSO mDefaultCharacter;
        [SerializeField] private LevelConfigSO mDefaultLevel;

        private Scene _bootstrapScene;

        private void Start()
        {
            _bootstrapScene = gameObject.scene;
            DontDestroyOnLoad(gameObject);

            mRunSelection.SelectCharacterSo = mDefaultCharacter;
            mRunSelection.SelectLevelSO = mDefaultLevel;

            mManagersScene.sceneReference
                .LoadSceneAsync(LoadSceneMode.Additive)
                .Completed += OnManagersLoaded;
        }

        private void OnManagersLoaded(AsyncOperationHandle<SceneInstance> _obj)
        {
            mLoadLevelChannel.LoadAssetAsync<LoadEventChannelSO>().Completed += OnChannelLoaded;
        }

        private void OnChannelLoaded(AsyncOperationHandle<LoadEventChannelSO> _obj)
        {
            _obj.Result.RaiseEvent(mGameLevelScene, true);

            if (_bootstrapScene.IsValid() && _bootstrapScene.isLoaded)
            {
                SceneManager.UnloadSceneAsync(_bootstrapScene);
            }
        }
    }
}
