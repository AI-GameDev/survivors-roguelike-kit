using UnityEngine;

namespace RGame.RoguelikeKit
{
    public class RotateAroundSelf : MonoBehaviour
    {

        [SerializeField] private float rotationSpeed = 360f;

        private void FixedUpdate()
        {
            transform.Rotate(0, 0, rotationSpeed * Time.fixedDeltaTime);
        }

    }
}
