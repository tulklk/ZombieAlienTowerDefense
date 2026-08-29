using System.Collections.Generic;
using AlienDefense.Combat;
using UnityEngine;

namespace AlienDefense.Enemies
{
    /// <summary>Owns every active status effect on one enemy: apply/refresh/stack/tick/remove. Never grants rewards or resolves the enemy.</summary>
    public sealed class EnemyStatusController : MonoBehaviour, IStatusApplicable
    {
        private const float MinimumSpeedMultiplier = 0.2f;

        [SerializeField]
        private EnemyMovement _movement;

        [SerializeField]
        private EnemyHealth _health;

        private readonly Dictionary<StatusEffectDefinition, StatusEffectInstance> _activeEffects =
            new Dictionary<StatusEffectDefinition, StatusEffectInstance>();

        private readonly List<StatusEffectDefinition> _expiredBuffer = new List<StatusEffectDefinition>();

        private GameObject _lastStatusSource;

        /// <summary>Called by EnemyController on Initialize and on pool reuse.</summary>
        public void Clear()
        {
            _activeEffects.Clear();
            _lastStatusSource = null;
            _movement?.SetStatusSpeedMultiplier(1f);
        }

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
                _activeEffects.Add(definition, new StatusEffectInstance(definition));
            }

            RefreshSpeedMultiplier();
        }

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

        private void RefreshSpeedMultiplier()
        {
            if (_movement == null)
            {
                return;
            }

            float multiplier = 1f;
            foreach (KeyValuePair<StatusEffectDefinition, StatusEffectInstance> pair in _activeEffects)
            {
                if (pair.Key.Type != StatusEffectType.Slow)
                {
                    continue;
                }

                multiplier = Mathf.Min(multiplier, pair.Key.Magnitude);
            }

            _movement.SetStatusSpeedMultiplier(Mathf.Max(MinimumSpeedMultiplier, multiplier));
        }
    }
}
