using System;
using UnityEngine;

namespace AlienDefense.Data
{
    /// <summary>One numbered rank (1-5, "star") of a SkillDefinition. Only the fields relevant to that skill's
    /// Type are meaningful for any given entry (e.g. Capacity is only read for the Capacity skill) - kept as one
    /// flat struct rather than 5 separate skill subclasses because the level-up UI needs to show any of the 5
    /// skills' current/next rank generically, and this stays simple to author/tune as a plain Inspector array.</summary>
    [Serializable]
    public struct SkillRankData
    {
        [TextArea]
        [Tooltip("Vietnamese description shown on the level-up card, e.g. 'Tăng bán kính hút 150%'.")]
        public string Description;

        [Tooltip("Radius skill only: full multiplier applied to UFOTractorBeamController's AttractionRadius (1.0 = unchanged, 2.0 = double).")]
        public float RadiusMultiplier;

        [Tooltip("Speed skill only: added to the UFO's base move speed as +X% (0.10 = +10%).")]
        public float SpeedBonusPercent;

        [Tooltip("Capacity skill only: EnergyWalletService's max Energy at this rank.")]
        public int CapacityMax;

        [Tooltip("Missile skill only: damage multiplier for the auto-fired missile.")]
        public float MissileDamageMultiplier;

        [Tooltip("Missile skill only: splash-radius multiplier (only rank 5's 'tăng phạm vi' actually raises this above 1).")]
        public float MissileRangeMultiplier;

        [Tooltip("Magnet skill only: additional +X% stacked on top of the Radius skill's own AttractionRadius multiplier.")]
        public float MagnetRadiusBonusPercent;

        [Tooltip("Magnet skill only: additional +X% stacked on top of the beam's pull speed.")]
        public float MagnetPullSpeedBonusPercent;
    }

    /// <summary>Config-only description of one of the 5 fixed player skills and its 5 ranks ("stars") - see the
    /// level-up spec: on every PlayerLevelProgressionService.LevelChanged, the player is offered 3 random
    /// skills (from PlayerSkillCatalog) to pick one from; picking raises that skill's current rank by 1 (capped
    /// at 5) and PlayerSkillEffectApplier re-applies every skill's effect to the relevant gameplay system.</summary>
    [CreateAssetMenu(fileName = "SkillDefinition", menuName = "AlienDefense/Player/Skill Definition")]
    public sealed class SkillDefinition : ScriptableObject
    {
        [SerializeField]
        private SkillType _type;

        [SerializeField]
        private string _displayName;

        [SerializeField]
        [Tooltip("Optional - shown on the level-up card.")]
        private Sprite _icon;

        [SerializeField]
        [Tooltip("Exactly 5 entries, rank 1 (index 0) through rank 5 (index 4).")]
        private SkillRankData[] _ranks = new SkillRankData[5];

        public SkillType Type => _type;
        public string DisplayName => _displayName;
        public Sprite Icon => _icon;
        public int MaxRank => _ranks?.Length ?? 0;

        /// <param name="rank">1-based (rank 1 = first star). Clamped into range.</param>
        public SkillRankData GetRank(int rank)
        {
            if (_ranks == null || _ranks.Length == 0)
            {
                return default;
            }

            int index = Mathf.Clamp(rank - 1, 0, _ranks.Length - 1);
            return _ranks[index];
        }
    }
}
