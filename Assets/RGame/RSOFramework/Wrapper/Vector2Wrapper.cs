using UnityEngine;

namespace RGame.Framework
{
    /// <summary>
    /// A ScriptableObject wrapper for a Vector2 value.
    /// </summary>
    [CreateAssetMenu(fileName = "NewVector2Wrapper", menuName = "RGame/Framework/Wrappers/Vector2 Wrapper")]
    public class Vector2WrapperSO : WrapperT<Vector2>
    {
        public override WrapperT<Vector2> Clone()
        {
            var clone = CreateInstance<Vector2WrapperSO>();
            clone.CopyFrom(this);
            return clone;
        }
    }
}