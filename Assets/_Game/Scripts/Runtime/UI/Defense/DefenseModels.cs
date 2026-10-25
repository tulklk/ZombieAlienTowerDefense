using System;
using System.Collections.Generic;
using System.Globalization;
using AlienDefense.Combat;
using AlienDefense.Towers;
using AlienDefense.UI.MainMenu;
using UnityEngine;

namespace AlienDefense.UI.Defense
{
    /// <summary>Every string the Defense tab shows, in one place. The project has no localization system yet; when
    /// one arrives these constants become its keys and nothing in the views has to change.</summary>
    public static class DefenseStrings
    {
        public const string Title = "Tower Collection";
        public const string CritDamage = "Towers Crit Damage";
        public const string CritInfo = "Every tower shot has a {0} chance to be a critical hit. A critical hit deals {1} damage: {2} base, +{3} for every permanent tower level you own.";
        public const string UpgradeCards = "Upgrade Cards";
        public const string GetCards = "Get Cards";
        public const string CoreStats = "Core Stats";
        public const string FullStats = "Full Stats";
        public const string About = "About";
        public const string AboutTower = "About tower";
        public const string LevelUp = "Level Up";
        public const string Unlock = "Unlock";
        public const string MaxLevel = "MAX LEVEL";
        public const string Max = "MAX";
        public const string LevelFormat = "Lvl. {0}";
        public const string UpgradeBonus = "Upgrade bonus";
        public const string Locked = "Locked";
        public const string NotEnoughCoins = "Not enough Coins";
        public const string NotEnoughCards = "Not enough Upgrade Cards";
        public const string Health = "Health";
        public const string HealthBonus = "Health bonus";
        public const string ShotDamage = "Shot damage";
        public const string AttackInterval = "Attack interval";
        public const string FireRange = "Fire Range";
        public const string DamageType = "Damage type";
        public const string Target = "Target";
        public const string Burst = "Burst";
        public const string AreaRadius = "Area radius";
        public const string SlowPower = "Slow";
        public const string SlowDuration = "Slow duration";
        public const string FreezeAfter = "Freeze after";
        public const string FreezeDuration = "Freeze duration";
        public const string CritChance = "Critical chance";
        public const string CritDamageShort = "Critical damage";
        public const string TipDamageType = "The kind of damage this tower deals.";
        public const string TipTarget = "Which enemy in range this tower picks: First = closest to your base, Nearest = closest to the tower, Strongest = most health.";
        public const string TipShotDamage = "Damage of one attack when the tower is first built in a level. Permanent levels raise it; upgrading the tower inside a level raises it further.";
        public const string TipBurst = "How many barrels fire in one attack. Barrels that fire together share the shot damage.";
        public const string TipFreeze = "Hits needed on the same enemy before it freezes solid.";
        public const string TipBaseHealth = "Base health in every level. Each Base level adds {0} of the level's own value.";
    }

    /// <summary>One line of a stat panel, already formatted for display.</summary>
    public struct DefenseStat
    {
        public Sprite Icon;
        public string Label;
        public string Value;
        public string Delta;
        public string Tooltip;
    }

    /// <summary>Everything the Defense tab shows about one entry (a tower or the UFO Base), read from the real
    /// definitions and progression services. Rebuilt whenever something changes; the views only display it.</summary>
    public sealed class DefenseEntry
    {
        public string Id;
        public string Title;
        public Sprite Icon;
        public Sprite TypeIcon;
        public bool IsBase;
        public bool IsUnlocked;
        public int UnlockCost;
        public int LevelIndex;
        public bool IsMaxLevel;
        public Sprite CardIcon;
        public int OwnedCoins, RequiredCoins, OwnedCards, RequiredCards;
        public MetaUpgradeResult UpgradeCheck;
        public float UpgradeBonus;
        public string About;
        public readonly List<DefenseStat> Core = new List<DefenseStat>();
        public readonly List<DefenseStat> Full = new List<DefenseStat>();
        /// <summary>Model shown in the 3D showcase (a visual-only branch). Null = the 2D icon is shown instead.</summary>
        public GameObject PreviewModel;
        public TowerDefinition Tower;
    }

    /// <summary>Stat icons the builder hands out (assigned in the Inspector by DefenseTabBuilder).</summary>
    [Serializable]
    public sealed class DefenseStatIcons
    {
        public Sprite Damage;
        public Sprite Interval;
        public Sprite Range;
        public Sprite Target;
        public Sprite Burst;
        public Sprite Area;
        public Sprite Freeze;
        public Sprite Crit;
        public Sprite Health;
        public Sprite Bonus;
    }

    /// <summary>Turns tower / base data into display stats. No tower is special-cased by name: what a tower shows
    /// follows from its TowerDefinition (attack behaviour, status effect, splash radius, levels).</summary>
    public static class DefenseStatBuilder
    {
        public static string Number(float value)
        {
            if (value >= 1000f)
            {
                return CurrencyFormatter.Format((long)Mathf.Round(value));
            }

            return Mathf.Abs(value - Mathf.Round(value)) < 0.005f
                ? Mathf.RoundToInt(value).ToString(CultureInfo.InvariantCulture)
                : value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        public static string Percent(float fraction)
        {
            return Number(fraction * 100f) + "%";
        }

        public static string Seconds(float seconds)
        {
            return Number(seconds) + "s";
        }

        private static string Delta(float current, float next, Func<float, string> format, bool lowerIsBetter = false)
        {
            float change = next - current;
            if (Mathf.Abs(change) < 0.005f)
            {
                return null;
            }

            return (change > 0f ? "+" : "-") + format(Mathf.Abs(change));
        }

        public static string TargetName(TargetingMode mode)
        {
            switch (mode)
            {
                case TargetingMode.Closest: return "Nearest";
                case TargetingMode.Strongest: return "Strongest";
                default: return "First";
            }
        }

        /// <summary>Fills Core and Full for a tower at <paramref name="levelIndex"/> (deltas vs the next level, if any).</summary>
        public static void BuildTower(DefenseEntry entry, TowerDefinition tower, int levelIndex, DefenseMetaConfig config,
            float critDamage, DefenseStatIcons icons)
        {
            entry.Core.Clear();
            entry.Full.Clear();
            if (tower == null || tower.LevelCount <= 0)
            {
                return;
            }

            // The permanent level only multiplies shot damage. Everything is quoted for the tower as it is first built
            // in a level (its lowest in-level level); in-level upgrades then raise the stats on top, as always.
            TowerLevelData now = tower.GetLevel(0);
            TowerLevelData next = null; // nothing but damage changes with the permanent level
            float multiplier = config != null ? config.GetTowerDamageMultiplier(levelIndex) : 1f;
            float shot = now.Damage * multiplier;
            bool canGrow = entry.IsUnlocked && !entry.IsMaxLevel && config != null;

            var damage = new DefenseStat
            {
                Icon = icons.Damage, Label = DefenseStrings.ShotDamage, Value = Number(shot),
                Delta = canGrow ? Delta(shot, now.Damage * config.GetTowerDamageMultiplier(levelIndex + 1), Number) : null,
                Tooltip = DefenseStrings.TipShotDamage,
            };
            var interval = new DefenseStat
            {
                Icon = icons.Interval, Label = DefenseStrings.AttackInterval, Value = Seconds(1f / now.AttacksPerSecond),
                Delta = next != null ? Delta(1f / now.AttacksPerSecond, 1f / next.AttacksPerSecond, Seconds) : null,
            };
            var range = new DefenseStat
            {
                Icon = icons.Range, Label = DefenseStrings.FireRange, Value = Number(now.Range),
                Delta = next != null ? Delta(now.Range, next.Range, Number) : null,
            };

            entry.Full.Add(new DefenseStat { Icon = entry.TypeIcon, Label = DefenseStrings.DamageType, Value = tower.DamageTypeLabel, Tooltip = DefenseStrings.TipDamageType });
            entry.Full.Add(new DefenseStat { Icon = icons.Target, Label = DefenseStrings.Target, Value = TargetName(tower.DefaultTargetingMode), Tooltip = DefenseStrings.TipTarget });
            entry.Full.Add(damage);
            entry.Full.Add(interval);

            StatusEffectDefinition status = tower.AttackBehavior == TowerAttackBehavior.Status ? tower.StatusEffectOnHit : null;
            if (tower.AttackBehavior == TowerAttackBehavior.Splash && tower.SplashRadius > 0f)
            {
                entry.Full.Add(new DefenseStat { Icon = icons.Area, Label = DefenseStrings.AreaRadius, Value = Number(tower.SplashRadius) });
            }

            if (status != null)
            {
                if (status.Type == StatusEffectType.Slow)
                {
                    entry.Full.Add(new DefenseStat { Icon = icons.Freeze, Label = DefenseStrings.SlowPower, Value = Percent(status.Magnitude) });
                    entry.Full.Add(new DefenseStat { Icon = icons.Freeze, Label = DefenseStrings.SlowDuration, Value = Seconds(status.Duration) });
                }

                StatusEffectDefinition proc = status.ProcEffect;
                if (proc != null && proc.Type == StatusEffectType.Stun)
                {
                    string hits = status.ProcMinHits == status.ProcMaxHits
                        ? status.ProcMinHits + " hits"
                        : status.ProcMinHits + "-" + status.ProcMaxHits + " hits";
                    entry.Full.Add(new DefenseStat { Icon = icons.Freeze, Label = DefenseStrings.FreezeAfter, Value = hits, Tooltip = DefenseStrings.TipFreeze });
                    entry.Full.Add(new DefenseStat { Icon = icons.Freeze, Label = DefenseStrings.FreezeDuration, Value = Seconds(proc.Duration) });
                }
            }

            entry.Full.Add(new DefenseStat
            {
                Icon = icons.Burst, Label = DefenseStrings.Burst, Value = now.ActiveMuzzles.ToString(CultureInfo.InvariantCulture),
                Delta = next != null && next.ActiveMuzzles != now.ActiveMuzzles ? "+" + (next.ActiveMuzzles - now.ActiveMuzzles) : null,
                Tooltip = DefenseStrings.TipBurst,
            });
            entry.Full.Add(range);
            if (config != null)
            {
                entry.Full.Add(new DefenseStat { Icon = icons.Crit, Label = DefenseStrings.CritChance, Value = Percent(config.CritChance) });
                entry.Full.Add(new DefenseStat { Icon = icons.Crit, Label = DefenseStrings.CritDamageShort, Value = Percent(critDamage) });
            }

            // Core: the two numbers that define the tower. A status tower leads with what its effect does.
            entry.Core.Add(damage);
            if (status != null && status.ProcEffect != null && status.ProcEffect.Type == StatusEffectType.Stun)
            {
                entry.Core.Add(interval);
                entry.Core.Add(new DefenseStat { Icon = icons.Freeze, Label = DefenseStrings.SlowPower, Value = Percent(status.Magnitude) });
                entry.Core.Add(new DefenseStat { Icon = icons.Freeze, Label = DefenseStrings.FreezeDuration, Value = Seconds(status.ProcEffect.Duration) });
            }
            else if (tower.AttackBehavior == TowerAttackBehavior.Splash)
            {
                entry.Core.Add(interval);
            }
            else
            {
                entry.Core.Add(range);
            }
        }

        /// <summary>Fills Core and Full for the UFO Base.</summary>
        public static void BuildBase(DefenseEntry entry, BaseDefenseUpgradeService service, DefenseMetaConfig config,
            int referenceHealth, DefenseStatIcons icons)
        {
            entry.Core.Clear();
            entry.Full.Clear();
            int level = service.CurrentLevelIndex;
            int health = service.ApplyAtLevel(referenceHealth, level);
            string healthDelta = service.IsMaxLevel ? null : "+" + Number(service.ApplyAtLevel(referenceHealth, level + 1) - health);
            float bonus = config.GetBaseHealthMultiplier(level) - 1f;
            float nextBonus = config.GetBaseHealthMultiplier(level + 1) - 1f;

            var healthStat = new DefenseStat
            {
                Icon = icons.Health, Label = DefenseStrings.Health, Value = Number(health), Delta = healthDelta,
                Tooltip = string.Format(CultureInfo.InvariantCulture, DefenseStrings.TipBaseHealth, Percent(config.BaseHealthBonusPerLevel)),
            };
            var bonusStat = new DefenseStat
            {
                Icon = icons.Bonus, Label = DefenseStrings.HealthBonus, Value = "+" + Percent(bonus),
                Delta = service.IsMaxLevel ? null : "+" + Percent(nextBonus - bonus),
            };
            entry.Core.Add(healthStat);
            entry.Core.Add(bonusStat);
            entry.Full.Add(healthStat);
            entry.Full.Add(bonusStat);
        }
    }
}
