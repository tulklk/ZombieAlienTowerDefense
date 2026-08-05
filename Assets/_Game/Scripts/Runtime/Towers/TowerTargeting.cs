using AlienDefense.Enemies;
using UnityEngine;

namespace AlienDefense.Towers
{
    /// <summary>Finds and holds the current in-range target for a tower, refreshed on an interval via Tick.</summary>
    public sealed class TowerTargeting : MonoBehaviour
    {
        private const float TargetRefreshInterval = 0.2f;

        private EnemyRegistry _enemyRegistry;
        private ITargetingStrategy _strategy;
        private float _range;
        private float _refreshTimer;

        public EnemyController CurrentTarget { get; private set; }

        public void Initialize(EnemyRegistry enemyRegistry, ITargetingStrategy strategy, float range)
        {
            _enemyRegistry = enemyRegistry;
            _strategy = strategy;
            _range = Mathf.Max(0.01f, range);
            _refreshTimer = 0f;
            CurrentTarget = null;
        }

        public void SetRange(float range)
        {
            _range = Mathf.Max(0.01f, range);
        }

        public void SetStrategy(ITargetingStrategy strategy)
        {
            _strategy = strategy;
        }

        public void Tick(float deltaTime)
        {
            if (CurrentTarget != null && (!CurrentTarget.IsTargetable || !IsWithinRange(CurrentTarget)))
            {
                CurrentTarget = null;
            }

            _refreshTimer -= deltaTime;
            if (_refreshTimer > 0f)
            {
                return;
            }

            _refreshTimer = TargetRefreshInterval;
            CurrentTarget = _strategy?.SelectTarget(_enemyRegistry, transform.position, _range);
        }

        private bool IsWithinRange(EnemyController target)
        {
            Vector3 origin = transform.position;
            Vector3 targetPosition = target.AimPoint.position;
            float dx = targetPosition.x - origin.x;
            float dz = targetPosition.z - origin.z;
            return dx * dx + dz * dz <= _range * _range;
        }
    }
}
