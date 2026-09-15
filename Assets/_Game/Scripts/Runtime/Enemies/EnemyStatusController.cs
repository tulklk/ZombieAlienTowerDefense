using System.Collections.Generic;
using AlienDefense.Combat;
using UnityEngine;

namespace AlienDefense.Enemies
{
    /// <summary>Owns every active status effect on one enemy: apply/refresh/stack/tick/remove, the per-enemy hit counts
    /// that build up to a proc (Frost slow hits -> Ice Stun), the Stun movement lock, and each effect's optional
    /// attached VFX (e.g. ice crystals around a stunned enemy). Never grants rewards or resolves the enemy.</summary>
    public sealed class EnemyStatusController : MonoBehaviour, IStatusApplicable
    {
        private const float MinimumSpeedMultiplier = 0.2f;
        private const float AuthoredBodyHeight = 1.8f;
        private const float MeltDuration = 0.4f;

        [SerializeField]
        private EnemyMovement _movement;

        [SerializeField]
        private EnemyHealth _health;

        private readonly Dictionary<StatusEffectDefinition, StatusEffectInstance> _activeEffects =
            new Dictionary<StatusEffectDefinition, StatusEffectInstance>();

        private readonly List<StatusEffectDefinition> _expiredBuffer = new List<StatusEffectDefinition>();

        // One reusable instance per effect type, created the first time this (pooled) enemy gets that effect. Either
        // it animates itself (IStatusEffectVisual, e.g. the ice prison) or it is just particle systems played here.
        private sealed class AttachedVisual
        {
            public IStatusEffectVisual Custom;
            public ParticleSystem[] Systems;
        }

        private readonly Dictionary<StatusEffectDefinition, AttachedVisual> _attachedVisuals =
            new Dictionary<StatusEffectDefinition, AttachedVisual>();

        private readonly HashSet<StatusEffectDefinition> _meltingVisuals = new HashSet<StatusEffectDefinition>();
        private ParticleSystem.Particle[] _particleBuffer;

        // Per enemy, per proc-carrying effect: hits so far and the randomly rolled hits needed for the next proc.
        private sealed class ProcCounter
        {
            public int Hits;
            public int Required;
        }

        private readonly Dictionary<StatusEffectDefinition, ProcCounter> _procCounters =
            new Dictionary<StatusEffectDefinition, ProcCounter>();

        [Header("Debug (read-only, runtime)")]
        [SerializeField]
        private int _debugProcHits;

        [SerializeField]
        private int _debugProcRequiredHits;

        [SerializeField]
        private bool _debugIsStunned;

        private StatusEffectDefinition _lastProcSource;
        private float _pendingProcCooldown;
        private float _procBlockedUntil;
        private float _clock; // advanced by Tick, so tests can drive time
        private EnemyController _owner;
        private Animator _animator;
        private float _animatorSpeedBeforeStun = 1f;
        private bool _animatorFrozen;

        private GameObject _lastStatusSource;

        private void Awake()
        {
            if (_health != null)
            {
                _health.Died += HandleDied;
            }
        }

        private void OnDestroy()
        {
            if (_health != null)
            {
                _health.Died -= HandleDied;
            }
        }

        /// <summary>Called by EnemyController on Initialize and on pool reuse.</summary>
        public void Clear()
        {
            _activeEffects.Clear();
            _lastStatusSource = null;
            SetStunned(false);
            _movement?.SetStatusSpeedMultiplier(1f);
            HideAllVisuals(ParticleSystemStopBehavior.StopEmittingAndClear);

            // A pooled enemy starts its next life with no build-up towards a proc.
            _procCounters.Clear();
            _lastProcSource = null;
            _procBlockedUntil = 0f;
            _clock = 0f;
            _debugProcHits = 0;
            _debugProcRequiredHits = 0;
        }

        /// <summary>True while a Stun holds this enemy: movement is fully stopped (damage still lands normally).</summary>
        public bool IsStunned { get; private set; }

        public void ApplyStatus(StatusEffectDefinition definition, GameObject source = null)
        {
            if (definition == null)
            {
                return;
            }

            if (source != null)
            {
                _lastStatusSource = source;
            }

            if (_activeEffects.TryGetValue(definition, out StatusEffectInstance existing))
            {
                existing.Reapply();
            }
            else
            {
                float durationScale = definition.Type == StatusEffectType.Stun ? StunDurationScale : 1f;
                existing = new StatusEffectInstance(definition, durationScale);
                _activeEffects.Add(definition, existing);
            }

            ShowVisual(definition, existing.RemainingDuration);
            RefreshSpeedMultiplier();
            CountTowardsProc(definition);
        }

        /// <summary>One more hit of a proc-carrying effect (e.g. a Frost slow) on this enemy: when the randomly rolled
        /// number of hits is reached, its proc effect (e.g. the Ice Stun) is applied and the count starts over with a
        /// fresh roll. Hits while stunned, or within the proc cooldown after a stun, do not count.</summary>
        private void CountTowardsProc(StatusEffectDefinition definition)
        {
            StatusEffectDefinition proc = definition.ProcEffect;
            if (proc == null || proc == definition || IsStunned || _clock < _procBlockedUntil)
            {
                return;
            }

            if (!_procCounters.TryGetValue(definition, out ProcCounter counter))
            {
                counter = new ProcCounter { Required = RollProcThreshold(definition) };
                _procCounters.Add(definition, counter);
            }

            _lastProcSource = definition;
            counter.Hits++;
            _debugProcHits = counter.Hits;
            _debugProcRequiredHits = counter.Required;

            if (counter.Hits < counter.Required)
            {
                return;
            }

            counter.Hits = 0;
            counter.Required = RollProcThreshold(definition);
            _debugProcHits = 0;
            _debugProcRequiredHits = counter.Required;
            _pendingProcCooldown = definition.ProcCooldown;
            ApplyStatus(proc, _lastStatusSource);
        }

        private static int RollProcThreshold(StatusEffectDefinition definition)
        {
            return Random.Range(definition.ProcMinHits, definition.ProcMaxHits + 1);
        }

        // Crowd-control resistance lives on the enemy's own definition (e.g. the Boss holds still for half as long).
        private float StunDurationScale
        {
            get
            {
                if (_owner == null)
                {
                    _owner = GetComponent<EnemyController>();
                }

                return _owner != null && _owner.Definition != null ? _owner.Definition.StunDurationMultiplier : 1f;
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Debug Trigger Proc (Ice Stun)")]
        private void DebugTriggerProc()
        {
            if (_lastProcSource == null || _lastProcSource.ProcEffect == null)
            {
                Debug.LogWarning("[EnemyStatusController] Hit this enemy with a proc-carrying effect (e.g. a Frost " +
                    "Tower) at least once first, so it knows which proc to trigger.", this);
                return;
            }

            _pendingProcCooldown = _lastProcSource.ProcCooldown;
            ApplyStatus(_lastProcSource.ProcEffect, _lastStatusSource);
        }
#endif

        public bool HasEffect(StatusEffectType type)
        {
            foreach (KeyValuePair<StatusEffectDefinition, StatusEffectInstance> pair in _activeEffects)
            {
                if (pair.Key.Type == type)
                {
                    return true;
                }
            }

            return false;
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>Advances every active effect by deltaTime, applies elapsed Burn ticks, and removes expired effects.
        /// Separated from Update() so tests can drive it with an explicit deltaTime instead of relying on Time.deltaTime.</summary>
        public void Tick(float deltaTime)
        {
            _clock += deltaTime;

            if (_activeEffects.Count == 0)
            {
                return;
            }

            _expiredBuffer.Clear();

            foreach (KeyValuePair<StatusEffectDefinition, StatusEffectInstance> pair in _activeEffects)
            {
                StatusEffectInstance instance = pair.Value;
                bool tickElapsed = instance.Tick(deltaTime);

                if (tickElapsed && pair.Key.Type == StatusEffectType.Burn)
                {
                    ApplyBurnTick(pair.Key, instance);
                }

                if (instance.IsExpired)
                {
                    _expiredBuffer.Add(pair.Key);
                }
            }

            if (_expiredBuffer.Count == 0)
            {
                return;
            }

            for (int i = 0; i < _expiredBuffer.Count; i++)
            {
                _activeEffects.Remove(_expiredBuffer[i]);
                MeltVisual(_expiredBuffer[i]);
            }

            RefreshSpeedMultiplier();
        }

        private void ApplyBurnTick(StatusEffectDefinition definition, StatusEffectInstance instance)
        {
            if (_health == null || !_health.IsDamageable)
            {
                return;
            }

            float damage = definition.Magnitude * instance.StackCount;
            var damageInfo = new DamageInfo(damage, _lastStatusSource, transform.position, DamageType.True);
            _health.TryApplyDamage(damageInfo);
        }

        /// <summary>Recomputed from the active effects every time one starts or ends - never "remember the old speed and
        /// put it back" - so the result is always base speed x strongest Slow x (Stun ? 0 : 1): an enemy stunned while
        /// slowed comes out of the stun still slowed if that Slow is still running, and at full speed if not.</summary>
        private void RefreshSpeedMultiplier()
        {
            float slowMultiplier = 1f;
            bool stunned = false;
            foreach (KeyValuePair<StatusEffectDefinition, StatusEffectInstance> pair in _activeEffects)
            {
                if (pair.Key.Type == StatusEffectType.Slow)
                {
                    slowMultiplier = Mathf.Min(slowMultiplier, pair.Key.Magnitude);
                }
                else if (pair.Key.Type == StatusEffectType.Stun)
                {
                    stunned = true;
                }
            }

            SetStunned(stunned);

            if (_movement != null)
            {
                _movement.SetStatusSpeedMultiplier(stunned ? 0f : Mathf.Max(MinimumSpeedMultiplier, slowMultiplier));
            }
        }

        private void SetStunned(bool stunned)
        {
            if (IsStunned == stunned)
            {
                return;
            }

            IsStunned = stunned;
            _debugIsStunned = stunned;
            FreezeAnimator(stunned);

            if (!stunned)
            {
                _procBlockedUntil = _clock + _pendingProcCooldown;
            }
        }

        /// <summary>Holds the pose while stunned. Only ever restores the speed it saved itself, and only if it is the
        /// one that froze it - so it never fights EnemyAnimatorSpeed's stride multiplier or the boss-intro hold.</summary>
        private void FreezeAnimator(bool freeze)
        {
            if (freeze)
            {
                if (_animatorFrozen)
                {
                    return;
                }

                if (_animator == null)
                {
                    _animator = GetComponentInChildren<Animator>();
                }

                if (_animator != null)
                {
                    _animatorSpeedBeforeStun = _animator.speed;
                    _animator.speed = 0f;
                    _animatorFrozen = true;
                }

                return;
            }

            if (_animatorFrozen && _animator != null)
            {
                _animator.speed = _animatorSpeedBeforeStun;
            }

            _animatorFrozen = false;
        }

        // A dead enemy's status visuals go straight away: self-animating ones break up quickly (the stun crystals
        // shatter), plain particles are cleared - whatever killed it plays its own effect on top. The Animator is
        // released at once so the death animation plays even if the enemy died mid-stun.
        private void HandleDied()
        {
            FreezeAnimator(false);
            _meltingVisuals.Clear();
            foreach (KeyValuePair<StatusEffectDefinition, AttachedVisual> pair in _attachedVisuals)
            {
                if (pair.Value.Custom != null)
                {
                    pair.Value.Custom.PlayExpire();
                }
                else
                {
                    Stop(pair.Value.Systems, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }

        private void ShowVisual(StatusEffectDefinition definition, float duration)
        {
            if (definition.AttachedVfxPrefab == null)
            {
                return;
            }

            if (!_attachedVisuals.TryGetValue(definition, out AttachedVisual visual))
            {
                GameObject instance = Instantiate(definition.AttachedVfxPrefab, transform);
                instance.transform.localRotation = Quaternion.identity;
                visual = new AttachedVisual { Custom = instance.GetComponent<IStatusEffectVisual>() };
                if (visual.Custom != null)
                {
                    // It sizes and places itself on the body.
                    instance.transform.localPosition = Vector3.zero;
                    instance.transform.localScale = Vector3.one;
                    visual.Custom.Attach(transform);
                }
                else
                {
                    FitToBody(instance.transform);
                    visual.Systems = instance.GetComponentsInChildren<ParticleSystem>(true);
                }

                _attachedVisuals[definition] = visual;
            }

            if (visual.Custom != null)
            {
                visual.Custom.PlayApply(duration);
                return;
            }

            ParticleSystem[] systems = visual.Systems;

            // Hit again while the ice is still melting: start a fresh block rather than letting the old one vanish.
            if (_meltingVisuals.Remove(definition))
            {
                Stop(systems, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            for (int i = 0; i < systems.Length; i++)
            {
                // isPlaying, not isEmitting: a one-shot ice block has finished emitting but must not be re-emitted
                // on every further hit while its pieces are still standing.
                if (systems[i] != null && !systems[i].isPlaying)
                {
                    systems[i].Play(false);
                }
            }
        }

        /// <summary>Stops emitting and lets whatever is on the enemy shrink away over MeltDuration, however long it
        /// was meant to live (the ice block's pieces live "forever" until told to go).</summary>
        private void MeltVisual(StatusEffectDefinition definition)
        {
            if (!_attachedVisuals.TryGetValue(definition, out AttachedVisual visual))
            {
                return;
            }

            if (visual.Custom != null)
            {
                visual.Custom.PlayExpire();
                return;
            }

            ParticleSystem[] systems = visual.Systems;
            _meltingVisuals.Add(definition);
            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem system = systems[i];
                if (system == null)
                {
                    continue;
                }

                system.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                int count = system.particleCount;
                if (count == 0)
                {
                    continue;
                }

                if (_particleBuffer == null || _particleBuffer.Length < count)
                {
                    _particleBuffer = new ParticleSystem.Particle[Mathf.Max(count, 16)];
                }

                count = system.GetParticles(_particleBuffer, count);
                for (int p = 0; p < count; p++)
                {
                    if (_particleBuffer[p].remainingLifetime > MeltDuration)
                    {
                        _particleBuffer[p].remainingLifetime = MeltDuration;
                    }
                }

                system.SetParticles(_particleBuffer, count);
            }
        }

        private void HideAllVisuals(ParticleSystemStopBehavior behavior)
        {
            _meltingVisuals.Clear();
            foreach (KeyValuePair<StatusEffectDefinition, AttachedVisual> pair in _attachedVisuals)
            {
                if (pair.Value.Custom != null)
                {
                    pair.Value.Custom.ResetVisual();
                }
                else
                {
                    Stop(pair.Value.Systems, behavior);
                }
            }
        }

        private static void Stop(ParticleSystem[] systems, ParticleSystemStopBehavior behavior)
        {
            if (systems == null)
            {
                return;
            }

            for (int i = 0; i < systems.Length; i++)
            {
                if (systems[i] != null)
                {
                    systems[i].Stop(false, behavior);
                }
            }
        }

        /// <summary>Plain particle VFX are authored around an enemy AuthoredBodyHeight tall standing on its origin. Plant
        /// the instance under this enemy's actual body and scale it to its height, so a Boss gets a bigger effect than
        /// a Runner. (Self-animating visuals fit themselves instead.)</summary>
        private void FitToBody(Transform instance)
        {
            Renderer body = GetComponentInChildren<SkinnedMeshRenderer>();
            if (body == null)
            {
                instance.localPosition = Vector3.zero;
                return;
            }

            Bounds bounds = body.bounds;
            instance.position = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);

            float scale = Mathf.Clamp(bounds.size.y / AuthoredBodyHeight, 0.5f, 4f);
            Vector3 parentScale = transform.lossyScale;
            instance.localScale = new Vector3(
                scale / Mathf.Max(0.0001f, parentScale.x),
                scale / Mathf.Max(0.0001f, parentScale.y),
                scale / Mathf.Max(0.0001f, parentScale.z));
        }
    }
}
