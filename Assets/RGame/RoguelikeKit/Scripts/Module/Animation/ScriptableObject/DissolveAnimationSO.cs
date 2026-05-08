using System.Collections.Generic;
using DG.Tweening;
using RGame.Framework;
using UnityEngine;
using UnityEngine.Events;

namespace RGame.RoguelikeKit
{
    [CreateAssetMenu(fileName = "DissolveAnimationConfig", menuName = "RGame/RoguelikeKit/Animation/Dissolve")]
    public class DissolveAnimationSO : DescriptionBaseSO
    {
        [Header("Animation Timing")]
        [SerializeField] private float _animationDuration = 0.3f;
        
        [Header("Easing")]
        [SerializeField] private Ease _animationEase = Ease.Linear;

        private readonly int mCutoffProperty = Shader.PropertyToID("_Cutoff");
        private readonly int mEdgeWidthProperty = Shader.PropertyToID("_EdgeWidth");
        private readonly int _shadowColorProperty = Shader.PropertyToID("_ShadowColor");
        
        private readonly Dictionary<SpriteRenderer, AnimationData> mAnimationDataMap = new();

        private class AnimationData
        {
            public Material MaterialInstance;
            public Sequence CurrentSequence;
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
        
        public void Play(SpriteRenderer spriteRenderer, UnityAction onComplete = null)
        {
            if (spriteRenderer == null) return;
            
            if (!mAnimationDataMap.TryGetValue(spriteRenderer, out var animData))
            {
                animData = new AnimationData
                {
                    MaterialInstance = spriteRenderer.material,
                };
                mAnimationDataMap.Add(spriteRenderer, animData);
            }
          
            spriteRenderer.material = animData.MaterialInstance;
            
            StopAnimation(animData);
            CreateAnimation(animData, onComplete);
        }

        public void Stop(SpriteRenderer spriteRenderer)
        {
            if (mAnimationDataMap.TryGetValue(spriteRenderer, out var animData))
            {
                StopAnimation(animData);
            }
        }

        public void Remove(SpriteRenderer spriteRenderer)
        {
            if (mAnimationDataMap.TryGetValue(spriteRenderer, out var animData))
            {
                StopAnimation(animData);
                if (animData.MaterialInstance != null)
                {
                    Destroy(animData.MaterialInstance);
                }
                mAnimationDataMap.Remove(spriteRenderer);
            }
        }
        
        public void ResetMaterial(SpriteRenderer spriteRenderer)
        {
            if (mAnimationDataMap.TryGetValue(spriteRenderer, out var animData))
            {
                animData.MaterialInstance.SetFloat(mCutoffProperty, 0);
                animData.MaterialInstance.SetFloat(mEdgeWidthProperty, 0);
                animData.MaterialInstance.SetColor(_shadowColorProperty, new Color(0, 0, 0, 0.7f));
            }
        }

        private void StopAnimation(AnimationData animData)
        {
            if (animData.CurrentSequence != null && animData.CurrentSequence.IsActive())
            {
                animData.CurrentSequence.Kill();
            }
            
            animData.CurrentSequence = null;
            ResetMaterial(animData.MaterialInstance);
        }
        
        private void CreateAnimation(AnimationData animData, UnityAction onComplete = null)
        {
            animData.CurrentSequence = DOTween.Sequence();
            
            animData.MaterialInstance.SetFloat(mEdgeWidthProperty, 0.05f);
            
            animData.CurrentSequence.Append(
                DOTween.To(
                    () => animData.MaterialInstance.GetFloat(mCutoffProperty),
                    x => animData.MaterialInstance.SetFloat(mCutoffProperty, x),
                    1.0f,
                    _animationDuration
                ).SetEase(_animationEase)
            );
            
            animData.CurrentSequence.Join(
                DOTween.To(
                    () => animData.MaterialInstance.GetColor(_shadowColorProperty),
                    x => animData.MaterialInstance.SetColor(_shadowColorProperty, x),
                    new Color(0, 0, 0, 0),
                    _animationDuration * 0.5f
                ).SetEase(_animationEase)
            );
            
            if (onComplete != null)
            {
                animData.CurrentSequence.OnComplete(() => onComplete.Invoke());
            }
        }

        private void ResetMaterial(Material material)
        {
            if (material != null)
            {
                material.SetFloat(mCutoffProperty, 0);
                material.SetFloat(mEdgeWidthProperty, 0);
                material.SetColor(_shadowColorProperty, new Color(0, 0, 0, 0.7f));
            }
        }
    }
}
