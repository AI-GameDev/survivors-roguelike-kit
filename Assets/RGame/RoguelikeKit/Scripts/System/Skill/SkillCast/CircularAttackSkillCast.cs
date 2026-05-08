using UnityEngine;

namespace RGame.RoguelikeKit
{
    public class CircularAttackSkillCast : SkillCast
    {
        [SerializeField] private float _midAngle;
        private float _angle = -45;
        
        public override void Cast(SkillDataSO skillData)
        {
            base.Cast(skillData);
            
            int amount = _stat.GetValue("Amount") + skillData.Amount;
            int damage = (int)(_stat.GetValue("Might") * skillData.Damage * 0.01f) + 1;
            float velocity = skillData.Velocity * _stat.GetValue("SkillSpeed") * 0.01f;
            float duration = skillData.Duration * _stat.GetValue("Duration") * 0.01f;

            if (skillData.CurrentState is SkillCurrentState.Mixed)
            {
                for (int i = 0; i < 16; i++)
                {
                    var go = _pool.Request(Key);
                    var skill = go.GetComponent<CircularAttackSkill>();
                
                    skill.transform.position = _globalConfig.GlobalPlayer.transform.position;
                    skill.transform.localScale = skill.transform.localScale * (_stat.GetValue("Area") + skillData.Area) * 0.01f * 2f;
                    _angle += 22.5f;
                    var rad = _angle * Mathf.Deg2Rad;
                    Vector2 dir = new Vector2(Mathf.Cos(rad), -Mathf.Sin(rad));
                
                    skill.OnDeath += HandleSkillDeath;
                    skill.Init(velocity,damage, duration, dir.normalized);
                }
            }
            else
            {
                for (int i = 0; i < amount; i++)
                {
                    var go = _pool.Request(Key);
                    var skill = go.GetComponent<CircularAttackSkill>();
                
                    skill.transform.position = _globalConfig.GlobalPlayer.transform.position;
                    skill.transform.localScale = skill.transform.localScale * (_stat.GetValue("Area") + skillData.Area) * 0.01f;
                    _angle += 45;
                    var rad = _angle * Mathf.Deg2Rad;
                    Vector2 dir = new Vector2(Mathf.Cos(rad), -Mathf.Sin(rad));
                
                    skill.OnDeath += HandleSkillDeath;
                    skill.Init(velocity,damage, duration, dir.normalized);
                }
            }
        }
    }
}
