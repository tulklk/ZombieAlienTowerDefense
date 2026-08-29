using UnityEngine;

namespace AlienDefense.Combat
{
    /// <summary>Config-only description of one status effect: type, duration, magnitude, stacking, visual hook.</summary>
    [CreateAssetMenu(fileName = "StatusEffectDefinition", menuName = "AlienDefense/Combat/Status Effect Definition")]
    public sealed class StatusEffectDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField]
        private string _id = "status_slow";

        [SerializeField]
        private string _displayName = "Slow";

        [SerializeField]
        private Sprite _icon;

        [Header("Behavior")]
        [SerializeField]
        private StatusEffectType _type = StatusEffectType.Slow;

        [SerializeField, Min(0.01f)]
        private float _duration = 3f;

        [Tooltip("Slow: speed multiplier applied while active (e.g. 0.5 = 50% speed). Burn: damage per tick per stack.")]
        [SerializeField]
        private float _magnitude = 0.5f;

        [SerializeField, Min(0.01f)]
        private float _tickInterval = 1f;

        [SerializeField]
        private StatusStackingRule _stackingRule = StatusStackingRule.RefreshDurationOnly;

        [SerializeField, Min(1)]
        private int _maxStacks = 1;

        [Header("Visual Hook (optional)")]
        [SerializeField]
        private Color _tintColor = Color.white;

        public string Id => _id;
        public string DisplayName => _displayName;
        public Sprite Icon => _icon;
        public StatusEffectType Type => _type;
        public float Duration => _duration;
        public float Magnitude => _magnitude;
        public float TickInterval => _tickInterval;
        public StatusStackingRule StackingRule => _stackingRule;
        public int MaxStacks => _maxStacks;
        public Color TintColor => _tintColor;

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(_id))
            {
                Debug.LogError($"[StatusEffectDefinition] '{name}' has an empty Id.", this);
            }

            if (_duration <= 0f)
            {
                _duration = 0.01f;
            }

            if (_tickInterval <= 0f)
            {
                _tickInterval = 0.01f;
            }

            if (_maxStacks < 1)
            {
                _maxStacks = 1;
            }

            if (_stackingRule == StatusStackingRule.RefreshDurationOnly && _maxStacks != 1)
            {
                _maxStacks = 1;
            }
        }
    }
}
