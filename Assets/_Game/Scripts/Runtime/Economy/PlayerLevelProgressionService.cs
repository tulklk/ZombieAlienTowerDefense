using System;
using UnityEngine;

namespace AlienDefense.Economy
{
    /// <summary>Owns the player's in-match Experience/Level. Experience increases ONLY through
    /// EnergyCollectionService, i.e. only when an EnergyPickup is actually tractor-beamed into the UFO — never
    /// from Enemy capture, never from Environment Prop absorption, never from Tower kills directly.</summary>
    public sealed class PlayerLevelProgressionService
    {
        // Placeholder curve (flat XP-per-level), same spirit as LevelCompletedResult's placeholder star formula:
        // simple and clearly labeled so it's easy to replace with a real curve later without touching callers.
        private const int ExperiencePerLevelConst = 20;

        public int CurrentExperience { get; private set; }
        public int CurrentLevel { get; private set; } = 1;

        /// <summary>Exposed so UI (WaveHUDView's repurposed Level bar) can show "X/ExperiencePerLevel" without
        /// duplicating this class's own leveling curve.</summary>
        public int ExperiencePerLevel => ExperiencePerLevelConst;

        public event Action<int> ExperienceChanged;
        public event Action<int> LevelChanged;

        /// <summary>(xpIntoCurrentLevel, xpNeededForNextLevel, normalized 0-1) - the single place this project
        /// computes "how close to the next Level", so UI never re-derives the curve itself.</summary>
        public (int currentInLevel, int neededForLevel, float normalized) GetProgressInCurrentLevel()
        {
            int xpAtLevelStart = (CurrentLevel - 1) * ExperiencePerLevel;
            int currentInLevel = CurrentExperience - xpAtLevelStart;
            float normalized = ExperiencePerLevel > 0 ? (float)currentInLevel / ExperiencePerLevel : 0f;
            return (currentInLevel, ExperiencePerLevel, normalized);
        }

        /// <summary>Intended caller: EnergyCollectionService only.</summary>
        public void AddExperience(int amount)
        {
            if (amount <= 0)
            {
                Debug.LogWarning($"[PlayerLevelProgressionService] Ignored AddExperience({amount}); amount must be positive.");
                return;
            }

            CurrentExperience += amount;
            ExperienceChanged?.Invoke(CurrentExperience);

            int newLevel = 1 + CurrentExperience / ExperiencePerLevel;
            if (newLevel != CurrentLevel)
            {
                CurrentLevel = newLevel;
                LevelChanged?.Invoke(CurrentLevel);
            }
        }
    }
}
