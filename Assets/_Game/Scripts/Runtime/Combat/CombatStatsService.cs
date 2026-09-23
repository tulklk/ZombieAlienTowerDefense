using System;
using System.Collections.Generic;
using AlienDefense.Enemies;
using AlienDefense.Towers;
using UnityEngine;

namespace AlienDefense.Combat
{
    /// <summary>Totals how much damage each attacker has dealt this match, for the pause panel's damage leaders.
    /// Read-only bookkeeping: nothing here feeds back into combat.
    ///
    /// Damage is grouped by tower type (all Blasters count as one line) rather than by individual tower, so selling
    /// and rebuilding does not scatter a player's contribution across rows. The UFO's own attacks are one more line.</summary>
    public sealed class CombatStatsService : IDisposable
    {
        /// <summary>One line of the leaderboard.</summary>
        public readonly struct Contributor
        {
            public readonly string Name;
            public readonly Sprite Icon;
            public readonly float Damage;

            /// <summary>The tower type behind this line; null for the UFO and anything that is not a tower.</summary>
            public readonly TowerDefinition Tower;

            public Contributor(string name, Sprite icon, float damage, TowerDefinition tower = null)
            {
                Name = name;
                Icon = icon;
                Damage = damage;
                Tower = tower;
            }
        }

        private sealed class Entry
        {
            public string Name;
            public Sprite Icon;
            public float Damage;
            public TowerDefinition Tower;
        }

        private readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>();
        private readonly Dictionary<int, string> _sourceKeys = new Dictionary<int, string>();
        private readonly List<Contributor> _leaders = new List<Contributor>();
        private readonly string _playerName;
        private bool _disposed;

        public float TotalDamage { get; private set; }

        public CombatStatsService(string playerName = "UFO")
        {
            _playerName = playerName;
            EnemyHealth.DamageApplied += HandleDamageApplied;
        }

        /// <summary>The biggest contributors first, at most <paramref name="max"/> of them.</summary>
        public IReadOnlyList<Contributor> GetLeaders(int max)
        {
            _leaders.Clear();
            foreach (Entry entry in _entries.Values)
            {
                _leaders.Add(new Contributor(entry.Name, entry.Icon, entry.Damage, entry.Tower));
            }

            _leaders.Sort((a, b) => b.Damage.CompareTo(a.Damage));
            if (max > 0 && _leaders.Count > max)
            {
                _leaders.RemoveRange(max, _leaders.Count - max);
            }

            return _leaders;
        }

        /// <summary>Starts a fresh match: every total goes back to zero. Called when a level (or a restart of it)
        /// is built - never when the victory panel opens, which only reads the finished numbers.</summary>
        public void BeginRun()
        {
            Reset();
        }

        public void Reset()
        {
            _entries.Clear();
            _sourceKeys.Clear();
            TotalDamage = 0f;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            EnemyHealth.DamageApplied -= HandleDamageApplied;
        }

        private void HandleDamageApplied(GameObject source, float amount)
        {
            RegisterDamage(source, amount);
        }

        /// <summary>Adds damage that actually came off an enemy's health (EnemyHealth reports it after clamping,
        /// so overkill never counts). Normally fed by EnemyHealth.DamageApplied.</summary>
        public void RegisterDamage(GameObject source, float amount)
        {
            if (source == null || amount <= 0f)
            {
                return;
            }

            TotalDamage += amount;

            // Resolving the attacker walks a few components, so the answer is cached per source instance - towers
            // fire thousands of times per match.
            int id = source.GetInstanceID();
            if (!_sourceKeys.TryGetValue(id, out string key))
            {
                Resolve(source, out key, out string name, out Sprite icon, out TowerDefinition tower);
                _sourceKeys[id] = key;
                if (!_entries.ContainsKey(key))
                {
                    _entries[key] = new Entry { Name = name, Icon = icon, Tower = tower };
                }
            }

            _entries[key].Damage += amount;
        }

        private void Resolve(GameObject source, out string key, out string name, out Sprite icon, out TowerDefinition tower)
        {
            tower = null;
            var towerController = source.GetComponentInParent<TowerController>();
            if (towerController != null && towerController.Definition != null)
            {
                key = "tower:" + towerController.Definition.name;
                name = string.IsNullOrEmpty(towerController.Definition.DisplayName) ? towerController.Definition.name : towerController.Definition.DisplayName;
                icon = towerController.Definition.Icon;
                tower = towerController.Definition;
                return;
            }

            if (source.GetComponentInParent<AlienDefense.Player.PlayerController>() != null)
            {
                key = "player";
                name = _playerName;
                icon = null;
                return;
            }

            key = "other:" + source.name;
            name = source.name;
            icon = null;
        }
    }
}
