#region

using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#endregion

namespace RGame.RoguelikeKit
{
    public class LevelItem : MonoBehaviour, IPointerEnterHandler, IPointerDownHandler, IPointerExitHandler
    {
        [SerializeField] private LevelAndCharacterSO mLevelAndCharacterSO;

        [SerializeField] private TextMeshProUGUI mDescription;
        [SerializeField] private Image mPreviewImage;
        public Image BackgroundImage;
        public UnityAction<LevelItem> OnPointerEnterAction;
        public UnityAction<LevelItem> OnPointerDownAction;
        public UnityAction<LevelItem> OnPointerExitAction;
        public bool IsSelected;
        public void Initialization(LevelConfigSO _configSo)
        {
            mDescription.text = _configSo.LevelDescription;
            mPreviewImage.sprite = _configSo.PreviewImage;

            GetComponent<Button>().onClick.AddListener(() => { mLevelAndCharacterSO.SelectLevelSO = _configSo; });
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            OnPointerEnterAction?.Invoke(this);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            OnPointerDownAction?.Invoke(this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            OnPointerExitAction?.Invoke(this);
        }
    }
}