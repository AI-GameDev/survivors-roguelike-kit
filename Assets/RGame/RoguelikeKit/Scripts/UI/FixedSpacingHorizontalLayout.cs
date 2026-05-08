using UnityEngine;
using UnityEngine.UI;

namespace RGame.RoguelikeKit
{
    [AddComponentMenu("Layout/Fixed Spacing Horizontal Layout")]
    [RequireComponent(typeof(RectTransform))]
    public class FixedSpacingHorizontalLayout : LayoutGroup
    {
        [SerializeField] private float mSpacing = 5f;

        public override void CalculateLayoutInputHorizontal()
        {
            base.CalculateLayoutInputHorizontal();
            
            float totalWidth = 0;
            int visibleChildren = 0;
            
            for (int i = 0; i < rectChildren.Count; i++)
            {
                if (!rectChildren[i].gameObject.activeInHierarchy) continue;
                
                totalWidth += rectChildren[i].rect.width;
                visibleChildren++;
            }
            
            if (visibleChildren > 1)
            {
                totalWidth += mSpacing * (visibleChildren - 1);
            }
            
            float startPos = (rectTransform.rect.width - totalWidth) * 0.5f;
            float currentPos = startPos;
            
            for (int i = 0; i < rectChildren.Count; i++)
            {
                if (!rectChildren[i].gameObject.activeInHierarchy) continue;

                // 只设置位置，不设置宽度
                SetChildAlongAxis(rectChildren[i], 0, currentPos);
                currentPos += rectChildren[i].rect.width + mSpacing;
            }
        }

        public override void CalculateLayoutInputVertical()
        {
        }

        public override void SetLayoutHorizontal()
        {
            CalculateLayoutInputHorizontal();
        }

        public override void SetLayoutVertical()
        {
        }
    }
}