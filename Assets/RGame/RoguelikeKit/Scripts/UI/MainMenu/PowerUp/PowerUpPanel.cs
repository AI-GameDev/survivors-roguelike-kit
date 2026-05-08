using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace RGame.RoguelikeKit
{
    public class PowerUpPanel : MonoBehaviour
    {
        private PowerUpDescription mDescription;
        private PowerUpAttributeUpgrade[] mAttributes;

        public UnityAction Closed;
        
        private IEnumerator Start()
        {
            yield return new WaitForSeconds(0.2f);
            
            mAttributes = GetComponentsInChildren<PowerUpAttributeUpgrade>();
            mDescription = GetComponentInChildren<PowerUpDescription>();
            
            for (int i = 0; i < mAttributes.Length; i++)
            {
                mAttributes[i].SelectAction += mDescription.SelectPowerUp;
            }

            mAttributes[0].UpdateInfo();
        }

        private void OnDestroy()
        {
            for (int i = 0; i < mAttributes.Length; i++)
            {
                mAttributes[i].SelectAction -= mDescription.SelectPowerUp;
            }
        }

        public void CloseButtonOnclick()
        {
            Closed?.Invoke();
        }
    }
}
