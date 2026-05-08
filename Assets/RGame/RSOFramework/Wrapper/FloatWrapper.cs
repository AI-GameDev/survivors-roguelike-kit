using UnityEngine;

namespace RGame.Framework
{
    [CreateAssetMenu(fileName = "FloatWrapper", menuName = "RGame/Framework/Wrappers/FloatWrapper")]
    public class FloatWrapper : WrapperT<float>
    {
        [SerializeField] private float minValue = float.MinValue;
        [SerializeField] private float maxValue = float.MaxValue;

        public float MinValue => minValue;
        public float MaxValue => maxValue;

        public override void Reset()
        {
            Value = 0f;
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            mValue = Mathf.Clamp(mValue, minValue, maxValue);
        }
#endif
    }

}
