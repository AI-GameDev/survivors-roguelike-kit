using System;
using UnityEngine;

namespace RGame.RoguelikeKit
{
    public class SaveSystem : MonoBehaviour
    {
        [SerializeField] private SaveSystemSO mSaveSystemSO;

        private void Awake()
        {
            mSaveSystemSO.LoadSaveDataFromDisk();
        }
    }
}
