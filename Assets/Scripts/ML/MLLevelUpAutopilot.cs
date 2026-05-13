#region

using System.Collections;
using System.Collections.Generic;
using RGame.RoguelikeKit;
using UnityEngine;
using UnityEngine.UI;

#endregion

namespace RGame.MLAgents
{
    /// <summary>
    ///     ML 학습 모드에서 ts=0으로 게임을 멈추는 UI 패널들(UpgradeSkillPanel, TreasurePanel)을
    ///     자동으로 닫고 timeScale을 학습 속도(20)로 복구한다. 기존 패널은 수정하지 않는다.
    /// </summary>
    public class MLLevelUpAutopilot : MonoBehaviour
    {
        [SerializeField] private UpgradeSkillUIChannel _openUpgradeUIChannel;
        [SerializeField] private UpgradeSkillUIChannel _openTreasurePanelChannel;
        [SerializeField] private float _postClickTimeScale = 20f;

        private void OnEnable()
        {
            if (_openUpgradeUIChannel != null)
                _openUpgradeUIChannel.RegisterListener(OnUpgradeUI);
            if (_openTreasurePanelChannel != null)
                _openTreasurePanelChannel.RegisterListener(OnTreasureUI);
        }

        private void OnDisable()
        {
            if (_openUpgradeUIChannel != null)
                _openUpgradeUIChannel.UnregisterListener(OnUpgradeUI);
            if (_openTreasurePanelChannel != null)
                _openTreasurePanelChannel.UnregisterListener(OnTreasureUI);
        }

        private void OnUpgradeUI(List<SkillDataSO> options)
        {
            StartCoroutine(AutoPickUpgrade());
        }

        private void OnTreasureUI(List<SkillDataSO> options)
        {
            StartCoroutine(AutoPickTreasure());
        }

        private IEnumerator AutoPickUpgrade()
        {
            // UpgradeSkillPanel.Show가 같은 프레임에 끝나도록 한 프레임 양보.
            yield return null;

            var panel = Object.FindFirstObjectByType<UpgradeSkillPanel>(FindObjectsInactive.Include);
            if (panel == null)
            {
                Debug.LogWarning("[Autopilot] UpgradeSkillPanel not found");
                Time.timeScale = _postClickTimeScale;
                yield break;
            }

            var buttons = panel.GetComponentsInChildren<Button>(false);
            if (buttons.Length == 0)
            {
                Debug.LogWarning("[Autopilot] No active Button under UpgradeSkillPanel");
                Time.timeScale = _postClickTimeScale;
                yield break;
            }

            Debug.Log("[Autopilot] Upgrade: clicking " + buttons[0].gameObject.name + " (of " + buttons.Length + ")");
            buttons[0].onClick.Invoke();

            Time.timeScale = _postClickTimeScale;
        }

        private IEnumerator AutoPickTreasure()
        {
            // TreasurePanel.Show가 같은 프레임에 끝나도록 한 프레임 양보.
            yield return null;

            var panel = Object.FindFirstObjectByType<TreasurePanel>(FindObjectsInactive.Include);
            if (panel == null)
            {
                Debug.LogWarning("[Autopilot] TreasurePanel not found");
                Time.timeScale = _postClickTimeScale;
                yield break;
            }

            // EnterSkill이 public이라 3초 애니메이션을 건너뛰고 즉시 호출 가능.
            Debug.Log("[Autopilot] Treasure: invoking EnterSkill directly");
            panel.EnterSkill();

            Time.timeScale = _postClickTimeScale;
        }
    }
}
