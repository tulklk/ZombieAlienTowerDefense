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

        [SerializeField]
        [Tooltip("Optional. Particle effects riding on the projectile (e.g. a rocket's exhaust). Restarted from " +
            "empty every time the pooled projectile is fired and cleared when it is returned, so world-space " +
            "particles from its previous flight never reappear.")]
        private ParticleSystem[] _attachedParticles = Array.Empty<ParticleSystem>();

        [SerializeField]
        [Tooltip("On: if the target dies or disappears mid-flight, keep flying to where it last was and detonate " +
            "there (splash still applies). Off: vanish, as bullets do.")]
        private bool _detonateAtLastPositionWhenTargetLost;

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
        private float _splashDamage;
        private Vector3 _lastAimPosition;
        private bool _hasLastAimPosition;

        private float _elapsedLifetime;
        private bool _hasHit;
        private bool _isInitialized;

        private float _arcHeight;
        private Vector3 _arcStart;
        private float _arcProgress;

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
            float splashRadius = 0f,
            float splashDamage = 0f,
            VfxDefinition killVfx = null)
        {
            _target = target;
            // Rides on the damage itself, so whatever resolves the kill plays it in place of the usual defeat effect.
            _damageInfo = killVfx != null ? damageInfo.WithKillVfx(killVfx) : damageInfo;
            _speed = Mathf.Max(0.01f, speed);
            _maximumLifetime = Mathf.Max(0.01f, maximumLifetime);
            _hitDistance = Mathf.Max(0.01f, hitDistance);
            _releaseToPool = releaseToPool;
            _vfxService = vfxService;
            _hitVfx = hitVfx;
            _statusEffectOnHit = statusEffectOnHit;
            _areaDamageResolver = areaDamageResolver;
            _splashRadius = splashRadius;
            _splashDamage = splashDamage;
            _hasLastAimPosition = false;
            _arcHeight = 0f;

            _elapsedLifetime = 0f;
            _hasHit = false;
            _isInitialized = true;
        }

        /// <summary>Optional, after Initialize and once the projectile is at its muzzle: above 0 the projectile is
        /// lobbed - it rises to <paramref name="arcHeight"/> above the straight line at mid-flight and drops onto
        /// the target, whose live position it keeps tracking.</summary>
        public void SetArc(float arcHeight)
        {
            _arcHeight = Mathf.Max(0f, arcHeight);
            _arcStart = transform.position;
            _arcProgress = 0f;
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
            _splashDamage = 0f;
            _hasLastAimPosition = false;

            if (_trail != null)
            {
                _trail.emitting = false;
                _trail.Clear();
            }

            for (int i = 0; i < _attachedParticles.Length; i++)
            {
                if (_attachedParticles[i] != null)
                {
                    _attachedParticles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }

        // The factory positions the projectile before activating it, so this restarts the attached effects at
        // the muzzle rather than wherever the instance was created or last flew.
        private void OnEnable()
        {
            // A pooled projectile must never draw a streak from where it last flew to the new muzzle.
            if (_trail != null)
            {
                _trail.Clear();
                _trail.emitting = true;
            }

            for (int i = 0; i < _attachedParticles.Length; i++)
            {
                if (_attachedParticles[i] != null)
                {
                    _attachedParticles[i].Clear(true);
                    _attachedParticles[i].Play(true);
                }
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

            Vector3 aimPosition;
            if (_target.IsValid)
            {
                aimPosition = _target.AimPoint.position;
                _lastAimPosition = aimPosition;
                _hasLastAimPosition = true;
            }
            else if (_detonateAtLastPositionWhenTargetLost && _hasLastAimPosition)
            {
                aimPosition = _lastAimPosition;
            }
            else
            {
                Despawn();
                return;
            }

            if (_arcHeight > 0f)
            {
                MoveAlongArc(aimPosition);
                return;
            }

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

        /// <summary>Lobbed flight: progress runs along the line from the launch point to the target's current aim
        /// point at the projectile's speed, with a parabola (0 at both ends, _arcHeight at the middle) added on top,
        /// so it climbs out of the barrel, turns over and falls onto the target. It faces its direction of travel.</summary>
        private void MoveAlongArc(Vector3 aimPosition)
        {
            float lineLength = Mathf.Max(0.5f, Vector3.Distance(_arcStart, aimPosition));
            _arcProgress = Mathf.Min(1f, _arcProgress + _speed * Time.deltaTime / lineLength);

            Vector3 previous = transform.position;
            Vector3 next = Vector3.Lerp(_arcStart, aimPosition, _arcProgress)
                + Vector3.up * (_arcHeight * 4f * _arcProgress * (1f - _arcProgress));
            transform.position = next;

            Vector3 travel = next - previous;
            if (travel.sqrMagnitude > 0.000001f)
            {
                transform.rotation = Quaternion.LookRotation(travel.normalized, Vector3.up);
            }

            if (_arcProgress >= 1f)
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

            if (_areaDamageResolver != null && _splashDamage > 0f)
            {
                // Direct hit first, then the blast hurts whatever else is around (a killed target is no longer
                // targetable, so the splash does not hit it twice).
                if (_target.IsValid)
                {
                    _target.Damageable?.TryApplyDamage(_damageInfo);
                }

                var splashInfo = new DamageInfo(_splashDamage, _damageInfo.Source, transform.position, _damageInfo.DamageType,
                    _damageInfo.KillVfx, _damageInfo.PopupStyle);
                _areaDamageResolver.ResolveSplash(transform.position, _splashRadius, splashInfo);
            }
            else if (_areaDamageResolver != null)
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
