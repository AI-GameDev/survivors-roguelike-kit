#region

using RGame.Framework;
using UnityEngine.AddressableAssets;

#endregion

namespace RGame.RoguelikeKit
{
    /// <summary>
    ///     This class is a base class which contains what is common to all game scenes (Locations, Menus, Managers)
    /// </summary>
    public class GameSceneSO : DescriptionBaseSO
    {
        public AssetReference sceneReference; //Used at runtime to load the scene from the right AssetBundle
    }
}