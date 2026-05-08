using System;
using System.Collections;
using System.Collections.Generic;
using RGame.CommonStat;
using RGame.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace RGame.RoguelikeKit
{
    public class TreasurePanel : MonoBehaviour
    {
        [SerializeField] private AttributeSO attributeSO;
        [SerializeField] private CommonStatRuntimeSO statSO;
        [SerializeField] private GlobalConfigSO globalConfig;
        [SerializeField] private UpgradeSkillUIChannel openTreasurePanelChannel;
        [SerializeField] private StringEventChannelSO upgradeSkillChannel;  // Add this reference
        [SerializeField] private TextMeshProUGUI goldText;
        [SerializeField] private Button enterBtn;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private List<Image> imageList;
        [SerializeField] private Sprite goldSprite;
        
        private List<SkillDataSO> currentSkills;  // Store current skills for upgrade
        
        private void OnEnable()
        {
            openTreasurePanelChannel.RegisterListener(Show);
        }

        private void OnDisable()
        {
            openTreasurePanelChannel.UnregisterListener(Show);
        }

        public void Show(List<SkillDataSO> skillDataSos)
        {
            Time.timeScale = 0;
            
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true; 
            canvasGroup.blocksRaycasts = true;

            currentSkills = skillDataSos;

            for (int i = 0; i < skillDataSos.Capacity; i++)
            {

                imageList[i].transform.parent.gameObject.SetActive(true);

                // Check if it's a money bag or regular skill
                imageList[i].sprite = skillDataSos[i].SkillIcon; // Money bag icon
            }

            StartCoroutine(IconAddAnimation(skillDataSos.Capacity));
        }

        public void EnterSkill()
        {
            // Apply all skill upgrades when user confirms
            foreach (var skill in currentSkills)
            {
                if (skill.IsMoneyBag())
                {
                    upgradeSkillChannel.RaiseEvent("MoneyBag");
                }
                else
                {
                    upgradeSkillChannel.RaiseEvent(skill.Key);
                }
            }
            
            Time.timeScale = 1;
            
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false; 
            canvasGroup.blocksRaycasts = false;
            
            for (int i = 0; i < imageList.Count; i++)
            {
                imageList[i].transform.parent.gameObject.SetActive(false);
            }
            
            enterBtn.gameObject.SetActive(false);
        }

        private IEnumerator IconAddAnimation(int count)
        {
            float timer = 0;

            float randomGold = Random.Range(100 + (count-1) * 200, 200 * count);

            randomGold *= statSO.GetValue("Greed") * 0.01f;

            int getGold = (int)randomGold;
            
            globalConfig.CurrentGetGold += getGold;
            
            while (timer < 3)
            {
                timer += Time.unscaledDeltaTime;
                
                goldText.text = ((int)(getGold * timer/3)).ToString();
                
                yield return null;
            }
            
            enterBtn.gameObject.SetActive(true);
        }
    }
}