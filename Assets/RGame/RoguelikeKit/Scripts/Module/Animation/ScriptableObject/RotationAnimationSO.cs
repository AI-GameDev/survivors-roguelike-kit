using System.Collections.Generic;
using DG.Tweening;
using RGame.Framework;
using UnityEngine;

namespace RGame.RoguelikeKit
{
    [CreateAssetMenu(fileName = "RotationAnimationConfig", menuName = "RGame/RoguelikeKit/Animation/RotationAnimation")]
    public class RotationAnimationSO : DescriptionBaseSO
    {
        [Header("Animation Settings")]
        [SerializeField] private float mRotationDuration = 1f;
        [SerializeField] private Vector3 mRotationAmount = new Vector3(0, 0, 360f);
        [SerializeField] private bool mInfiniteLoop = true;
        [SerializeField] private RotateMode mRotateMode = RotateMode.FastBeyond360;
        
        [Header("Easing")]
        [SerializeField] private Ease mRotationEase = Ease.Linear;

        private readonly Dictionary<Transform, AnimationData> mAnimationDataMap = new();

        private class AnimationData
        {
            public Tween CurrentTween;
            public Vector3 OriginalRotation;
        }

        public void OnEnable()
        {
            mAnimationDataMap.Clear();
        }

        public void OnDisable()
        {
            foreach (var data in mAnimationDataMap.Values)
            {
                StopAnimation(data);
            }
            mAnimationDataMap.Clear();
        }

        public void Play(Transform transform)
        {
            if (transform == null) return;
            
            if (!mAnimationDataMap.TryGetValue(transform, out var animData))
            {
                animData = new AnimationData
                {
                    OriginalRotation = transform.localRotation.eulerAngles
                };
                mAnimationDataMap.Add(transform, animData);
            }

            StopAnimation(animData);
            CreateAnimation(transform, animData);
        }

        public void Stop(Transform transform)
        {
            if (mAnimationDataMap.TryGetValue(transform, out var animData))
            {
                StopAnimation(animData);
                ResetRotation(transform, animData);
            }
        }

        public void Remove(Transform transform)
        {
            if (mAnimationDataMap.TryGetValue(transform, out var animData))
            {
                StopAnimation(animData);
                ResetRotation(transform, animData);
                mAnimationDataMap.Remove(transform);
            }
        }

        private void StopAnimation(AnimationData animData)
        {
            if (animData.CurrentTween != null && animData.CurrentTween.IsActive())
            {
                animData.CurrentTween.Kill();
                animData.CurrentTween = null;
            }
        }

        private void CreateAnimation(Transform transform, AnimationData animData)
        {
            animData.CurrentTween = transform.DOLocalRotate(
                mRotationAmount,
                mRotationDuration,
                mRotateMode
            )
            .SetEase(mRotationEase);

            if (mInfiniteLoop)
            {
                animData.CurrentTween.SetLoops(-1, LoopType.Restart);
            }
        }

        private void ResetRotation(Transform transform, AnimationData animData)
        {
            if (transform != null)
            {
                transform.localRotation = Quaternion.Euler(animData.OriginalRotation);
            }
        }
        
        private void OnValidate()
        {
            if (!Application.isPlaying) return;
            
            foreach (var kvp in mAnimationDataMap)
            {
                if (kvp.Key != null)
                {
                    Stop(kvp.Key);
                    Play(kvp.Key);
                }
            }
        }
    }
}