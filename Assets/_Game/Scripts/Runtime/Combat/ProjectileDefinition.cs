using AlienDefense.Vfx;
using UnityEngine;

namespace AlienDefense.Combat
{
    /// <summary>Config-only description of a projectile type: movement, lifetime, pool sizing.</summary>
    [CreateAssetMenu(fileName = "ProjectileDefinition", menuName = "AlienDefense/Combat/Projectile Definition")]
    public sealed class ProjectileDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField]
        private string _id = "projectile_blaster";

        [SerializeField]
        private string _displayName = "Blaster Bolt";

        [SerializeField]
        private ProjectileController _prefab;

        [Header("Movement")]
        [SerializeField, Min(0.01f)]
        private float _speed = 10f;

        [SerializeField, Min(0.01f)]
        private float _maximumLifetime = 5f;

        [SerializeField, Min(0.01f)]
        private float _hitDistance = 0.25f;

        [SerializeField, Min(0f)]
        [Tooltip("0 = flies straight at the target. Above 0 = lobbed: climbs to this height (metres) above the " +
            "straight line at mid-flight, then drops onto the target, still tracking it.")]
        private float _arcHeight;

        [Header("VFX (optional)")]
        [SerializeField]
        private VfxDefinition _hitVfxDefinition;

        [SerializeField]
        [Tooltip("Optional. Replaces the enemy's own defeat effect when this projectile's hit is the killing blow " +
            "(e.g. the Frost Tower's ice lance shattering the enemy it kills).")]
        private VfxDefinition _killVfxDefinition;

        [SerializeField]
        [Tooltip("Floating damage number shown over each enemy this projectile (or its splash) actually damages.")]
        private DamagePopupStyle _damagePopupStyle = DamagePopupStyle.None;

        [Header("Pool")]
        [SerializeField, Min(0)]
        private int _poolPrewarmCount = 20;

        [SerializeField, Min(0)]
        private int _poolDefaultCapacity = 40;

        [SerializeField, Min(0)]
        private int _poolMaximumSize = 200;

        public string Id => _id;
        public string DisplayName => _displayName;
        public ProjectileController Prefab => _prefab;
        public float Speed => _speed;
        public float MaximumLifetime => _maximumLifetime;
        public float HitDistance => _hitDistance;
        public float ArcHeight => _arcHeight;
        public VfxDefinition HitVfxDefinition => _hitVfxDefinition;
        public VfxDefinition KillVfxDefinition => _killVfxDefinition;
        public DamagePopupStyle DamagePopupStyle => _damagePopupStyle;
        public int PoolPrewarmCount => _poolPrewarmCount;
        public int PoolDefaultCapacity => _poolDefaultCapacity;
        public int PoolMaximumSize => _poolMaximumSize;

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(_id))
            {
                Debug.LogError($"[ProjectileDefinition] '{name}' has an empty Id.", this);
            }

            if (_prefab == null)
            {
                Debug.LogError($"[ProjectileDefinition] '{name}' has no Prefab assigned.", this);
            }

            if (_speed <= 0f)
            {
                _speed = 0.01f;
            }

            if (_maximumLifetime <= 0f)
            {
                _maximumLifetime = 0.01f;
            }

            if (_hitDistance <= 0f)
            {
                _hitDistance = 0.01f;
            }

            if (_poolPrewarmCount < 0)
            {
                _poolPrewarmCount = 0;
            }

            if (_poolDefaultCapacity < _poolPrewarmCount)
            {
                Debug.LogError($"[ProjectileDefinition] '{name}': Pool Default Capacity ({_poolDefaultCapacity}) must be >= Pool Prewarm Count ({_poolPrewarmCount}).", this);
            }

            if (_poolMaximumSize < _poolDefaultCapacity)
            {
                Debug.LogError($"[ProjectileDefinition] '{name}': Pool Maximum Size ({_poolMaximumSize}) must be >= Pool Default Capacity ({_poolDefaultCapacity}).", this);
            }
        }
    }
}
