using UnityEngine;

namespace RGame.RoguelikeKit
{
    public class BouncingSkillCast : SkillCast
    {
        public override void Cast(SkillDataSO skillData)
        {
            base.Cast(skillData);
            
            int amount = _stat.GetValue("Amount") + skillData.Amount;
            int damage = (int)(_stat.GetValue("Might") * skillData.Damage * 0.01f) + 1;
            float velocity = skillData.Velocity * _stat.GetValue("SkillSpeed") * 0.01f;
            float duration = skillData.Duration * _stat.GetValue("Duration") * 0.01f;
            
            for (int i = 0; i < amount; i++)
            {
                var go = _pool.Request(Key);
                var skill = go.GetComponent<BouncingSkill>();

                if (skillData.CurrentState is SkillCurrentState.Mixed)
                {
                    skill.GetComponent<SpriteRenderer>().color = new Color(1, 0.5f, 0.5f, 1);
                }
                
                skill.transform.localScale = skill.transform.localScale * (_stat.GetValue("Area") + skillData.Area) * 0.01f;                skill.transform.position = _globalConfig.GlobalPlayer.transform.position;
                skill.OnDeath += HandleSkillDeath;
                skill.Init(velocity,damage,duration);
            }
        }

        public override void MixSkill()
        {
            base.MixSkill();
            _mySkillData.Velocity = _mySkillData.Velocity * 1.5f;
            _mySkillData.Amount += 3;
        }
    }
}
