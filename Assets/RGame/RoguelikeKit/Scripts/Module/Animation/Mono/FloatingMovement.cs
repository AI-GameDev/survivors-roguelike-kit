using UnityEngine;

namespace RGame.RoguelikeKit
{
    public class FloatingMovement : MonoBehaviour
    {
        [Header("Floating Settings")] 
        public float floatSpeed = 2f;

        public float floatAmplitude = 0.5f;

        private Vector3 startPosition;

        private void Start()
        {
            startPosition = transform.position;
        }

        private void Update()
        {
            float newY = startPosition.y + Mathf.Sin(Time.time * floatSpeed) * floatAmplitude;

            transform.position = new Vector3(startPosition.x, newY, startPosition.z);
        }
    }
}
