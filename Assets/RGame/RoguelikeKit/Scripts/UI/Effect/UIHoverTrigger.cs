using UnityEngine;
using UnityEngine.EventSystems;

namespace RGame.RoguelikeKit
{
     public class UIHoverTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            [SerializeField] private GameObject mTargetObject;
            [SerializeField] private bool mActiveOnStart = false;
            
            private void Start()
            {
                if (mTargetObject != null)
                {
                    mTargetObject.SetActive(mActiveOnStart);
                }
            }
    
            public void OnPointerEnter(PointerEventData eventData)
            {
                if (mTargetObject != null)
                {
                    mTargetObject.SetActive(true);
                }
            }
    
            public void OnPointerExit(PointerEventData eventData)
            {
                if (mTargetObject != null)
                {
                    mTargetObject.SetActive(false);
                }
            }
    
            public void SetTargetObject(GameObject target)
            {
                mTargetObject = target;
            }
        }
}
