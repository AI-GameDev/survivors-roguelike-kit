namespace RGame.RoguelikeKit
{
    public class Coin : IDropOut
    {
        public override void Do()
        {
            //AudioKit.PlaySound("Coin");

            Destroy(gameObject);
        }
    }
}