using RGame.Framework;
using UnityEngine;

namespace RGame.RoguelikeKit
{
    [CreateAssetMenu(menuName = "RGame/RoguelikeKit/Buff/SlowBuff")]
    public class SlowBuff : BaseBuff
    {
        public int SlowPercentage;
        public PoolRuntimeSO PoolRuntimeSo;
        
        private int _deltaValue;
        private Material _material;
        private Vector4 _originalShadowOffset;
        private float _originalTimeSpeed;
        private GameObject _slowBuff;
        
        public override void Activate()
        {
            BuffName = "SlowBuff";
            int value = Owner.StatRuntime.GetValue("Speed");
            _deltaValue = (int)(value * SlowPercentage * 0.01f);
            Owner.StatRuntime.ModifyValue("Speed", -1 * _deltaValue);
            
            SpriteRenderer renderer = Owner.MySpriteRenderer;
            
            if (renderer != null)
            {
                _material = renderer.material;
                
                _originalShadowOffset = _material.GetVector("_ShadowOffset");
                _originalTimeSpeed = _material.GetFloat("_TimeSpeed");
                
                _material.SetVector("_ShadowOffset", new Vector4(999f, _originalShadowOffset.y, 0f, 0f));
                _material.SetFloat("_TimeSpeed", _originalTimeSpeed * SlowPercentage * 0.01f);
            }
            
            _slowBuff = PoolRuntimeSo.Request("SlowBuff");
            _slowBuff.transform.position = Owner.transform.position + Vector3.down * 0.5f;
            _slowBuff.transform.SetParent(Owner.transform);

            Owner.OnDeath += OnOwnerDestroy;
        }

        public override void DeActivate()
        {
            Owner.StatRuntime.ModifyValue("Speed", _deltaValue);
            
            _material = Owner.MySpriteRenderer.material;
            _material.SetVector("_ShadowOffset", _originalShadowOffset);
            _material.SetFloat("_TimeSpeed", _originalTimeSpeed);
            
            PoolRuntimeSo.Return(_slowBuff);
            Owner.OnDeath -= OnOwnerDestroy;
        }

        private void OnOwnerDestroy(BaseEnemy owner)
        {
            RemoveAllBuff.RaiseEvent(this);
        }
    }
}