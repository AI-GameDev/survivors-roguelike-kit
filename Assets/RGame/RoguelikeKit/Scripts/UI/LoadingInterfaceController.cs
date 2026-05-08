#region

using System.Collections;
using RGame.Framework;
using UnityEngine;

#endregion

namespace RGame.RoguelikeKit
{
    public class LoadingInterfaceController : MonoBehaviour
    {
        [SerializeField] private GameObject mLoadingInterface;
        [SerializeField] private GameObject mCamera;

        [Header("Listening on")] [SerializeField]
        private BoolEventChannelSO mToggleLoadingScreenEvent;

        private void OnEnable()
        {
            mToggleLoadingScreenEvent.RegisterListener(ToggleLoadingScreen);
        }

        private void OnDisable()
        {
            mToggleLoadingScreenEvent.UnregisterListener(ToggleLoadingScreen);
        }

        private void ToggleLoadingScreen(bool _state)
        {
            if (!_state)
                StartCoroutine(DelayDeActive());
            else
                mCamera.SetActive(true);
        }

        private IEnumerator DelayDeActive()
        {
            yield return new WaitForSeconds(1f);

            mLoadingInterface.SetActive(false);

            mCamera.SetActive(false);
        }
    }
}