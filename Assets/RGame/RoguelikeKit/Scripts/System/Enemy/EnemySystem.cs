using System;
using System.Collections.Generic;
using RGame.Framework;
using UnityEngine;

namespace RGame.RoguelikeKit
{
    public class EnemySystem : DescriptionBaseSO
    {
        private List<BaseEnemy> _activeEnemies = new List<BaseEnemy>();

        private void OnEnable()
        {
            _activeEnemies.Clear();
        }

        public void AddEnemy(BaseEnemy meleeEnemy)
        {
            _activeEnemies.Add(meleeEnemy);
        }

        public void RemoveEnemy(BaseEnemy meleeEnemy)
        {
            _activeEnemies.Remove(meleeEnemy);
        }

        public List<BaseEnemy> GetEnemies()
        {
            List<BaseEnemy> enemies = new List<BaseEnemy>(_activeEnemies);

            return enemies;
        }

        public void ClearEnemies()
        {
            _activeEnemies.Clear();
        }

        public List<BaseEnemy> GetNearestEnemies(Vector3 position, float x, float y)
        {
            List<BaseEnemy> nearestEnemies = new List<BaseEnemy>();

            if (_activeEnemies.Count == 0)
            {
                return nearestEnemies;
            }

            List<(BaseEnemy enemy, float distance)> enemyDistances = new List<(BaseEnemy, float)>();

            foreach (var enemy in _activeEnemies)
            {
                if (enemy == null) continue;

                float distance = Vector3.Distance(position, enemy.transform.position);
                if (Mathf.Abs(position.x - enemy.transform.position.x) < x &&
                    Mathf.Abs(position.y - enemy.transform.position.y) < y)
                {
                    enemyDistances.Add((enemy, distance));
                }
            }
            
            enemyDistances.Sort((a, b) => a.distance.CompareTo(b.distance));
            
            for (int i = 0; i < Mathf.Min(10, enemyDistances.Count); i++)
            {
                nearestEnemies.Add(enemyDistances[i].enemy);
            }

            return nearestEnemies;
        }
    }
}
