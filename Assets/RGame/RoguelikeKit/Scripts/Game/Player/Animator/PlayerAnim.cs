#region

using UnityEngine;

#endregion

namespace RGame.RoguelikeKit
{
    public class PlayerAnim : MonoBehaviour
    {
        [HideInInspector] public Animator MyAnimator;
        [HideInInspector] public Rigidbody2D MyRigidbody;

        private void Awake()
        {
            MyRigidbody = transform.GetComponent<Rigidbody2D>();
            MyAnimator = transform.GetComponent<Animator>();
        }

        private void Update()
        {
            if (MyRigidbody.linearVelocity.magnitude != 0)
                ChangeAnimation("Run");
            else
                ChangeAnimation("Idle");
        }

        public void ChangeAnimation(string _animationName)
        {
            MyAnimator.CrossFade(_animationName, 0);
        }
    }
}