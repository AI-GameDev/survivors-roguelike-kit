using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace RGame.RoguelikeKit
{
    public class ChildObjectController : MonoBehaviour
    {
        [HideInInspector] public Image BG;
        
        public UnityAction ShowAction;
        public UnityAction HideAction;

        private void Awake()
        {
            BG = GetComponent<Image>();
        }

        public void ShowChildren()
        {
            foreach (Transform child in transform)
            {
                child.gameObject.SetActive(true);
            }
            
            ShowAction?.Invoke();
        }

        public void HideChildren()
        {
            foreach (Transform child in transform)
            {
                child.gameObject.SetActive(false);
            }
            
            HideAction?.Invoke();
        }
    }
}
