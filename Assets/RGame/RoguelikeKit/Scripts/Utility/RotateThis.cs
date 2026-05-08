using UnityEngine;

namespace RGame.RoguelikeKit
{
    public class RotateThis : MonoBehaviour
    {
        [SerializeField] private RotationAnimationSO rotationAnimation;

        private void OnEnable()
        {
            rotationAnimation.Play(transform);
        }

        private void OnDisable()
        {
            rotationAnimation.Remove(transform);
        }
    }
}
