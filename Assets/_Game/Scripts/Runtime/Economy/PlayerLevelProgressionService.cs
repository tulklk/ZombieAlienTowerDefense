using System;
using UnityEngine;

namespace AlienDefense.Economy
{
    /// <summary>Owns the player's in-match Experience/Level. Experience increases through EnergyCollectionService
    /// (an EnergyPickup tractor-beamed into the UFO) and through LevelCompositionRoot's own direct call on
    /// UFOTractorBeamController.PropAbsorbed (a decorative Environment Prop - tree, rock, mushroom... - absorbed;
    /// see TractorAbsorbableProp.ExperienceReward for why that bypasses EnergyCollectionService, since props never
    /// touch the Energy wallet) — never from Enemy capture, never from Tower kills directly.</summary>
    public sealed class PlayerLevelProgressionService
    {
        // Progressive curve: each level-up costs more XP than the last, instead of the old flat-20-per-level
        // placeholder. Level N -> N+1 costs (BaseExperienceForNextLevel + (N-1) * ExperienceIncrementPerLevel), so
        // Level 1->2 needs the base amount and every level after that needs steadily more (1->2: 30, 2->3: 45,
        // 3->4: 60, ...). Tune these two numbers to retune the whole curve without touching any caller.
        private const int BaseExperienceForNextLevel = 30;
        private const int ExperienceIncrementPerLevel = 15;

        public int CurrentExperience { get; private set; }
        public int CurrentLevel { get; private set; } = 1;

        public event Action<int> ExperienceChanged;
        public event Action<int> LevelChanged;

        /// <summary>XP required to advance from `level` to `level + 1`.</summary>
        public static int ExperienceRequiredForLevel(int level)
        {
            return BaseExperienceForNextLevel + Mathf.Max(0, level - 1) * ExperienceIncrementPerLevel;
        }

        /// <summary>Total cumulative XP needed to REACH `level`, counting from Level 1 at 0 XP.</summary>
        private static int CumulativeExperienceForLevel(int level)
        {
            int total = 0;
            for (int i = 1; i < level; i++)
            {
                total += ExperienceRequiredForLevel(i);
            }

            return total;
        }

        /// <summary>(xpIntoCurrentLevel, xpNeededForNextLevel, normalized 0-1) - the single place this project
        /// computes "how close to the next Level", so UI never re-derives the curve itself.</summary>
        public (int currentInLevel, int neededForLevel, float normalized) GetProgressInCurrentLevel()
        {
            int xpAtLevelStart = CumulativeExperienceForLevel(CurrentLevel);
            int neededForLevel = ExperienceRequiredForLevel(CurrentLevel);
            int currentInLevel = CurrentExperience - xpAtLevelStart;
            float normalized = neededForLevel > 0 ? (float)currentInLevel / neededForLevel : 0f;
            return (currentInLevel, neededForLevel, normalized);
        }

        /// <summary>Intended callers: EnergyCollectionService (EnergyPickups) and LevelCompositionRoot's
        /// HandlePropAbsorbedForExperience (decorative Environment Props) only.</summary>
        public void AddExperience(int amount)
        {
            if (amount <= 0)
            {
                Debug.LogWarning($"[PlayerLevelProgressionService] Ignored AddExperience({amount}); amount must be positive.");
                return;
            }

            CurrentExperience += amount;
            ExperienceChanged?.Invoke(CurrentExperience);

            int startingLevel = CurrentLevel;
            while (CurrentExperience >= CumulativeExperienceForLevel(CurrentLevel + 1))
            {
                CurrentLevel++;
            }

            if (CurrentLevel != startingLevel)
            {
                LevelChanged?.Invoke(CurrentLevel);
            }
        }
    }
}
