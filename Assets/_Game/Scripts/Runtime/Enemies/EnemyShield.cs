using System;
using UnityEngine;

namespace AlienDefense.Enemies
{
    /// <summary>Optional shield layer that absorbs damage before it reaches EnemyHealth, with delayed regen.</summary>
    public sealed class EnemyShield : MonoBehaviour
    {
        [SerializeField, Min(0f)]
        private float _maxShield = 40f;

        [SerializeField, Min(0f)]
        private float _regenPerSecond = 4f;

        [SerializeField, Min(0f)]
        private float _regenDelayAfterHit = 3f;

        private float _regenDelayTimer;

        public float CurrentShield { get; private set; }
        public float MaxShield => _maxShield;
        public bool HasShield => CurrentShield > 0f;

        public event Action<float, float> ShieldChanged;

        private void Awake()
        {
            ResetState();
        }

        /// <summary>Called by EnemyController on Initialize/pool reuse to restore a full shield.</summary>
        public void ResetState()
        {
            CurrentShield = _maxShield;
            _regenDelayTimer = 0f;
            ShieldChanged?.Invoke(CurrentShield, _maxShield);
        }

        /// <summary>Absorbs as much of the incoming damage as the shield can. Returns the leftover for EnemyHealth.</summary>
        public float Absorb(float incomingDamage)
        {
            if (incomingDamage <= 0f || CurrentShield <= 0f)
            {
                return incomingDamage;
            }

            float absorbed = Mathf.Min(CurrentShield, incomingDamage);
            CurrentShield -= absorbed;
            _regenDelayTimer = _regenDelayAfterHit;
            ShieldChanged?.Invoke(CurrentShield, _maxShield);

            return incomingDamage - absorbed;
        }

        private void Update()
        {
            if (CurrentShield >= _maxShield || _maxShield <= 0f)
            {
                return;
            }

            if (_regenDelayTimer > 0f)
            {
                _regenDelayTimer -= Time.deltaTime;
                return;
            }

            CurrentShield = Mathf.Min(_maxShield, CurrentShield + _regenPerSecond * Time.deltaTime);
            ShieldChanged?.Invoke(CurrentShield, _maxShield);
        }
    }
}
