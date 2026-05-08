#region

using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

#endregion

namespace RGame.RoguelikeKit
{
    public class FadeController : MonoBehaviour
    {
        [SerializeField] private FadeChannelSO mFadeChannelSO;
        [SerializeField] private Image mImageComponent;
        [SerializeField] private GameObject mCamera;

        private void OnEnable()
        {
            mFadeChannelSO.OnEventRaised += InitiateFade;
        }

        private void OnDisable()
        {
            mFadeChannelSO.OnEventRaised -= InitiateFade;
        }

        /// <summary>
        ///     Controls the fade-in and fade-out.
        /// </summary>
        /// <param name="_fadeIn">If false, the screen becomes black. If true, rectangle fades out and gameplay is visible.</param>
        /// <param name="_duration">How long it takes to the image to fade in/out.</param>
        /// <param name="_desiredColor">Target color for the image to reach. Disregarded when fading out.</param>
        private void InitiateFade(bool _fadeIn, float _duration, Color _desiredColor)
        {
            if (!_fadeIn) mCamera.SetActive(true);

            if (_fadeIn)
                StartCoroutine(DelayFade(_duration, _desiredColor));
            else
                mImageComponent.DOBlendableColor(_desiredColor, _duration);
        }

        private IEnumerator DelayFade(float _duration, Color _desiredColor)
        {
            yield return new WaitForSeconds(1f);

            mImageComponent.DOBlendableColor(_desiredColor, _duration);
        }
    }
}