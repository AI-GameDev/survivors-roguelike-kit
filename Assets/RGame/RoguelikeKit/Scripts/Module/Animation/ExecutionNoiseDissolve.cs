#region

using DG.Tweening;
using UnityEngine;

#endregion

namespace RGame.RoguelikeKit
{
    public class ModuleNoiseDissolve : Module
    {
        private readonly float duration = 0.4f;
        private readonly SpriteRenderer spriteRenderer;

        public void Do()
        {
            var material = spriteRenderer.material;

            material.DOFloat(0.8f, "_Clip", duration).From(-0.1f).OnComplete(() => { Object.Destroy(spriteRenderer.gameObject); });
        }
    }
}