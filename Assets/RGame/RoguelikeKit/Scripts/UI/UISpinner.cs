#region

using UnityEngine;

#endregion

namespace RGame.RoguelikeKit
{
    public class UISpinner : MonoBehaviour
    {
        [SerializeField] private float mRotateSpeed = -150f;
        private RectTransform mRectComponent;

        private void Start()
        {
            mRectComponent = GetComponent<RectTransform>();
        }

        private void Update()
        {
            mRectComponent.Rotate(0f, 0f, mRotateSpeed * Time.deltaTime);
        }
    }
}