using UnityEngine;

namespace RGame.RoguelikeKit
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class MatchTargetSorting : MonoBehaviour
    {
        [SerializeField] private int _extraAdd;
        private SpriteRenderer _spriteRenderer;
        [SerializeField] private SpriteRenderer _targetRenderer;

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void FixedUpdate()
        {
            if (_spriteRenderer == null || _targetRenderer == null)
                return;
            
            _spriteRenderer.sortingLayerID = _targetRenderer.sortingLayerID;
            _spriteRenderer.sortingOrder   = _targetRenderer.sortingOrder + _extraAdd;
        }
    }
}
