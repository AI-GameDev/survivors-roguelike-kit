#region

using RGame.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#endregion

namespace RGame.RoguelikeKit
{
    public class CharacterItem : MonoBehaviour, IPointerEnterHandler, IPointerDownHandler, IPointerExitHandler
    {
        [SerializeField] private LevelAndCharacterSO mLevelAndCharacterSO;

        [SerializeField] private TextMeshProUGUI mTitle;
        [SerializeField] private Image mCharacterImage;
        [SerializeField] private Image mSkillImage;
        [SerializeField] private TextMeshProUGUI mSellPrice;
        public GameObject SellGameObject;
        public Image BackgroundImage;
        
        public UnityAction<CharacterItem, bool> OnPointerEnterAction;
        public UnityAction<CharacterItem, bool> OnPointerDownAction;
        public UnityAction<CharacterItem, bool> OnPointerExitAction;
        public bool IsSelected;
        public bool IsUnlocked;
        public int SellPrice;
        public int CharacterID;
        public void Initialization(CharacterSelectConfigSO _configSo, bool isUnlocked)
        {
            mTitle.text = _configSo.CharacterName;
            mCharacterImage.sprite = _configSo.CharacterImage;
            mSkillImage.sprite = _configSo.BeginSkillImage;
            IsUnlocked = isUnlocked;

            if (!isUnlocked)
            {
                mSellPrice.text = _configSo.SellingPrice.ToString();
                SellGameObject.SetActive(true);
                SellPrice = _configSo.SellingPrice;
            }
            
            GetComponent<Button>().onClick.AddListener(() => { mLevelAndCharacterSO.SelectCharacterSo = _configSo; });
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            OnPointerEnterAction?.Invoke(this,IsUnlocked);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            OnPointerDownAction?.Invoke(this,IsUnlocked);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            OnPointerExitAction?.Invoke(this,IsUnlocked);
        }
    }
}