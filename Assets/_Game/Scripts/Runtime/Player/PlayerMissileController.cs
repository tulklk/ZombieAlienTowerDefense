using System;
using System.Collections.Generic;
using AlienDefense.Combat;
using AlienDefense.Data;
using AlienDefense.Economy;
using AlienDefense.Enemies;
using UnityEngine;

namespace AlienDefense.Player
{
    /// <summary>Auto-fires volleys of ProjectileFactory-spawned rockets while the player's Missile skill has been
    /// picked at least once (rank > 0). Each rocket locks onto a different targetable enemy in range (nearest
    /// first) and kills it outright; after a volley the launcher needs a fixed cooldown before the next one.
    /// Ranking the skill up adds rockets to each volley (SkillRankData.MissileCount) - it never shortens the
    /// cooldown. Bosses are not one-shot: a rocket takes a fixed share of a boss's maximum health instead.</summary>
    public sealed class PlayerMissileController : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("World-space origin missiles spawn from and aim range is measured from. Falls back to this " +
            "transform if left unassigned.")]
        private Transform _firePoint;

        [SerializeField, Min(0.1f)]
        [Tooltip("Base targeting range in world units before the Missile skill's rank-5 'tăng phạm vi' bonus.")]
        private float _baseRange = 12f;

        [SerializeField, Min(0.5f)]
        [Tooltip("Seconds between volleys. Fixed - skill ranks add rockets, not fire rate.")]
        private float _cooldownDuration = 10f;

        [SerializeField, Min(0f)]
        [Tooltip("Seconds between rockets of the same volley leaving the launcher, so they read as a salvo.")]
        private float _volleyStagger = 0.12f;

        [SerializeField, Min(0f)]
        [Tooltip("Sideways spread (metres) between rockets of one volley at launch.")]
        private float _launchSpread = 0.35f;

        [SerializeField, Range(0.01f, 1f)]
        [Tooltip("A rocket hitting a boss removes this fraction of the boss's maximum health instead of killing it.")]
        private float _bossDamageFraction = 0.1f;

        private PlayerSkillService _skills;
        private EnemyRegistry _enemyRegistry;
        private ProjectileFactory _projectileFactory;
        private ProjectileDefinition _projectileDefinition;

        private float _cooldownRemaining;
        private readonly List<EnemyController> _volleyTargets = new List<EnemyController>(5);
        private int _pendingLaunches;
        private int _launchIndex;
        private float _staggerTimer;

        /// <summary>True once the Missile skill has been picked (the launcher and its HUD button exist).</summary>
        public bool IsUnlocked => _skills != null && _skills.GetRank(SkillType.Missile) > 0;

        public float CooldownDuration => _cooldownDuration;
        public float CooldownRemaining => _cooldownRemaining;

        /// <summary>1 right after a volley, 0 when ready to fire.</summary>
        public float CooldownNormalized => _cooldownDuration > 0f ? Mathf.Clamp01(_cooldownRemaining / _cooldownDuration) : 0f;

        public bool IsReady => IsUnlocked && _cooldownRemaining <= 0f && _pendingLaunches == 0;

        /// <summary>Raised when a volley is committed. Arg: rockets in it.</summary>
        public event Action<int> VolleyFired;

        public void Initialize(PlayerSkillService skills, EnemyRegistry enemyRegistry, ProjectileFactory projectileFactory, ProjectileDefinition projectileDefinition)
        {
            _skills = skills;
            _enemyRegistry = enemyRegistry;
            _projectileFactory = projectileFactory;
            _projectileDefinition = projectileDefinition;
            _cooldownRemaining = 0f;
            _pendingLaunches = 0;
            _volleyTargets.Clear();

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

            if (_pendingLaunches > 0)
            {
                TickVolley();
                return;
            }

            if (_cooldownRemaining > 0f)
            {
                _cooldownRemaining = Mathf.Max(0f, _cooldownRemaining - Time.deltaTime);
                return;
            }

            SkillDefinition definition = _skills.GetDefinition(SkillType.Missile);
            if (definition == null)
            {
                return;
            }

            SkillRankData rankData = definition.GetRank(rank);
            float range = _baseRange * Mathf.Max(1f, rankData.MissileRangeMultiplier);
            int rockets = rankData.MissileCount > 0 ? rankData.MissileCount : rank;

            CollectTargets(range, rockets);
            if (_volleyTargets.Count == 0)
            {
                return; // stays ready until something comes into range
            }

            // The cooldown starts with the volley, not after its last rocket leaves.
            _cooldownRemaining = _cooldownDuration;
            _pendingLaunches = _volleyTargets.Count;
            _launchIndex = 0;
            _staggerTimer = 0f;
            VolleyFired?.Invoke(_pendingLaunches);
            TickVolley();
        }

        private void TickVolley()
        {
            _staggerTimer -= Time.deltaTime;
            while (_pendingLaunches > 0 && _staggerTimer <= 0f)
            {
                Launch(_volleyTargets[_launchIndex], _launchIndex, _volleyTargets.Count);
                _launchIndex++;
                _pendingLaunches--;
                _staggerTimer += _volleyStagger;
            }

            if (_pendingLaunches == 0)
            {
                _volleyTargets.Clear();
            }
        }

        private void Launch(EnemyController target, int index, int count)
        {
            if (target == null || !target.IsTargetable)
            {
                return; // died before its rocket left - that rocket is simply not fired
            }

            float maxHealth = target.Health != null ? target.Health.MaximumHealth : 0f;
            float damage = target.BossController != null
                ? maxHealth * _bossDamageFraction
                : Mathf.Max(maxHealth, target.Health != null ? target.Health.CurrentHealth : 0f) * 10f; // kills through any armour

            float side = count > 1 ? (index - (count - 1) * 0.5f) * _launchSpread : 0f;
            Vector3 spawn = _firePoint.position + _firePoint.right * side;
            Vector3 toTarget = target.AimPoint.position - spawn;
            Quaternion rotation = toTarget.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(toTarget.normalized, Vector3.up) : _firePoint.rotation;

            var damageInfo = new DamageInfo(damage, gameObject, target.AimPoint.position);
            var request = new ProjectileSpawnRequest(spawn, rotation, new CombatTargetHandle(target), damageInfo);
            _projectileFactory.Spawn(_projectileDefinition, request);
        }

        /// <summary>Nearest-first, one rocket per enemy.</summary>
        private void CollectTargets(float range, int maxTargets)
        {
            _volleyTargets.Clear();
            float rangeSqr = range * range;
            Vector3 origin = _firePoint.position;

            for (int i = 0; i < _enemyRegistry.Count; i++)
            {
                EnemyController candidate = _enemyRegistry.GetAt(i);
                if (candidate == null || !candidate.IsTargetable)
                {
                    continue;
                }

                float sqrDist = (candidate.AimPoint.position - origin).sqrMagnitude;
                if (sqrDist > rangeSqr)
                {
                    continue;
                }

                // Insertion into a short sorted list (at most 5 rockets).
                int insertAt = _volleyTargets.Count;
                for (int j = 0; j < _volleyTargets.Count; j++)
                {
                    if (sqrDist < (_volleyTargets[j].AimPoint.position - origin).sqrMagnitude)
                    {
                        insertAt = j;
                        break;
                    }
                }

                if (insertAt >= maxTargets)
                {
                    continue;
                }

                _volleyTargets.Insert(insertAt, candidate);
                if (_volleyTargets.Count > maxTargets)
                {
                    _volleyTargets.RemoveAt(_volleyTargets.Count - 1);
                }
            }
        }
    }
}
