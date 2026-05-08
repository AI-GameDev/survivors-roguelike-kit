using System.Collections;
using DG.Tweening;
using UnityEngine;

namespace RGame.RoguelikeKit
{
    public class RandomSkillCast : SkillCast
    {
        public override void Cast(SkillDataSO skillData)
        {
            base.Cast(skillData);
            
            int amount = _stat.GetValue("Amount") + skillData.Amount;
            int damage = (int)(_stat.GetValue("Might") * skillData.Damage * 0.01f) + 1;
            
            var enemies = _enemySystem.GetNearestEnemies(_globalConfig.GlobalPlayer.transform.position, 12, 7);

            if (enemies.Count <= amount)
            {
                for (int i = 0; i < amount && i< enemies.Count; i++)
                {
                    SpawnSkill(damage,enemies[i]);
                }
            }
            else
            {
                for (int i = 0; i < amount; i++)
                {
                    int index = Random.Range(0, enemies.Count);
                    SpawnSkill(damage, enemies[index]);
                    enemies.Remove(enemies[index]);
                }
            }
           
        }

        private void SpawnSkill(int damage, BaseEnemy meleeEnemy)
        {
            var go = _pool.Request(Key);
                
            go.transform.position = meleeEnemy.transform.position;

            var spriteRenderer = go.GetComponentInChildren<SpriteRenderer>();
                
            spriteRenderer.DOFade(0, 0.5f).OnComplete(() =>
            {
                spriteRenderer.color = Color.white;
                    
                _pool.Return(go);
            });
                
            meleeEnemy.MyHit.Hit(damage);
        }

        public override void MixSkill()
        {
            base.MixSkill();
            _mySkillData.CD *= 0.3f;
        }
    }
}
