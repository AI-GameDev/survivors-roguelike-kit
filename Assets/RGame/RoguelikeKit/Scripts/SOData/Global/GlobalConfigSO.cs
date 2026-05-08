#region

using RGame.Framework;

#endregion

namespace RGame.RoguelikeKit
{
    public class GlobalConfigSO : DescriptionBaseSO
    {
        public float MoveSpeedBalanceFactor = 0.05f;
        public Player GlobalPlayer { get; set; }
        public int CurrentGetGold;
        public int CurrentGetKill;

        public void OnGameStart()
        {
            CurrentGetGold = 0;
            CurrentGetKill = 0;
        }
    }
}