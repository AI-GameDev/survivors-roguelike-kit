using UnityEngine;

namespace RGame.RoguelikeKit
{
    public class ChainSkillCast : SkillCast
    {
        public override void Cast(SkillDataSO skillData)
        {
            base.Cast(skillData);

            int damage = (int)(_stat.GetValue("Might") * skillData.Damage * 0.01f) + 1;
            float velocity = skillData.Velocity * _stat.GetValue("SkillSpeed") * 0.01f;
            int amount = _stat.GetValue("Amount") + skillData.Amount;

            for (int i = 0; i < amount; i++)
            {
                var go = _pool.Request(Key);
                var skill = go.GetComponent<ChainSkill>();
                skill.transform.position = _globalConfig.GlobalPlayer.transform.position;
            
                skill.OnDeath += HandleSkillDeath;
                skill.Init(velocity, damage, _enemySystem);
            }
        }

        public override void MixSkill()
        {
            base.MixSkill();
            _mySkillData.CD *= 0.3f;
        }
    }
}
