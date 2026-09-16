using System;
using AlienDefense.Combat;
using AlienDefense.Vfx;
using UnityEngine;

namespace AlienDefense.Enemies
{
    /// <summary>Owns an enemy's current/maximum health and death notification.</summary>
    public sealed class EnemyHealth : MonoBehaviour, IDamageable
    {
        public float CurrentHealth { get; private set; }
        public float MaximumHealth { get; private set; }
        public bool IsDead => CurrentHealth <= 0f;
        public bool IsDamageable => !IsDead && !_isCaptureImmune && !_isEncounterImmune;

        public event Action<float, float> HealthChanged;
        public event Action Died;

        /// <summary>Raised after damage actually lands, with the attacker's GameObject and the amount taken off this
        /// enemy (after defense/shield, clamped to the health that was left). Static because enemies are pooled: a
        /// level-scoped listener (CombatStatsService) subscribes once instead of re-subscribing per spawn.</summary>
        public static event Action<GameObject, float> DamageApplied;

        /// <summary>Same moment as DamageApplied, with the victim and the full DamageInfo (e.g. its popup style) -
        /// the amount is still the final damage taken, after defense/shield and clamped to remaining health.</summary>
        public static event Action<EnemyHealth, DamageInfo, float> DamageLanded;

        /// <summary>The killing blow's DamageInfo.KillVfx, if it carried one; set just before Died fires.</summary>
        public VfxDefinition KillVfxOverride { get; private set; }

        private bool _diedFired;
        private float _lastAppliedDamage;
        private VfxDefinition _incomingKillVfx;
        private bool _isCaptureImmune;
        private bool _isEncounterImmune;
        private EnemyDefense _defense;
        private EnemyShield _shield;

        public void Initialize(float maximumHealth)
        {
            MaximumHealth = Mathf.Max(1f, maximumHealth);
            ResetState();
        }

        /// <summary>Optional. Wires the sibling defense/shield components consulted by the DamageInfo overload.</summary>
        public void SetDefenseAndShield(EnemyDefense defense, EnemyShield shield)
        {
            _defense = defense;
            _shield = shield;
        }

        /// <summary>Driven by EnemyController while a tractor beam capture is in progress. No damage source may
        /// bypass this — the raw float overload consults it too.</summary>
        public void SetCaptureImmune(bool value)
        {
            _isCaptureImmune = value;
        }

        /// <summary>Driven by EnemyController.SetCombatActive while a boss-intro group is on screen but not yet
        /// released - nothing (tower shot, splash, burn tick) may damage it until the fight actually starts.</summary>
        public void SetEncounterImmune(bool value)
        {
            _isEncounterImmune = value;
        }

        public void ResetState()
        {
            CurrentHealth = MaximumHealth;
            _diedFired = false;
            KillVfxOverride = null;
            _isCaptureImmune = false;
            _isEncounterImmune = false;
            HealthChanged?.Invoke(CurrentHealth, MaximumHealth);
        }

        public bool TryApplyDamage(float amount)
        {
            if (amount <= 0f)
            {
                return false;
            }

            if (!IsDamageable)
            {
                return false;
            }

            float before = CurrentHealth;
            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            _lastAppliedDamage = before - CurrentHealth;
            HealthChanged?.Invoke(CurrentHealth, MaximumHealth);

            if (CurrentHealth <= 0f && !_diedFired)
            {
                _diedFired = true;
                KillVfxOverride = _incomingKillVfx;
                Died?.Invoke();
            }

            return true;
        }

        public bool TryApplyDamage(in DamageInfo damageInfo)
        {
            float amount = damageInfo.Amount;

            if (_defense != null)
            {
                amount = _defense.ModifyIncomingDamage(amount, damageInfo.DamageType);
            }

            if (_shield != null && damageInfo.DamageType != DamageType.True)
            {
                amount = _shield.Absorb(amount);
            }

            _incomingKillVfx = damageInfo.KillVfx;
            bool applied = TryApplyDamage(amount);
            _incomingKillVfx = null;

            if (applied && _lastAppliedDamage > 0f)
            {
                DamageApplied?.Invoke(damageInfo.Source, _lastAppliedDamage);
                DamageLanded?.Invoke(this, damageInfo, _lastAppliedDamage);
            }

            return applied;
        }
    }
}
