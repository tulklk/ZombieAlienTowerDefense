using UnityEngine;

namespace AlienDefense.Combat
{
    /// <summary>Config-only description of one status effect: type, duration, magnitude, stacking, visual hooks.</summary>
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

        [Tooltip("Slow: speed multiplier applied while active (e.g. 0.5 = 50% speed). Burn: damage per tick per stack. " +
            "Stun: unused (movement is simply stopped).")]
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

        [SerializeField]
        [Tooltip("Optional. Effect attached to an affected enemy for as long as this effect lasts (e.g. ice crystals " +
            "around a stunned enemy). Instantiated once per enemy and reused. A root implementing IStatusEffectVisual " +
            "animates and fits itself; plain particle prefabs are authored around a 1.8 m body and melt away at the end.")]
        private GameObject _attachedVfxPrefab;

        [Header("Proc (optional) - repeated hits build up to a second effect")]
        [SerializeField]
        [Tooltip("Optional. After a random number of hits of THIS effect on the same enemy (counted per enemy, from " +
            "any source), that enemy also gets this effect - e.g. Frost slow hits building up to an Ice Stun. Hits " +
            "landing while the enemy is stunned, or within Proc Cooldown after, do not count.")]
        private StatusEffectDefinition _procEffect;

        [SerializeField, Min(1)]
        private int _procMinHits = 3;

        [SerializeField, Min(1)]
        [Tooltip("Inclusive: each time, the hits needed are rolled between Min and Max.")]
        private int _procMaxHits = 6;

        [SerializeField, Min(0f)]
        [Tooltip("Seconds after a stun wears off during which hits are not counted, so stuns cannot chain back to back.")]
        private float _procCooldown = 0.3f;

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
        public GameObject AttachedVfxPrefab => _attachedVfxPrefab;
        public StatusEffectDefinition ProcEffect => _procEffect;
        public int ProcMinHits => _procMinHits;
        public int ProcMaxHits => Mathf.Max(_procMinHits, _procMaxHits);
        public float ProcCooldown => _procCooldown;

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

            if (_procMaxHits < _procMinHits)
            {
                _procMaxHits = _procMinHits;
            }

            if (_procEffect == this)
            {
                Debug.LogError($"[StatusEffectDefinition] '{name}' cannot proc itself.", this);
                _procEffect = null;
            }
        }
    }
}
