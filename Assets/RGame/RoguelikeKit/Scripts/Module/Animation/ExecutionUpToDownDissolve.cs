#region

using DG.Tweening;
using UnityEngine;

#endregion

namespace RGame.RoguelikeKit
{
    /// <summary>
    ///     Top to bottom disappearance animation
    /// </summary>
    public class ModuleUpToDownDissolve : Module
    {
        private readonly float duration = 0.3f;
        public SpriteRenderer SpriteRenderer;

        public void Do()
        {
            if (SpriteRenderer != null)
            {
                var bounds = SpriteRenderer.bounds;

                var topY = bounds.max.y + 0.1f;
                var bottomY = bounds.min.y;

                var material = SpriteRenderer.material;

                material.SetFloat("_Clip", topY);
                material.DOFloat(bottomY, "_Clip", duration).OnComplete(() => { Object.Destroy(SpriteRenderer.gameObject); });
            }
            else
            {
                Debug.LogError("SpriteRenderer is not assigned.");
            }
        }
    }
}