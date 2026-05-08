using RGame.CommonStat;
using RGame.Framework;
using UnityEngine;

namespace RGame.RoguelikeKit
{
    public class ToNearestSkillCast : SkillCast
    {
        public override void Cast(SkillDataSO skillData)
        {
            base.Cast(skillData);
            
            int amount = _stat.GetValue("Amount") + skillData.Amount;
            int damage = (int)(_stat.GetValue("Might") * skillData.Damage * 0.01f) + 1;
            float velocity = skillData.Velocity * _stat.GetValue("SkillSpeed") * 0.01f;
            
            var enemies = _enemySystem.GetNearestEnemies(_globalConfig.GlobalPlayer.transform.position, 12, 7);
            
            for (int i = 0; i < amount && i< enemies.Count; i++)
            {
                var go = _pool.Request(Key);
                var skill = go.GetComponent<ToNearestSkill>();

                if (skillData.CurrentState is SkillCurrentState.Mixed)
                {
                    skill.GetComponent<SpriteRenderer>().color = Color.red;
                }
                
                skill.transform.localScale = skill.transform.localScale * (_stat.GetValue("Area") + skillData.Area) * 0.01f;                skill.transform.position = _globalConfig.GlobalPlayer.transform.position;
                skill.OnDeath += HandleSkillDeath;
                skill.Init(velocity,damage,enemies[i].transform.position);
            }
        }

        public override void MixSkill()
        {
            base.MixSkill();
            _mySkillData.CD = 0.15f;
            _mySkillData.Damage = 5;
        }
    }
}
