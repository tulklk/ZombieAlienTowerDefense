using AlienDefense.Combat;
using AlienDefense.Vfx;
using UnityEngine;

namespace AlienDefense.Towers
{
    /// <summary>Config-only description of a tower type: identity, cost, targeting default, and per-level stats.</summary>
    [CreateAssetMenu(fileName = "TowerDefinition", menuName = "AlienDefense/Towers/Tower Definition")]
    public sealed class TowerDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField]
        private string _id = "tower_blaster";

        [SerializeField]
        private string _displayName = "Tower";

        [SerializeField]
        private Sprite _icon;

        [SerializeField]
        private TowerController _prefab;

        [Header("Economy (used by later phases)")]
        [SerializeField, Min(0)]
        private int _buildCost = 75;

        [SerializeField, Range(0f, 1f)]
        private float _sellPercentage = 0.5f;

        [Header("Combat")]
        [SerializeField]
        private TargetingMode _defaultTargetingMode = TargetingMode.First;

        [SerializeField]
        private ProjectileDefinition _projectileDefinition;

        [Header("VFX (optional)")]
        [SerializeField]
        private VfxDefinition _muzzleVfxDefinition;

        [Header("Levels")]
        [Tooltip("Index 0 is Level 1. UpgradeCost at index N is the cost to reach it from N-1.")]
        [SerializeField]
        private TowerLevelData[] _levels;

        public string Id => _id;
        public string DisplayName => _displayName;
        public Sprite Icon => _icon;
        public TowerController Prefab => _prefab;
        public int BuildCost => _buildCost;
        public float SellPercentage => _sellPercentage;
        public TargetingMode DefaultTargetingMode => _defaultTargetingMode;
        public ProjectileDefinition ProjectileDefinition => _projectileDefinition;
        public VfxDefinition MuzzleVfxDefinition => _muzzleVfxDefinition;
        public int LevelCount => _levels?.Length ?? 0;

        public TowerLevelData GetLevel(int index)
        {
            return _levels[index];
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(_id))
            {
                Debug.LogError($"[TowerDefinition] '{name}' has an empty Id.", this);
            }

            if (_prefab == null)
            {
                Debug.LogError($"[TowerDefinition] '{name}' has no Prefab assigned.", this);
            }

            if (_buildCost < 0)
            {
                _buildCost = 0;
            }

            if (_projectileDefinition == null)
            {
                Debug.LogError($"[TowerDefinition] '{name}' has no Projectile Definition assigned.", this);
            }

            if (_levels == null || _levels.Length == 0)
            {
                Debug.LogError($"[TowerDefinition] '{name}' has no levels.", this);
                return;
            }

            for (int i = 0; i < _levels.Length; i++)
            {
                TowerLevelData level = _levels[i];
                if (level == null || !level.IsValid)
                {
                    Debug.LogError($"[TowerDefinition] '{name}' has an invalid level at index {i}.", this);
                }
            }
        }
    }
}
