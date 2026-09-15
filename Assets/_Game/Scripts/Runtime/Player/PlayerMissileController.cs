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
    /// picked at least once (rank > 0). A volley always fires the rank's full rocket count
    /// (SkillRankData.MissileCount): rockets spread over the nearest enemies in range first, and when there are
    /// fewer enemies than rockets the extra ones go after enemies already locked on (or re-pick a live one if
    /// their target died before launch). Each rocket kills what it hits outright; from rank 2 the impact also
    /// blasts every other enemy within MissileSplashRadius for MissileSplashDamage. After a volley the launcher
    /// needs a fixed cooldown - ranks add rockets and splash, never fire rate. Bosses are not one-shot: a direct
    /// hit takes a fixed share of a boss's maximum health instead.</summary>
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
        private AreaDamageResolver _areaDamage;

        private float _cooldownRemaining;
        private readonly List<EnemyController> _nearest = new List<EnemyController>(5);
        private readonly List<EnemyController> _volleyTargets = new List<EnemyController>(5);
        private readonly HashSet<EnemyController> _launchedAt = new HashSet<EnemyController>();
        private int _pendingLaunches;
        private int _launchIndex;
        private float _staggerTimer;
        private float _volleyRange;
        private float _volleySplashRadius;
        private float _volleySplashDamage;

        /// <summary>True once the Missile skill has been picked (the launcher and its HUD button exist).</summary>
        public bool IsUnlocked => _skills != null && _skills.GetRank(SkillType.Missile) > 0;

        public float CooldownDuration => _cooldownDuration;
        public float CooldownRemaining => _cooldownRemaining;

        /// <summary>1 right after a volley, 0 when ready to fire.</summary>
        public float CooldownNormalized => _cooldownDuration > 0f ? Mathf.Clamp01(_cooldownRemaining / _cooldownDuration) : 0f;

        public bool IsReady => IsUnlocked && _cooldownRemaining <= 0f && _pendingLaunches == 0;

        /// <summary>Raised when a volley is committed. Arg: rockets in it.</summary>
        public event Action<int> VolleyFired;

        /// <param name="areaDamage">Optional. Without it rockets never splash.</param>
        public void Initialize(PlayerSkillService skills, EnemyRegistry enemyRegistry, ProjectileFactory projectileFactory,
            ProjectileDefinition projectileDefinition, AreaDamageResolver areaDamage = null)
        {
            _skills = skills;
            _enemyRegistry = enemyRegistry;
            _projectileFactory = projectileFactory;
            _projectileDefinition = projectileDefinition;
            _areaDamage = areaDamage;
            _cooldownRemaining = 0f;
            _pendingLaunches = 0;
            _volleyTargets.Clear();
            _launchedAt.Clear();

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
            int rockets = Mathf.Max(1, rankData.MissileCount > 0 ? rankData.MissileCount : rank);

            CollectNearest(range, rockets, null);
            if (_nearest.Count == 0)
            {
                return; // stays ready until something comes into range
            }

            // Every rocket gets a target: the nearest enemies first, then round again over the same ones.
            _volleyTargets.Clear();
            for (int i = 0; i < rockets; i++)
            {
                _volleyTargets.Add(_nearest[i % _nearest.Count]);
            }

            _volleyRange = range;
            _volleySplashRadius = _areaDamage != null ? Mathf.Max(0f, rankData.MissileSplashRadius) : 0f;
            _volleySplashDamage = Mathf.Max(0f, rankData.MissileSplashDamage);
            _launchedAt.Clear();

            // The cooldown starts with the volley, not after its last rocket leaves.
            _cooldownRemaining = _cooldownDuration;
            _pendingLaunches = rockets;
            _launchIndex = 0;
            _staggerTimer = 0f;
            VolleyFired?.Invoke(rockets);
            TickVolley();
        }

        private void TickVolley()
        {
            _staggerTimer -= Time.deltaTime;
            while (_pendingLaunches > 0 && _staggerTimer <= 0f)
            {
                Launch(ResolveLaunchTarget(_volleyTargets[_launchIndex]), _launchIndex, _volleyTargets.Count);
                _launchIndex++;
                _pendingLaunches--;
                _staggerTimer += _volleyStagger;
            }

            if (_pendingLaunches == 0)
            {
                _volleyTargets.Clear();
                _launchedAt.Clear();
            }
        }

        /// <summary>Spreads the volley: the planned target if no rocket has gone after it yet; else the nearest live
        /// enemy nobody is chasing (new ones may have walked into range); else the planned target again if it is
        /// alive, or any live enemy in range. Null only if the range is empty.</summary>
        private EnemyController ResolveLaunchTarget(EnemyController planned)
        {
            bool plannedAlive = planned != null && planned.IsTargetable;
            if (plannedAlive && !_launchedAt.Contains(planned))
            {
                return planned;
            }

            CollectNearest(_volleyRange, 1, _launchedAt);
            if (_nearest.Count > 0)
            {
                return _nearest[0];
            }

            if (plannedAlive)
            {
                return planned;
            }

            CollectNearest(_volleyRange, 1, null);
            return _nearest.Count > 0 ? _nearest[0] : null;
        }

        private void Launch(EnemyController target, int index, int count)
        {
            if (target == null)
            {
                return; // nothing left in range for this rocket
            }

            _launchedAt.Add(target);

            float maxHealth = target.Health != null ? target.Health.MaximumHealth : 0f;
            float damage = target.BossController != null
                ? maxHealth * _bossDamageFraction
                : Mathf.Max(maxHealth, target.Health != null ? target.Health.CurrentHealth : 0f) * 10f; // kills through any armour

            float side = count > 1 ? (index - (count - 1) * 0.5f) * _launchSpread : 0f;
            Vector3 spawn = _firePoint.position + _firePoint.right * side;
            Vector3 toTarget = target.AimPoint.position - spawn;
            Quaternion rotation = toTarget.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(toTarget.normalized, Vector3.up) : _firePoint.rotation;

            var damageInfo = new DamageInfo(damage, gameObject, target.AimPoint.position);
            bool splash = _volleySplashRadius > 0f && _volleySplashDamage > 0f;
            var request = new ProjectileSpawnRequest(
                spawn, rotation, new CombatTargetHandle(target), damageInfo,
                areaDamageResolver: splash ? _areaDamage : null,
                splashRadius: splash ? _volleySplashRadius : 0f,
                splashDamage: splash ? _volleySplashDamage : 0f);
            _projectileFactory.Spawn(_projectileDefinition, request);
        }

        /// <summary>Fills _nearest with up to maxTargets targetable enemies in range, nearest first, skipping any
        /// in exclude.</summary>
        private void CollectNearest(float range, int maxTargets, HashSet<EnemyController> exclude)
        {
            _nearest.Clear();
            float rangeSqr = range * range;
            Vector3 origin = _firePoint.position;

            for (int i = 0; i < _enemyRegistry.Count; i++)
            {
                EnemyController candidate = _enemyRegistry.GetAt(i);
                if (candidate == null || !candidate.IsTargetable || (exclude != null && exclude.Contains(candidate)))
                {
                    continue;
                }

                float sqrDist = (candidate.AimPoint.position - origin).sqrMagnitude;
                if (sqrDist > rangeSqr)
                {
                    continue;
                }

                // Insertion into a short sorted list (at most 5 rockets).
                int insertAt = _nearest.Count;
                for (int j = 0; j < _nearest.Count; j++)
                {
                    if (sqrDist < (_nearest[j].AimPoint.position - origin).sqrMagnitude)
                    {
                        insertAt = j;
                        break;
                    }
                }

                if (insertAt >= maxTargets)
                {
                    continue;
                }

                _nearest.Insert(insertAt, candidate);
                if (_nearest.Count > maxTargets)
                {
                    _nearest.RemoveAt(_nearest.Count - 1);
                }
            }
        }
    }
}
