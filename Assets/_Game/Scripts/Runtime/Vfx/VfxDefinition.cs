using UnityEngine;

namespace AlienDefense.Vfx
{
    /// <summary>Config-only description of a one-shot pooled VFX: prefab, lifetime, pool sizing.</summary>
    [CreateAssetMenu(fileName = "VfxDefinition", menuName = "AlienDefense/Vfx/Vfx Definition")]
    public sealed class VfxDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField]
        private string _id = "vfx_hit";

        [SerializeField]
        private PooledVfx _prefab;

        [Header("Lifetime")]
        [SerializeField, Min(0.05f)]
        private float _lifetime = 1f;

        [Header("Pool")]
        [SerializeField, Min(0)]
        private int _poolPrewarmCount = 4;

        [SerializeField, Min(0)]
        private int _poolDefaultCapacity = 8;

        [SerializeField, Min(0)]
        private int _poolMaximumSize = 32;

        public string Id => _id;
        public PooledVfx Prefab => _prefab;
        public float Lifetime => _lifetime;
        public int PoolPrewarmCount => _poolPrewarmCount;
        public int PoolDefaultCapacity => _poolDefaultCapacity;
        public int PoolMaximumSize => _poolMaximumSize;

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(_id))
            {
                Debug.LogError($"[VfxDefinition] '{name}' has an empty Id.", this);
            }

            if (_prefab == null)
            {
                Debug.LogError($"[VfxDefinition] '{name}' has no Prefab assigned.", this);
            }

            if (_lifetime <= 0f)
            {
                _lifetime = 0.05f;
            }

            if (_poolPrewarmCount < 0)
            {
                _poolPrewarmCount = 0;
            }

            if (_poolDefaultCapacity < _poolPrewarmCount)
            {
                Debug.LogError($"[VfxDefinition] '{name}': Pool Default Capacity ({_poolDefaultCapacity}) must be >= Pool Prewarm Count ({_poolPrewarmCount}).", this);
            }

            if (_poolMaximumSize < _poolDefaultCapacity)
            {
                Debug.LogError($"[VfxDefinition] '{name}': Pool Maximum Size ({_poolMaximumSize}) must be >= Pool Default Capacity ({_poolDefaultCapacity}).", this);
            }
        }
    }
}
