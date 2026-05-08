using System.Collections.Generic;
using DG.Tweening;
using RGame.Framework;
using UnityEngine;

namespace RGame.RoguelikeKit
{
    [CreateAssetMenu(fileName = "HitAnimationConfig", menuName = "RGame/RoguelikeKit/Animation/HitAnimation")]
    public class HitAnimationSO : DescriptionBaseSO
    {
        [Header("Animation Timing")]
        [SerializeField] private float mHitDuration = 0.1f;
        [SerializeField] private float mFadeDuration = 0.1f;

        [Header("Colors")]
        [SerializeField] private Color mHitColor = Color.white;
        [SerializeField] private Color mNormalColor = new Color(0, 0, 0, 0);

        [Header("Easing")]
        [SerializeField] private Ease mHitEase = Ease.Linear;
        [SerializeField] private Ease mFadeEase = Ease.Linear;

        private readonly int _blendColorId = Shader.PropertyToID("_BlendColor");

        private readonly Dictionary<SpriteRenderer, Sequence> _seqMap = new();

        public void OnEnable() => _seqMap.Clear();

        public void OnDisable()
        {
            foreach (var seq in _seqMap.Values) seq.Kill();
            _seqMap.Clear();
        }

        public void Play(SpriteRenderer sr)
        {
            if (!sr) return;

            // if already animating, stop first
            Stop(sr);

            Sequence seq = DOTween.Sequence();

            seq.Append(
                DOTween.To(
                        () => sr.material.GetColor(_blendColorId),
                        c => sr.material.SetColor(_blendColorId, c),
                        mHitColor,
                        mHitDuration)
                    .SetEase(mHitEase)
            );

            seq.Append(
                DOTween.To(
                        () => sr.material.GetColor(_blendColorId),
                        c => sr.material.SetColor(_blendColorId, c),
                        mNormalColor,
                        mFadeDuration)
                    .SetEase(mFadeEase)
            );

            _seqMap[sr] = seq;
        }

        public void Stop(SpriteRenderer sr)
        {
            if (_seqMap.TryGetValue(sr, out var seq))
            {
                seq.Kill();
                _seqMap.Remove(sr);
            }

            if (sr) ResetMaterial(sr.material);
        }

        public void Remove(SpriteRenderer sr) => Stop(sr);

        public void ResetMaterial(Material mat)
        {
            if (mat) mat.SetColor(_blendColorId, mNormalColor);
        }
    }
}
