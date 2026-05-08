using UnityEditor;
using UnityEngine;

namespace RoguelikeKit
{
    /// <summary>
    /// Editor tool for setting UI anchors to match current position
    /// </summary>
    public class UIAnchorSetter : EditorWindow
    {
        [MenuItem("Tools/Set Anchors to Corners")]
        private static void SetAnchorsToCorners()
        {
            foreach (var gameObject in Selection.gameObjects)
            {
                SetAnchorsRecursively(gameObject);
            }
        }

        private static void SetAnchorsRecursively(GameObject obj)
        {
            if (obj == null) return;

            var rectTransform = obj.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                Debug.LogWarning($"GameObject '{obj.name}' has no RectTransform component, skipped.");
                return;
            }

            var parentRectTransform = rectTransform.parent as RectTransform;
            if (parentRectTransform == null)
            {
                Debug.LogWarning($"GameObject '{obj.name}' has no parent RectTransform, skipped.");
                return;
            }

            Vector2 CalculateAnchor(Vector2 anchor, Vector2 offset, float dimension)
            {
                return new Vector2(
                    anchor.x + offset.x / dimension,
                    anchor.y + offset.y / dimension
                );
            }

            rectTransform.anchorMin = CalculateAnchor(
                rectTransform.anchorMin,
                rectTransform.offsetMin,
                parentRectTransform.rect.width
            );

            rectTransform.anchorMax = CalculateAnchor(
                rectTransform.anchorMax,
                rectTransform.offsetMax,
                parentRectTransform.rect.height
            );

            rectTransform.offsetMin = rectTransform.offsetMax = Vector2.zero;
            Debug.Log($"Anchors set for: '{obj.name}'");
        }
    }
}