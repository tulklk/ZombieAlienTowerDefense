using AlienDefense.Combat;
using AlienDefense.Data;
using AlienDefense.Enemies;
using UnityEngine;

namespace AlienDefense.Player
{
    /// <summary>Finds the closest enemy in range and fires projectiles at it via ProjectileFactory.</summary>
    public sealed class PlayerAutoAttack : MonoBehaviour
    {
        private const float TargetRefreshInterval = 0.15f;

        [SerializeField]
        private Transform _firePoint;

        private PlayerDefinition _definition;
        private EnemyRegistry _enemyRegistry;
        private ProjectileFactory _projectileFactory;

        private float _targetRefreshTimer;
        private float _attackCooldown;
        private EnemyController _currentTarget;
        private bool _isInitialized;
        private bool _isAttackEnabled = true;

        public void Initialize(PlayerDefinition definition, EnemyRegistry enemyRegistry, ProjectileFactory projectileFactory)
        {
            _definition = definition;
            _enemyRegistry = enemyRegistry;
            _projectileFactory = projectileFactory;

            _targetRefreshTimer = 0f;
            _attackCooldown = 0f;
            _currentTarget = null;
            _isInitialized = true;
        }

        public void SetAttackEnabled(bool value)
        {
            _isAttackEnabled = value;
            if (!value)
            {
                _currentTarget = null;
            }
        }

        private void Update()
        {
            if (!_isInitialized || !_isAttackEnabled)
            {
                return;
            }

            _targetRefreshTimer -= Time.deltaTime;
            if (_targetRefreshTimer <= 0f)
            {
                _targetRefreshTimer = TargetRefreshInterval;
                RefreshTarget();
            }

            if (_currentTarget == null)
            {
                return;
            }

            _attackCooldown -= Time.deltaTime;
            if (_attackCooldown <= 0f)
            {
                Attack();
                _attackCooldown = 1f / _definition.AttacksPerSecond;
            }
        }

        private void RefreshTarget()
        {
            if (_enemyRegistry == null)
            {
                return;
            }

            float rangeSq = _definition.AttackRange * _definition.AttackRange;

            if (_currentTarget != null && _currentTarget.IsTargetable && IsWithinRangeSq(_currentTarget, rangeSq))
            {
                return;
            }

            _currentTarget = null;
            float closestDistanceSq = float.MaxValue;
            Vector3 origin = transform.position;

            for (int i = 0; i < _enemyRegistry.Count; i++)
            {
                EnemyController candidate = _enemyRegistry.GetAt(i);
                if (candidate == null || !candidate.IsTargetable)
                {
                    continue;
                }

                Vector3 candidatePosition = candidate.AimPoint.position;
                float dx = candidatePosition.x - origin.x;
                float dz = candidatePosition.z - origin.z;
                float distanceSq = dx * dx + dz * dz;

                if (distanceSq <= rangeSq && distanceSq < closestDistanceSq)
                {
                    closestDistanceSq = distanceSq;
                    _currentTarget = candidate;
                }
            }
        }

        private bool IsWithinRangeSq(EnemyController target, float rangeSq)
        {
            Vector3 origin = transform.position;
            Vector3 targetPosition = target.AimPoint.position;
            float dx = targetPosition.x - origin.x;
            float dz = targetPosition.z - origin.z;
            return dx * dx + dz * dz <= rangeSq;
        }

        private void Attack()
        {
            if (_currentTarget == null || !_currentTarget.IsTargetable || _projectileFactory == null || _firePoint == null)
            {
                return;
            }

            var damage = new DamageInfo(_definition.AttackDamage, gameObject, _currentTarget.AimPoint.position);
            var targetHandle = new CombatTargetHandle(_currentTarget);
            var request = new ProjectileSpawnRequest(_firePoint.position, _firePoint.rotation, targetHandle, damage);

            _projectileFactory.Spawn(_definition.ProjectileDefinition, request);
        }
    }
}
