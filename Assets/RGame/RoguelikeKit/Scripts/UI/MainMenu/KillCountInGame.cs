using TMPro;
using UnityEngine;

namespace RGame.RoguelikeKit
{
    public class KillCountInGame : MonoBehaviour
    {
        [SerializeField]
        private GlobalConfigSO _globalConfig;
        [SerializeField]
        private TextMeshProUGUI _killCountText;

        private void FixedUpdate()
        {
            _killCountText.text = _globalConfig.CurrentGetKill.ToString();
        }
    }
}
