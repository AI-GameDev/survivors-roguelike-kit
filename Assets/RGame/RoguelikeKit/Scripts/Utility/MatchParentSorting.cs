using UnityEngine;

namespace RGame.RoguelikeKit
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class MatchParentSorting : MonoBehaviour
    {
        [SerializeField] private int _extraAdd;
        private SpriteRenderer _spriteRenderer;
        private SpriteRenderer _parentRenderer;

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();

            if (transform.parent != null)
            {
                _parentRenderer = transform.parent.GetComponent<SpriteRenderer>();
                if (_parentRenderer == null)
                {
                    Debug.LogWarning($"<color=yellow>[MatchParentSorting]</color> Parent of '{name}' has no SpriteRenderer.");
                }
            }
            else
            {
                Debug.LogWarning($"<color=yellow>[MatchParentSorting]</color> '{name}' has no parent; will not update sorting.");
            }
        }

        private void FixedUpdate()
        {
            if (_spriteRenderer == null || _parentRenderer == null)
                return;
            
            _spriteRenderer.sortingLayerID = _parentRenderer.sortingLayerID;
            _spriteRenderer.sortingOrder   = _parentRenderer.sortingOrder + _extraAdd;
        }
    }
}
