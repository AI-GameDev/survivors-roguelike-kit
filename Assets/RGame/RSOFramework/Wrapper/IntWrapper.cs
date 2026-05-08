using UnityEngine;

namespace RGame.Framework
{
    [CreateAssetMenu(fileName = "IntWrapper", menuName = "RGame/Framework/Wrappers/IntWrapper")]
    public class IntWrapper : WrapperT<int>
    {
        [SerializeField] private int minValue = int.MinValue;
        [SerializeField] private int maxValue = int.MaxValue;

        public int MinValue => minValue;
        public int MaxValue => maxValue;

        public override void Reset()
        {
            Value = 0;
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
