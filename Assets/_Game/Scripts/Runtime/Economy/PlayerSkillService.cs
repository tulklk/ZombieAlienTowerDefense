using System;
using System.Collections.Generic;
using AlienDefense.Data;

namespace AlienDefense.Economy
{
    /// <summary>Owns the player's in-match rank (0 = not picked yet, 1-5 = star count) for each of the 5 fixed
    /// SkillType skills. Ranks only ever go up, one at a time, via Upgrade - called exactly by whichever UI
    /// presents the "pick 1 of 3" level-up offer (see GetRandomOffer). Never applies any gameplay effect itself
    /// - see PlayerSkillEffectApplier, which listens to SkillRankChanged and pushes the new rank's stats onto
    /// the relevant systems (UFOTractorBeamController, EnergyWalletService, ...).</summary>
    public sealed class PlayerSkillService
    {
        private readonly Dictionary<SkillType, SkillDefinition> _catalog = new Dictionary<SkillType, SkillDefinition>();
        private readonly Dictionary<SkillType, int> _ranks = new Dictionary<SkillType, int>();
        private readonly Random _random;

        /// <summary>Fired with (skill, newRank) every time Upgrade actually raises a rank.</summary>
        public event Action<SkillType, int> SkillRankChanged;

        public PlayerSkillService(IReadOnlyList<SkillDefinition> catalog, int randomSeed = 0)
        {
            _random = randomSeed != 0 ? new Random(randomSeed) : new Random();

            if (catalog != null)
            {
                foreach (SkillDefinition definition in catalog)
                {
                    if (definition != null)
                    {
                        _catalog[definition.Type] = definition;
                    }
                }
            }
        }

        public SkillDefinition GetDefinition(SkillType type)
        {
            return _catalog.TryGetValue(type, out SkillDefinition definition) ? definition : null;
        }

        /// <summary>0 = never picked. Otherwise the current star count (1-5).</summary>
        public int GetRank(SkillType type)
        {
            return _ranks.TryGetValue(type, out int rank) ? rank : 0;
        }

        public bool IsMaxed(SkillType type)
        {
            SkillDefinition definition = GetDefinition(type);
            return definition != null && GetRank(type) >= definition.MaxRank;
        }

        /// <summary>Raises this skill's rank by exactly 1 (no-op past MaxRank or for an unknown skill) and fires
        /// SkillRankChanged. Intended caller: the level-up choice UI only, after the player picks a card.</summary>
        public void Upgrade(SkillType type)
        {
            SkillDefinition definition = GetDefinition(type);
            if (definition == null)
            {
                return;
            }

            int current = GetRank(type);
            if (current >= definition.MaxRank)
            {
                return;
            }

            int newRank = current + 1;
            _ranks[type] = newRank;
            SkillRankChanged?.Invoke(type, newRank);
        }

        /// <summary>Picks up to `count` distinct skills at random for a level-up offer - prefers skills that
        /// aren't maxed yet; only offers a maxed skill if fewer than `count` non-maxed skills remain.</summary>
        public List<SkillType> GetRandomOffer(int count)
        {
            var notMaxed = new List<SkillType>();
            var all = new List<SkillType>();
            foreach (SkillType type in _catalog.Keys)
            {
                all.Add(type);
                if (!IsMaxed(type))
                {
                    notMaxed.Add(type);
                }
            }

            List<SkillType> pool = notMaxed.Count > 0 ? notMaxed : all;
            Shuffle(pool);

            int take = Math.Min(count, pool.Count);
            return pool.GetRange(0, take);
        }

        private void Shuffle(List<SkillType> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
