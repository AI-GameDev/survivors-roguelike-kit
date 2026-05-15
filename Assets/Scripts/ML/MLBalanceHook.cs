#region

using UnityEngine;

#endregion

namespace RGame.MLAgents
{
    /// <summary>
    ///     ML 밸런싱 데이터 수집용 정적 brokerage. base 게임 코드는 항상 Notify*를 호출하지만,
    ///     listener(=MLBalanceLogger)가 등록되지 않은 일반 플레이에서는 첫 줄에서 즉시 return한다.
    ///     일반 플레이 동작에 0% 영향.
    /// </summary>
    public static class MLBalanceHook
    {
        public interface ISink
        {
            void OnDamageDealt(Object enemy, int damage, string sourceSkillKey);
            void OnDamageTaken(int damage, string attackerEnemyKey, string attackKind);
            void OnEnemyDeath(Object enemy, string lastSourceSkillKey);
            void OnEnemySpawn(string enemyKey, Vector3 position, float gameTimeSec, float intensity);
        }

        private static ISink _sink;

        public static void Register(ISink sink) { _sink = sink; }

        public static void Unregister(ISink sink)
        {
            if (_sink == sink) _sink = null;
        }

        public static bool IsActive => _sink != null;

        // 단조 증가 카운터. Agent가 episode delta로 kill 보상을 정확히 받기 위해 사용한다.
        // 폴링(enemySystem.Count delta) 방식은 spawn ≥ death 속도일 때 kill을 놓치므로 이 카운터로 대체.
        public static int EnemyDeathCounter { get; private set; }

        public static void NotifyDamageDealt(Object enemy, int damage, string sourceSkillKey)
        {
            if (_sink == null) return;
            _sink.OnDamageDealt(enemy, damage, sourceSkillKey);
        }

        public static void NotifyDamageTaken(int damage, string attackerEnemyKey, string attackKind)
        {
            if (_sink == null) return;
            _sink.OnDamageTaken(damage, attackerEnemyKey, attackKind);
        }

        public static void NotifyEnemyDeath(Object enemy, string lastSourceSkillKey)
        {
            EnemyDeathCounter++;
            if (_sink == null) return;
            _sink.OnEnemyDeath(enemy, lastSourceSkillKey);
        }

        public static void NotifyEnemySpawn(string enemyKey, Vector3 position, float gameTimeSec, float intensity)
        {
            if (_sink == null) return;
            _sink.OnEnemySpawn(enemyKey, position, gameTimeSec, intensity);
        }
    }
}
