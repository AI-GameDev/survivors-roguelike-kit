using UnityEngine;

namespace RGame.RoguelikeKit
{
    public class InsideScopeSkillCast : SkillCast
    {
        public override void Cast(SkillDataSO skillData)
        {
            base.Cast(skillData);

            int damage = (int)(_stat.GetValue("Might") * skillData.Damage * 0.01f) + 1;

            var go = _pool.Request(Key);
            var skill = go.GetComponent<InsideScopeSkill>();

            if (skillData.CurrentState is SkillCurrentState.Mixed)
            {
                skill.AttackTimer = 0.5f;
            }
            
            skill.transform.position = _globalConfig.GlobalPlayer.transform.position;
            skill.transform.localScale = skill.transform.localScale * (_stat.GetValue("Area") + skillData.Area) * 0.01f;
            skill.OnDeath += HandleSkillDeath;
            skill.Init(damage,_globalConfig.GlobalPlayer.transform);
        }

        public override void MixSkill()
        {
            base.MixSkill();
            _mySkillData.Area *= 2;
        }
    }
}
