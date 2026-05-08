#region

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

#endregion

namespace RGame.RoguelikeKit
{
    public class LevelSelectPanel : MonoBehaviour
    {
        [SerializeField] private GameObject mSelectList;
        [SerializeField] private LevelItem mLevelPrefab;
        [SerializeField] private List<LevelConfigSO> mLevelSos;
        private List<LevelItem> _levelItems = new List<LevelItem>();
        public UnityAction Closed;
        public UnityAction Confirm;

        private void Start()
        {
            CreateLevelItem();
        }

        private void CreateLevelItem()
        {
            foreach (var item in mLevelSos)
            {
                var levelItem = Instantiate(mLevelPrefab, mSelectList.transform);

                levelItem.Initialization(item);
                
                levelItem.OnPointerEnterAction += PointerEnterAction;
                levelItem.OnPointerExitAction += PointerExitAction;
                levelItem.OnPointerDownAction += PointerDownAction;
                _levelItems.Add(levelItem);
            }
        }

        public void ClosedButton()
        {
            Closed.Invoke();
        }

        public void ConfirmButton()
        {
            Confirm.Invoke();
        }
        
        private void PointerEnterAction(LevelItem item)
        {
            item.BackgroundImage.color = Color.yellow;
        }

        private void PointerExitAction(LevelItem item)
        {
            if (item.IsSelected) return;
            item.BackgroundImage.color = Color.red;
        }

        private void PointerDownAction(LevelItem item)
        {
            for (int i = 0; i < _levelItems.Count; i++)
            {
                _levelItems[i].BackgroundImage.color = Color.red;
                _levelItems[i].IsSelected = false;
            }
            item.BackgroundImage.color = Color.yellow;
            item.IsSelected = true;
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _levelItems.Count; i++)
            {
                _levelItems[i].OnPointerEnterAction -= PointerEnterAction;
                _levelItems[i].OnPointerExitAction -= PointerExitAction;
                _levelItems[i].OnPointerDownAction -= PointerDownAction;
            }
        }
    }
}