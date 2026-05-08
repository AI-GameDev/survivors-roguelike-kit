using RGame.Framework;
using UnityEngine;

namespace RGame.RoguelikeKit
{
    [CreateAssetMenu(menuName = "RGame/RoguelikeKit/Buff/FreezingBuff")]
    public class FreezingBuff : BaseBuff
    {
        public PoolRuntimeSO PoolRuntimeSo;
        
        private Material _material;
        private int _originalSpeed;
        private Vector4 _originalShadowOffset;
        private float _originalTimeSpeed;
        private GameObject _slowBuff;
        
        public override void Activate()
        {
            BuffName = "FreezingBuff";
            _originalSpeed = Owner.StatRuntime.GetValue("Speed");
            
            Owner.StatRuntime.ModifyValue("Speed", -1 * _originalSpeed);
            
            SpriteRenderer renderer = Owner.MySpriteRenderer;
            
            if (renderer != null)
            {
                _material = renderer.material;
                
                _originalShadowOffset = _material.GetVector("_ShadowOffset");
                _originalTimeSpeed = _material.GetFloat("_TimeSpeed");
                
                _material.SetVector("_ShadowOffset", new Vector4(999f, _originalShadowOffset.y, 0f, 0f));
                _material.SetFloat("_TimeSpeed", 0);
            }
            
            _slowBuff = PoolRuntimeSo.Request("SlowBuff");
            _slowBuff.transform.position = Owner.transform.position + Vector3.down * 0.5f;
            _slowBuff.transform.SetParent(Owner.transform);

            Owner.OnDeath += OnOwnerDestroy;
        }

        public override void DeActivate()
        {
            Owner.StatRuntime.ModifyValue("Speed", _originalSpeed);
            
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
