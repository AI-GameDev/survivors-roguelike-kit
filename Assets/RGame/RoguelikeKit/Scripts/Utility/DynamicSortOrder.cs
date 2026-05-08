using System.Linq;
using UnityEngine;

namespace RGame.RoguelikeKit
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class DynamicSortOrder : MonoBehaviour
    {
        [Tooltip("SpriteRenderers on this GameObject or its children that should NOT be auto-sorted")]
        [SerializeField] private SpriteRenderer[] excludedRenderers;

        private SpriteRenderer[] _spriteRenderers;

        private void Awake()
        {
            _spriteRenderers = GetComponentsInChildren<SpriteRenderer>()
                .Where(sr => excludedRenderers == null || !excludedRenderers.Contains(sr))
                .ToArray();
        }

        private void FixedUpdate()
        {
            int order = (int)(-transform.position.y * 100);
            
            foreach (var sr in _spriteRenderers)
            {
                sr.sortingOrder = order;
            }
        }
    }
}