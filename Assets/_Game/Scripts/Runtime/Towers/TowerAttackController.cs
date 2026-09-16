using System;
using AlienDefense.Combat;
using AlienDefense.Enemies;
using AlienDefense.Vfx;
using UnityEngine;

namespace AlienDefense.Towers
{
    /// <summary>Owns attack cooldown and delegates the actual attack to an IAttackStrategy.</summary>
    public sealed class TowerAttackController : MonoBehaviour
    {
        [SerializeField]
        private Transform _firePoint;

        [SerializeField]
        [Tooltip("Optional. Extra muzzles (e.g. the second barrel of a twin gun) that fire together with the fire " +
            "point. The attack's damage is shared evenly across all muzzles so damage per second is unchanged, and " +
            "on-hit effects (slow, stun build-up) ride only on the fire point's shot.")]
        private Transform[] _extraFirePoints = Array.Empty<Transform>();

        private IAttackStrategy _attackStrategy;
        private GameObject _sourceObject;
        private float _damage;
        private float _attacksPerSecond;
        private float _cooldownTimer;
        private bool _isAttackEnabled = true;
        private VfxService _vfxService;
        private VfxDefinition _muzzleVfx;

        public void Initialize(IAttackStrategy attackStrategy, GameObject sourceObject)
        {
            _attackStrategy = attackStrategy;
            _sourceObject = sourceObject;
            _cooldownTimer = 0f;
        }

        /// <summary>Optional. Wires the muzzle flash VFX played on every successful attack.</summary>
        public void SetMuzzleVfx(VfxService vfxService, VfxDefinition muzzleVfx)
        {
            _vfxService = vfxService;
            _muzzleVfx = muzzleVfx;
        }

        public void ApplyStats(float damage, float attacksPerSecond)
        {
            _damage = damage;
            _attacksPerSecond = Mathf.Max(0.01f, attacksPerSecond);
        }

        public void SetAttackEnabled(bool value)
        {
            _isAttackEnabled = value;
        }

        public void Tick(float deltaTime, EnemyController target)
        {
            if (!_isAttackEnabled || target == null || _firePoint == null || _attackStrategy == null)
            {
                return;
            }

            _cooldownTimer -= deltaTime;
            if (_cooldownTimer > 0f)
            {
                return;
            }

            int muzzleCount = 1 + CountExtraFirePoints();
            var damageInfo = new DamageInfo(_damage / muzzleCount, _sourceObject, target.AimPoint.position);
            if (!_attackStrategy.TryAttack(target, damageInfo, _firePoint))
            {
                return;
            }

            _cooldownTimer = 1f / _attacksPerSecond;
            _vfxService?.Play(_muzzleVfx, _firePoint.position, _firePoint.rotation);

            for (int i = 0; i < _extraFirePoints.Length; i++)
            {
                Transform muzzle = _extraFirePoints[i];
                if (muzzle != null && _attackStrategy.TryAttack(target, damageInfo, muzzle, isFollowUpShot: true))
                {
                    _vfxService?.Play(_muzzleVfx, muzzle.position, muzzle.rotation);
                }
            }
        }

        private int CountExtraFirePoints()
        {
            int count = 0;
            for (int i = 0; i < _extraFirePoints.Length; i++)
            {
                if (_extraFirePoints[i] != null)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
