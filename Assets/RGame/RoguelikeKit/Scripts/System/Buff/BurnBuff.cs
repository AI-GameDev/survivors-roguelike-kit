using UnityEngine;

namespace RGame.RoguelikeKit
{
    [CreateAssetMenu(menuName = "RGame/RoguelikeKit/Buff/BurnBuff")]
    public class BurnBuff : BaseBuff
    {
        public int Damage;

        private EnemyHit _ownerHit;
        
        public override void Activate()
        {
            BuffName = "BurnBuff";
            
            var mat =  Owner.MySpriteRenderer.material;
            
            mat.SetColor("_FlameRedBlend", Color.red);
            
            Owner.OnDeath += OnOwnerDestroy;

            _ownerHit = Owner.GetComponentInChildren<EnemyHit>();
        }

        public override void DeActivate()
        {
            var mat =  Owner.MySpriteRenderer.material;
            
            mat.SetColor("_FlameRedBlend", Color.white);
            
            Owner.OnDeath -= OnOwnerDestroy;
        }

        public override void TickEffect()
        {
            base.TickEffect();
            
            _ownerHit.Hit(Damage);
        }
        
        private void OnOwnerDestroy(BaseEnemy owner)
        {
            RemoveAllBuff.RaiseEvent(this);
        }
    }
}
