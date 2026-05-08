using UnityEngine;

namespace RGame.Framework
{
    [CreateAssetMenu(fileName = "BoolWrapper", menuName = "RGame/Framework/Wrappers/BoolWrapper")]
    public class BoolWrapper : WrapperT<bool>
    {
        public override void Reset()
        {
            Value = false;
        }
    }
}
