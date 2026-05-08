#region

using UnityEngine;

#endregion

namespace RGame.RoguelikeKit
{
    public class ModuleRotateToMoveDirection : Module
    {
        public float InitialAngleOffset;
        public Vector2 MoveDirection;
        public Transform ObjectTransform;

        public void Do()
        {
            if (MoveDirection != Vector2.zero)
            {
                var targetAngle = Mathf.Atan2(MoveDirection.y, MoveDirection.x) * Mathf.Rad2Deg;
                targetAngle -= 90f;
                targetAngle += InitialAngleOffset;

                ObjectTransform.rotation = Quaternion.Euler(0, 0, targetAngle);
            }
        }
    }
}