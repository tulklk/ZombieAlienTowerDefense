using AlienDefense.Combat;
using AlienDefense.Data;
using AlienDefense.Economy;
using AlienDefense.Enemies;
using UnityEngine;

namespace AlienDefense.Player
{
    /// <summary>Auto-fires ProjectileFactory-spawned missiles at the nearest targetable enemy in range, only
    /// while the player's Missile skill has been picked at least once (rank > 0). Unlike
    /// PlayerSkillEffectApplier's targets there is no persistent state to keep in sync here - damage and range
    /// are read straight from the current rank's SkillRankData every time a shot fires.</summary>
    public sealed class PlayerMissileController : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("World-space origin missiles spawn from and aim range is measured from. Falls back to this " +
            "transform if left unassigned.")]
        private Transform _firePoint;

        [SerializeField, Min(0.1f)]
        [Tooltip("Base targeting range in world units before the Missile skill's rank-5 'tăng phạm vi' bonus.")]
        private float _baseRange = 12f;

        [SerializeField, Min(0.1f)]
        private float _attacksPerSecond = 1f;

        [SerializeField, Min(0f)]
        [Tooltip("Base damage before the current rank's MissileDamageMultiplier.")]
        private float _baseDamage = 15f;

        private PlayerSkillService _skills;
        private EnemyRegistry _enemyRegistry;
        private ProjectileFactory _projectileFactory;
        private ProjectileDefinition _projectileDefinition;
        private float _cooldownTimer;

        public void Initialize(PlayerSkillService skills, EnemyRegistry enemyRegistry, ProjectileFactory projectileFactory, ProjectileDefinition projectileDefinition)
        {
            _skills = skills;
            _enemyRegistry = enemyRegistry;
            _projectileFactory = projectileFactory;
            _projectileDefinition = projectileDefinition;

            if (_firePoint == null)
            {
                _firePoint = transform;
            }
        }

        private void Update()
        {
            if (_skills == null || _enemyRegistry == null || _projectileFactory == null || _projectileDefinition == null || _firePoint == null)
            {
                return;
            }

            int rank = _skills.GetRank(SkillType.Missile);
            if (rank <= 0)
            {
                return; // Missile skill never picked yet - the UFO stays unarmed
            }

            _cooldownTimer -= Time.deltaTime;
            if (_cooldownTimer > 0f)
            {
                return;
            }

            SkillDefinition definition = _skills.GetDefinition(SkillType.Missile);
            if (definition == null)
            {
                return;
            }

            SkillRankData rankData = definition.GetRank(rank);
            float range = _baseRange * Mathf.Max(1f, rankData.MissileRangeMultiplier);

            EnemyController target = FindNearestTarget(range);
            if (target == null)
            {
                return; // still on cooldown next frame's check is skipped, but nothing to shoot right now
            }

            float damage = _baseDamage * Mathf.Max(0.01f, rankData.MissileDamageMultiplier);
            var targetHandle = new CombatTargetHandle(target);
            var damageInfo = new DamageInfo(damage, gameObject, target.AimPoint.position);
            var request = new ProjectileSpawnRequest(_firePoint.position, _firePoint.rotation, targetHandle, damageInfo);
            _projectileFactory.Spawn(_projectileDefinition, request);

            _cooldownTimer = 1f / _attacksPerSecond;
        }

        private EnemyController FindNearestTarget(float range)
        {
            EnemyController nearest = null;
            float nearestSqr = range * range;
            Vector3 origin = _firePoint.position;

            for (int i = 0; i < _enemyRegistry.Count; i++)
            {
                EnemyController candidate = _enemyRegistry.GetAt(i);
                if (candidate == null || !candidate.IsTargetable)
                {
                    continue;
                }

                float sqrDist = (candidate.AimPoint.position - origin).sqrMagnitude;
                if (sqrDist <= nearestSqr)
                {
                    nearestSqr = sqrDist;
                    nearest = candidate;
                }
            }

            return nearest;
        }
    }
}
