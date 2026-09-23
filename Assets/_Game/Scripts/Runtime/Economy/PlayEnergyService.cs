using System;
using AlienDefense.Save;

namespace AlienDefense.Economy
{
    /// <summary>The lobby energy spent to start a level ("Start ⚡5"): a capped bar that refills one point every few
    /// real-time minutes, also while the game is closed.
    ///
    /// Nothing ticks. The save holds the amount as of a moment in time (PlayerProfileService's PlayEnergy state), and
    /// the current amount is derived from the clock whenever it is asked for - so there is no per-frame work, no
    /// timer to keep alive across scenes, and a restart loses nothing. Separate from the in-level EnergyWalletService
    /// (the energy the UFO collects during a match), which this never touches.</summary>
    public sealed class PlayEnergyService
    {
        private readonly PlayerProfileService _profile;
        private readonly long _regenTicks;

        public int Max { get; }
        public int CostPerLevel { get; }

        public PlayEnergyService(PlayerProfileService profile, int max, int costPerLevel, float regenMinutes)
        {
            _profile = profile;
            Max = Math.Max(1, max);
            CostPerLevel = Math.Max(0, costPerLevel);
            _regenTicks = Math.Max(TimeSpan.TicksPerSecond, (long)(Math.Max(0.05f, regenMinutes) * TimeSpan.TicksPerMinute));
        }

        public TimeSpan RegenInterval => TimeSpan.FromTicks(_regenTicks);

        public int GetCurrent(DateTime nowUtc)
        {
            Settle(nowUtc, out int amount, out _);
            return amount;
        }

        /// <summary>Time until the next point comes back; zero while the bar is full.</summary>
        public TimeSpan GetTimeToNext(DateTime nowUtc)
        {
            Settle(nowUtc, out int amount, out long since);
            if (amount >= Max)
            {
                return TimeSpan.Zero;
            }

            long remaining = _regenTicks - (nowUtc.Ticks - since);
            return TimeSpan.FromTicks(Math.Max(0L, Math.Min(_regenTicks, remaining)));
        }

        public bool CanAfford(DateTime nowUtc) => GetCurrent(nowUtc) >= CostPerLevel;

        /// <summary>Spends one level's cost, all or nothing, and saves. A full bar starts its refill countdown from
        /// the moment it is spent; a bar that was already refilling keeps the progress toward its next point.</summary>
        public bool TrySpendForLevel(DateTime nowUtc)
        {
            if (_profile == null)
            {
                return true; // no profile (level started outside Bootstrap): nothing to charge
            }

            Settle(nowUtc, out int amount, out long since);
            if (amount < CostPerLevel)
            {
                return false;
            }

            long anchor = amount >= Max ? nowUtc.Ticks : since;
            _profile.SetPlayEnergyState(amount - CostPerLevel, anchor);
            return true;
        }

        /// <summary>The amount right now and the moment the current partial point started accruing. Regained points
        /// are applied here without writing to the save - only a spend persists.</summary>
        private void Settle(DateTime nowUtc, out int amount, out long since)
        {
            if (_profile == null || _profile.PlayEnergyUpdatedUtcTicks <= 0)
            {
                amount = Max; // new profile or an older save: start full
                since = nowUtc.Ticks;
                return;
            }

            amount = Math.Min(Max, _profile.StoredPlayEnergy);
            since = _profile.PlayEnergyUpdatedUtcTicks;
            long elapsed = nowUtc.Ticks - since;
            if (elapsed < 0)
            {
                since = nowUtc.Ticks; // clock moved backwards: restart the countdown rather than refill
                return;
            }

            if (amount >= Max)
            {
                since = nowUtc.Ticks;
                return;
            }

            long gained = elapsed / _regenTicks;
            if (gained <= 0)
            {
                return;
            }

            if (amount + gained >= Max)
            {
                amount = Max;
                since = nowUtc.Ticks;
                return;
            }

            amount += (int)gained;
            since += gained * _regenTicks;
        }
    }
}
