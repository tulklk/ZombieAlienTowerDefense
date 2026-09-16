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

            public Contributor(string name, Sprite icon, float damage)
            {
                Name = name;
                Icon = icon;
                Damage = damage;
            }
        }

        private sealed class Entry
        {
            public string Name;
            public Sprite Icon;
            public float Damage;
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
                _leaders.Add(new Contributor(entry.Name, entry.Icon, entry.Damage));
            }

            _leaders.Sort((a, b) => b.Damage.CompareTo(a.Damage));
            if (max > 0 && _leaders.Count > max)
            {
                _leaders.RemoveRange(max, _leaders.Count - max);
            }

            return _leaders;
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
                Resolve(source, out key, out string name, out Sprite icon);
                _sourceKeys[id] = key;
                if (!_entries.ContainsKey(key))
                {
                    _entries[key] = new Entry { Name = name, Icon = icon };
                }
            }

            _entries[key].Damage += amount;
        }

        private void Resolve(GameObject source, out string key, out string name, out Sprite icon)
        {
            var tower = source.GetComponentInParent<TowerController>();
            if (tower != null && tower.Definition != null)
            {
                key = "tower:" + tower.Definition.name;
                name = string.IsNullOrEmpty(tower.Definition.DisplayName) ? tower.Definition.name : tower.Definition.DisplayName;
                icon = tower.Definition.Icon;
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
