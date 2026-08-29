using UnityEngine;

namespace AlienDefense.Enemies
{
    /// <summary>Config-only description of boss-specific behavior layered on top of a normal EnemyDefinition.</summary>
    [CreateAssetMenu(fileName = "BossBehaviorDefinition", menuName = "AlienDefense/Enemies/Boss Behavior Definition")]
    public sealed class BossBehaviorDefinition : ScriptableObject
    {
        [Header("Phase Transition")]
        [SerializeField, Range(0.01f, 0.99f)]
        private float _phaseTwoHealthThreshold = 0.5f;

        [Header("Minions")]
        [SerializeField]
        private EnemyDefinition _minionDefinition;

        [SerializeField, Min(1)]
        private int _minionCountPerBurst = 2;

        [SerializeField, Min(0.5f)]
        private float _minionSpawnInterval = 8f;

        [SerializeField, Range(0.1f, 1f)]
        private float _phaseTwoMinionIntervalMultiplier = 0.6f;

        [Header("Phase Modifiers")]
        [SerializeField, Min(0.01f)]
        private float _phaseTwoSpeedMultiplier = 1.4f;

        [SerializeField, Range(0f, 0.95f)]
        private float _phaseOneDamageReductionPercent = 0.1f;

        [SerializeField, Range(0f, 0.95f)]
        private float _phaseTwoDamageReductionPercent = 0.3f;

        public float PhaseTwoHealthThreshold => _phaseTwoHealthThreshold;
        public EnemyDefinition MinionDefinition => _minionDefinition;
        public int MinionCountPerBurst => _minionCountPerBurst;
        public float MinionSpawnInterval => _minionSpawnInterval;
        public float PhaseTwoMinionIntervalMultiplier => _phaseTwoMinionIntervalMultiplier;
        public float PhaseTwoSpeedMultiplier => _phaseTwoSpeedMultiplier;
        public float PhaseOneDamageReductionPercent => _phaseOneDamageReductionPercent;
        public float PhaseTwoDamageReductionPercent => _phaseTwoDamageReductionPercent;

        private void OnValidate()
        {
            if (_minionDefinition == null)
            {
                Debug.LogError($"[BossBehaviorDefinition] '{name}' has no Minion Definition assigned.", this);
            }
        }
    }
}
