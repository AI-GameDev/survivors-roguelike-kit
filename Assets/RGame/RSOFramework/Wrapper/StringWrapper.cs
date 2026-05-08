using UnityEngine;

namespace RGame.Framework
{
    [CreateAssetMenu(fileName = "StringWrapper", menuName = "RGame/Framework/Wrappers/StringWrapper")]
    public class StringWrapper : WrapperT<string>
    {
        public override void Reset()
        {
            Value = string.Empty;
        }
    }
}
