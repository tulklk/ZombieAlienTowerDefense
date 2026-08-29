using System;
using AlienDefense.Vfx;
using UnityEngine;

namespace AlienDefense.Combat
{
    /// <summary>Moves toward its target and applies damage on arrival; despawns on hit, timeout or lost target.</summary>
    public sealed class ProjectileController : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Optional.")]
        private TrailRenderer _trail;

        private CombatTargetHandle _target;
        private DamageInfo _damageInfo;
        private float _speed;
        private float _maximumLifetime;
        private float _hitDistance;
        private Action<ProjectileController> _releaseToPool;
        private VfxService _vfxService;
        private VfxDefinition _hitVfx;
        private StatusEffectDefinition _statusEffectOnHit;
        private AreaDamageResolver _areaDamageResolver;
        private float _splashRadius;

        private float _elapsedLifetime;
        private bool _hasHit;
        private bool _isInitialized;

        public void Initialize(
            CombatTargetHandle target,
            DamageInfo damageInfo,
            float speed,
            float maximumLifetime,
            float hitDistance,
            Action<ProjectileController> releaseToPool,
            VfxService vfxService = null,
            VfxDefinition hitVfx = null,
            StatusEffectDefinition statusEffectOnHit = null,
            AreaDamageResolver areaDamageResolver = null,
            float splashRadius = 0f)
        {
            _target = target;
            _damageInfo = damageInfo;
            _speed = Mathf.Max(0.01f, speed);
            _maximumLifetime = Mathf.Max(0.01f, maximumLifetime);
            _hitDistance = Mathf.Max(0.01f, hitDistance);
            _releaseToPool = releaseToPool;
            _vfxService = vfxService;
            _hitVfx = hitVfx;
            _statusEffectOnHit = statusEffectOnHit;
            _areaDamageResolver = areaDamageResolver;
            _splashRadius = splashRadius;

            _elapsedLifetime = 0f;
            _hasHit = false;
            _isInitialized = true;
        }

        /// <summary>Called by the owning pool when this instance is returned, including prewarm.</summary>
        public void HandleReturnedToPool()
        {
            _isInitialized = false;
            _hasHit = false;
            _elapsedLifetime = 0f;
            _statusEffectOnHit = null;
            _areaDamageResolver = null;
            _splashRadius = 0f;

            if (_trail != null)
            {
                _trail.Clear();
            }
        }

        private void Update()
        {
            if (!_isInitialized)
            {
                return;
            }

            _elapsedLifetime += Time.deltaTime;
            if (_elapsedLifetime >= _maximumLifetime)
            {
                Despawn();
                return;
            }

            if (!_target.IsValid)
            {
                Despawn();
                return;
            }

            Vector3 aimPosition = _target.AimPoint.position;
            Vector3 currentPosition = transform.position;
            Vector3 newPosition = Vector3.MoveTowards(currentPosition, aimPosition, _speed * Time.deltaTime);
            transform.position = newPosition;

            Vector3 direction = aimPosition - currentPosition;
            if (direction.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }

            if (Vector3.Distance(newPosition, aimPosition) <= _hitDistance)
            {
                ApplyHit();
            }
        }

        private void ApplyHit()
        {
            if (_hasHit)
            {
                return;
            }

            _hasHit = true;

            if (_areaDamageResolver != null)
            {
                _areaDamageResolver.ResolveSplash(transform.position, _splashRadius, _damageInfo);
            }
            else if (_target.IsValid)
            {
                _target.Damageable?.TryApplyDamage(_damageInfo);
            }

            if (_statusEffectOnHit != null && _target.IsValid)
            {
                _target.StatusController?.ApplyStatus(_statusEffectOnHit, _damageInfo.Source);
            }

            _vfxService?.Play(_hitVfx, transform.position, transform.rotation);

            Despawn();
        }

        private void Despawn()
        {
            if (!_isInitialized)
            {
                return;
            }

            _isInitialized = false;
            _releaseToPool?.Invoke(this);
        }
    }
}
