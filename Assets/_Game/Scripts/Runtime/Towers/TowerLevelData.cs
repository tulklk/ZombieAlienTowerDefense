using System;
using UnityEngine;

namespace AlienDefense.Towers
{
    /// <summary>Config for one tower level. Index 0 is Level 1; UpgradeCost at index N is the cost to reach it from N-1.</summary>
    [Serializable]
    public sealed class TowerLevelData
    {
        [SerializeField, Min(0)]
        [Tooltip("Cost to reach this level from the previous one. Index 0 (Level 1) may be 0.")]
        private int _upgradeCost;

        [SerializeField, Min(0)]
        [Tooltip("Energy Ball cost to reach this level via the UFO proximity build/upgrade flow (separate " +
            "currency from _upgradeCost's Economy resource - see EnergyTowerTransactionService).")]
        private int _energyCost;

        [SerializeField, Min(0.01f)]
        private float _damage = 10f;

        [SerializeField, Min(0.01f)]
        private float _range = 4f;

        [SerializeField, Min(0.01f)]
        private float _attacksPerSecond = 1f;

        [SerializeField, Min(0f)]
        private float _turretRotationSpeed = 360f;

        public int UpgradeCost => _upgradeCost;
        public int EnergyCost => _energyCost;
        public float Damage => _damage;
        public float Range => _range;
        public float AttacksPerSecond => _attacksPerSecond;
        public float TurretRotationSpeed => _turretRotationSpeed;

        public bool IsValid => _damage > 0f && _range > 0f && _attacksPerSecond > 0f && _turretRotationSpeed >= 0f && _upgradeCost >= 0;
    }
}
