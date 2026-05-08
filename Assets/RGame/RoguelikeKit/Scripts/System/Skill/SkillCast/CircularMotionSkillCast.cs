using UnityEngine;

namespace RGame.RoguelikeKit
{
    public class CircularMotionSkillCast : SkillCast
    {
        [SerializeField] private float _radius;
        
        public override void Cast(SkillDataSO skillData)
        {
            base.Cast(skillData);
            
            int amount = _stat.GetValue("Amount") + skillData.Amount;
            int damage = (int)(_stat.GetValue("Might") * skillData.Damage * 0.01f) + 1;
            float velocity = skillData.Velocity * _stat.GetValue("SkillSpeed") * 0.01f;
            float duration = skillData.Duration * _stat.GetValue("Duration") * 0.01f;
            float radius = _radius * _stat.GetValue("Area") * 0.01f;
            
            for (int i = 0; i < amount; i++)
            {
                var go = _pool.Request(Key);
                var skill = go.GetComponent<CircularMotionSkill>();

                if (skillData.CurrentState is SkillCurrentState.Mixed)
                {
                    skill.GetComponent<SpriteRenderer>().color = new Color(1,1,0,1);
                    skill.GetComponent<TrailRenderer>().startColor = new Color(1, 1, 0, 1);
                    skill.GetComponent<TrailRenderer>().endColor = new Color(1, 1, 0, 1);
                }
                
                skill.transform.localScale = skill.transform.localScale * (_stat.GetValue("Area") + skillData.Area) * 0.01f;                
                skill.OnDeath += HandleSkillDeath;
                skill.Init(radius,velocity,damage,(360f / amount) * i, duration, _globalConfig.GlobalPlayer.transform);
            }
        }

        public override void MixSkill()
        {
            base.MixSkill();
            _mySkillData.Velocity += 1;
            _mySkillData.Amount += 3;
        }
    }
}
