using RGame.RoguelikeKit;
using UnityEngine;

namespace RGame.MLAgents
{
    /// <summary>
    /// ML 모드 전용 카메라 추적기. 기존 CameraFollow의 맵 경계 clamp를 우회해
    /// 플레이어가 맵 밖으로 나가도 끝까지 따라간다. MLAgentBootstrap이 OnPlayerSpawn에서
    /// 런타임에 부착하므로 본 게임 흐름에는 영향을 주지 않는다.
    /// </summary>
    public class MLCameraFollow : MonoBehaviour
    {
        public Transform Target;
        public float SmoothSpeed = 1f;

        private void FixedUpdate()
        {
            if (Target == null) return;
            var targetPosition = new Vector3(Target.position.x, Target.position.y, transform.position.z);
            transform.position = Vector3.Lerp(transform.position, targetPosition, SmoothSpeed * Time.fixedDeltaTime);
        }
    }
}
