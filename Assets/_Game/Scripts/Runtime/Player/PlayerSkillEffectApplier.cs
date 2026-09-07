using AlienDefense.Data;
using AlienDefense.Economy;
using UnityEngine;

namespace AlienDefense.Player
{
    /// <summary>Listens to PlayerSkillService.SkillRankChanged and is the ONLY class allowed to push each
    /// skill's numeric effect onto the systems it actually changes - UFOTractorBeamController (Radius +
    /// Magnet, both stack into one radius/pull-speed multiplier pair), PlayerMovement (Speed), EnergyWalletService
    /// (Capacity/"Tải"). Missile is read directly by PlayerMissileController at fire time instead (nothing to
    /// "apply" ahead of time there). Recomputes ALL of a target system's contributing skills together on any
    /// change, rather than applying deltas, so ranks can never drift out of sync with what's actually equipped.</summary>
    public sealed class PlayerSkillEffectApplier : MonoBehaviour
    {
        private PlayerSkillService _skills;
        private UFOTractorBeamController _tractorBeam;
        private PlayerMovement _movement;
        private EnergyWalletService _energyWallet;

        public void Initialize(PlayerSkillService skills, UFOTractorBeamController tractorBeam, PlayerMovement movement, EnergyWalletService energyWallet)
        {
            Unsubscribe();

            _skills = skills;
            _tractorBeam = tractorBeam;
            _movement = movement;
            _energyWallet = energyWallet;

            if (_skills != null)
            {
                _skills.SkillRankChanged += HandleSkillRankChanged;
            }

            ApplyAll();
        }

        private void HandleSkillRankChanged(SkillType type, int newRank)
        {
            ApplyAll();
        }

        /// <summary>Recomputes every target system's final numbers from scratch off the current ranks - simple
        /// and always-correct at this small a skill count (5), no need to special-case "which skill changed".</summary>
        private void ApplyAll()
        {
            if (_skills == null)
            {
                return;
            }

            float radiusMultiplier = GetFloatOrDefault(SkillType.Radius, r => r.RadiusMultiplier, 1f);
            float magnetRadiusBonus = GetFloatOrDefault(SkillType.Magnet, r => r.MagnetRadiusBonusPercent, 0f);
            float magnetPullSpeedBonus = GetFloatOrDefault(SkillType.Magnet, r => r.MagnetPullSpeedBonusPercent, 0f);

            if (_tractorBeam != null)
            {
                _tractorBeam.SetSkillMultipliers(radiusMultiplier * (1f + magnetRadiusBonus), 1f + magnetPullSpeedBonus);
            }

            if (_movement != null)
            {
                float speedBonus = GetFloatOrDefault(SkillType.Speed, r => r.SpeedBonusPercent, 0f);
                _movement.SetSpeedMultiplier(1f + speedBonus);
            }

            if (_energyWallet != null)
            {
                int capacity = GetIntOrDefault(SkillType.Capacity, r => r.CapacityMax, _energyWallet.MaxEnergy);
                _energyWallet.SetMaxEnergy(capacity);
            }
        }

        private float GetFloatOrDefault(SkillType type, System.Func<SkillRankData, float> selector, float defaultValue)
        {
            int rank = _skills.GetRank(type);
            if (rank <= 0)
            {
                return defaultValue;
            }

            SkillDefinition definition = _skills.GetDefinition(type);
            if (definition == null)
            {
                return defaultValue;
            }

            return selector(definition.GetRank(rank));
        }

        private int GetIntOrDefault(SkillType type, System.Func<SkillRankData, int> selector, int defaultValue)
        {
            int rank = _skills.GetRank(type);
            if (rank <= 0)
            {
                return defaultValue;
            }

            SkillDefinition definition = _skills.GetDefinition(type);
            if (definition == null)
            {
                return defaultValue;
            }

            return selector(definition.GetRank(rank));
        }

        private void Unsubscribe()
        {
            if (_skills != null)
            {
                _skills.SkillRankChanged -= HandleSkillRankChanged;
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }
    }
}
