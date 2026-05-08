#region

using UnityEngine;

#endregion

namespace RGame.RoguelikeKit
{
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private PlayerSpawnChannelSO mPlayerSpawn;
        [SerializeField] private LevelAndCharacterSO mConfig;
        public float SmoothSpeed = 1f;

        private MapConfigSO _mMapConfigSo;
        private float mHalfHeight;
        private float mHalfWidth;
        private Transform mPlayer;

        private void Start()
        {
            _mMapConfigSo = mConfig.SelectLevelSO.MapConfig;
            var mainCamera = Camera.main;
            mHalfHeight = mainCamera.orthographicSize;
            mHalfWidth = mainCamera.aspect * mHalfHeight;
        }

        private void FixedUpdate()
        {
            if (mPlayer == null) return;

            var targetPosition = new Vector3(mPlayer.position.x, mPlayer.position.y, transform.position.z);

            var smoothedPosition = Vector3.Lerp(transform.position, targetPosition, SmoothSpeed * Time.fixedDeltaTime);

            var clampedX = Mathf.Clamp(smoothedPosition.x, -_mMapConfigSo.Width / 2 + mHalfWidth + 5, _mMapConfigSo.Width / 2 - mHalfWidth - 5);
            var clampedY = Mathf.Clamp(smoothedPosition.y, -_mMapConfigSo.Height / 2 + mHalfHeight + 5, _mMapConfigSo.Height / 2 - mHalfHeight - 5);

            transform.position = new Vector3(clampedX, clampedY, smoothedPosition.z);
        }

        private void OnEnable()
        {
            mPlayerSpawn.RegisterListener(OnPlayerSpawn);
        }

        private void OnDisable()
        {
            mPlayerSpawn.UnregisterListener(OnPlayerSpawn);
        }

        private void OnPlayerSpawn(Player _player)
        {
            mPlayer = _player.transform;
        }
    }
}