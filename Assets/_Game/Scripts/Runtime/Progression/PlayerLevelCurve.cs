using UnityEngine;

namespace AlienDefense.Progression
{
    /// <summary>Where a lifetime XP total puts the player: level, XP into that level, and XP the level needs.</summary>
    public readonly struct PlayerLevelProgress
    {
        public readonly int Level;
        public readonly int XpIntoLevel;

        /// <summary>XP the current level needs to reach the next one; 0 at the max level.</summary>
        public readonly int XpForNextLevel;

        public PlayerLevelProgress(int level, int xpIntoLevel, int xpForNextLevel)
        {
            Level = level;
            XpIntoLevel = xpIntoLevel;
            XpForNextLevel = xpForNextLevel;
        }

        public bool IsMaxLevel => XpForNextLevel <= 0;

        /// <summary>0..1 fill of the XP bar (full at the max level).</summary>
        public float Progress01 => IsMaxLevel ? 1f : Mathf.Clamp01((float)XpIntoLevel / XpForNextLevel);
    }

    /// <summary>The player (account) level curve over the saved lifetime XP (PlayerProfileService.PlayerExperience,
    /// paid by victory rewards). Level 1 needs <c>xpForLevel2</c> to level up, and every level after needs
    /// <c>xpIncreasePerLevel</c> more than the one before - a gentle linear ramp. Pure math, no state.</summary>
    public sealed class PlayerLevelCurve
    {
        private readonly int _xpForLevel2;
        private readonly int _xpIncreasePerLevel;

        public int MaxLevel { get; }

        public PlayerLevelCurve(int xpForLevel2 = 1000, int xpIncreasePerLevel = 500, int maxLevel = 99)
        {
            _xpForLevel2 = Mathf.Max(1, xpForLevel2);
            _xpIncreasePerLevel = Mathf.Max(0, xpIncreasePerLevel);
            MaxLevel = Mathf.Max(1, maxLevel);
        }

        /// <summary>XP needed to go from <paramref name="level"/> to the next level.</summary>
        public int XpToAdvanceFrom(int level)
        {
            if (level >= MaxLevel)
            {
                return 0;
            }

            long value = _xpForLevel2 + (long)_xpIncreasePerLevel * (Mathf.Max(1, level) - 1);
            return (int)System.Math.Min(int.MaxValue, value);
        }

        public PlayerLevelProgress Evaluate(int lifetimeXp)
        {
            int remaining = Mathf.Max(0, lifetimeXp);
            int level = 1;
            while (level < MaxLevel)
            {
                int need = XpToAdvanceFrom(level);
                if (remaining < need)
                {
                    return new PlayerLevelProgress(level, remaining, need);
                }

                remaining -= need;
                level++;
            }

            return new PlayerLevelProgress(MaxLevel, 0, 0);
        }
    }
}
