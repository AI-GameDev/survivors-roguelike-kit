using UnityEngine;

namespace RGame.RoguelikeKit
{
    public class RotateToMovementDirection : MonoBehaviour
    {
        [SerializeField][Range(0,360)]
        private float _spriteDefaultAngle = 0f;
    
        private Vector2 _lastPosition;
        
        private void FixedUpdate()
        {
            Vector2 currentPosition = transform.position;
            Vector2 delta = currentPosition - _lastPosition;
            
            if (delta.magnitude != 0)
            {
                float computedAngle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
                float finalRotation = computedAngle + _spriteDefaultAngle - 90;
                
                transform.rotation = Quaternion.Euler(0, 0, finalRotation);
            }
            
            _lastPosition = currentPosition;
        }
    }
}
