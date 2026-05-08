using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace RoguelikeKit
{
    public class BarAnimator
    {
        private readonly Image mBar;
        private readonly float mDelayTime;
        private readonly Image mTransitionBar;
        private readonly bool _reverse;
        private Tween mAnimationTween;
        private bool _disposed;

        public BarAnimator(Image _bar, Image _transitionBar, float _delayTime, bool reverse = false)
        {
            mBar = _bar;
            mTransitionBar = _transitionBar;
            mDelayTime = _delayTime;
            _reverse = reverse;
            
            if (_reverse)
            {
                mTransitionBar = _bar;
                mBar = _transitionBar;
            }
        }

        public void SetValue(int _newValue, int _maxValue)
        {
            if (_disposed) return;
            
            var targetFillAmount = _newValue * 1.0f / _maxValue;
            SetValue(targetFillAmount);
        }

        public void SetValue(float line)
        {
            if (_disposed) return;
            
            mAnimationTween?.Kill();
            
            mBar.fillAmount = line;
            SetAlpha(mTransitionBar, 1f);
            
            Sequence seq = DOTween.Sequence();
            seq.AppendInterval(mDelayTime);
            seq.Append(mTransitionBar.DOFade(0f, 0.4f));
            seq.OnComplete(() =>
            {
                if (_disposed) return;
                mTransitionBar.fillAmount = line;
                SetAlpha(mTransitionBar, 1f);
            });

            mAnimationTween = seq;
        }
        
        private void SetAlpha(Image image, float alpha)
        {
            if (_disposed) return;
            if (image == null) return;
            
            Color c = image.color;
            c.a = alpha;
            image.color = c;
        }

        public void Kill()
        {
            if (_disposed) return;
            
            _disposed = true;
            mAnimationTween?.Kill();
            mAnimationTween = null;
        }
    }
}
