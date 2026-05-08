#region

using System;
using System.Collections.Generic;
using RGame.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

#endregion

namespace RGame.RoguelikeKit
{
    public class CharacterSelectPanel : MonoBehaviour
    {
        [SerializeField] private GameObject mSelectList;
        [SerializeField] private CharacterItem mCharacterPrefab;
        [SerializeField] private List<CharacterSelectConfigSO> mCharacterSos;
        [SerializeField] private CharacterSaveSO mCharacterSave;
        [SerializeField] private AttributeSO mAttributeSo;
        [SerializeField] private VoidEventChannelSO _saveGameChannel;
        private List<CharacterItem> _characterItems = new List<CharacterItem>();
        public UnityAction Closed;
        public UnityAction Confirm;

        private void Start()
        {
            CreateCharacterItem();
        }

        private void CreateCharacterItem()
        {
            //SaveSystem Code To List is better code
            for (int i = 0; i < mCharacterSos.Count; i++)
            {
                bool isUnlocked = true;

                if (i == 1 || i == 2)
                {
                    isUnlocked = i == 1 ? mCharacterSave.CharacterTwo : mCharacterSave.CharacterThree;
                }
                var characterItem = Instantiate(mCharacterPrefab, mSelectList.transform);
                characterItem.Initialization(mCharacterSos[i], isUnlocked);
                characterItem.OnPointerEnterAction += PointerEnterAction;
                characterItem.OnPointerExitAction += PointerExitAction;
                characterItem.OnPointerDownAction += PointerDownAction;
                _characterItems.Add(characterItem);
                characterItem.CharacterID = i;
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

        private void PointerEnterAction(CharacterItem item, bool isUnlocked)
        {
            item.BackgroundImage.color = Color.yellow;
        }

        private void PointerExitAction(CharacterItem item, bool isUnlocked)
        {
            if (item.IsSelected) return;
            item.BackgroundImage.color = Color.red;
        }

        private void PointerDownAction(CharacterItem item, bool isUnlocked)
        {
            if (isUnlocked)
            {
                for (int i = 0; i < _characterItems.Count; i++)
                {
                    _characterItems[i].BackgroundImage.color = Color.red;
                    _characterItems[i].IsSelected = false;
                }
                item.BackgroundImage.color = Color.yellow;
                item.IsSelected = true;
            }
            else
            {
                if (item.SellPrice <= mAttributeSo.GoldCount)
                {
                    mAttributeSo.SetAttribute("GoldCount",mAttributeSo. GoldCount - item.SellPrice);
                    item.IsUnlocked = true;
                    item.SellGameObject.SetActive(false);

                    if (item.CharacterID == 1)
                    {
                        mCharacterSave.SaveCharacter(true, mCharacterSave.CharacterThree);
                    }
                    
                    if (item.CharacterID == 2)
                    {
                        mCharacterSave.SaveCharacter(mCharacterSave.CharacterTwo, true);
                    }

                    _saveGameChannel.RaiseEvent();
                }
            }
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _characterItems.Count; i++)
            {
                _characterItems[i].OnPointerEnterAction -= PointerEnterAction;
                _characterItems[i].OnPointerExitAction -= PointerExitAction;
                _characterItems[i].OnPointerDownAction -= PointerDownAction;
            }
        }
    }
}