#region

using RGame.Framework;
using UnityEngine;
using UnityEngine.Serialization;

#endregion

namespace RGame.RoguelikeKit
{
    public class Player : MonoBehaviour
    {
        [SerializeField] private PlayerSpawnChannelSO mPlayerSpawn;
        [SerializeField] private StringEventChannelSO upgradeSkillChannel;
        [SerializeField] private string _beginSkillKey = "Skill1";
        
        public Vector2 MoveDirection { get; set; } = Vector2.right;
        
        private void Start()
        {
            mPlayerSpawn.RaiseEvent(this);
            upgradeSkillChannel.RaiseEvent(_beginSkillKey);
        }
    }
}