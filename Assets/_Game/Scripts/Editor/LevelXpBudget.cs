using System.Collections.Generic;
using AlienDefense.Data;
using AlienDefense.Economy;
using AlienDefense.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AlienDefense.EditorTools
{
    /// <summary>How many absorbable props a level needs so the player can max every skill from the tractor beam
    /// alone (user, 2026-10-11). Every level-up offers one pick and every pick raises one skill by one star, so
    /// maxing all skills = (sum of every SkillDefinition's MaxRank) level-ups; the XP for that comes straight from
    /// PlayerLevelProgressionService's curve. The level then gets props worth that XP times Headroom (nobody hoovers
    /// up every last prop in a timed run). Enemy kill drops are a bonus on top and are not counted.
    ///
    /// XP per prop stays inside TractorAbsorbableProp's designed 1-2 range: small things 1, anything at least
    /// MediumSize big (it takes a grown beam or a bigger bite of the run) 2. Only props the beam can ever take
    /// (size up to the fully upgraded beam radius) count towards the budget.</summary>
    public static class LevelXpBudget
    {
        public const float Headroom = 1.3f;
        public const float MediumSize = 0.45f; // about 0.9 m across: a crate, a barrel, a life ring (was 0.75 until the tidy plot layout)
        private const string SkillFolder = "Assets/_Game/Data/Skills";
        private const string BeamPath = "Assets/_Game/Data/Player/UFO_TractorBeam.asset";

        /// <summary>Level-ups needed to max every skill.</summary>
        public static int LevelUpsToMaxSkills()
        {
            int total = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:SkillDefinition", new[] { SkillFolder }))
            {
                var skill = AssetDatabase.LoadAssetAtPath<SkillDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (skill != null)
                {
                    total += skill.MaxRank;
                }
            }

            return total;
        }

        /// <summary>XP from level 1 (0 XP) to level 1 + LevelUpsToMaxSkills.</summary>
        public static int RequiredXp()
        {
            int xp = 0;
            int levelUps = LevelUpsToMaxSkills();
            for (int level = 1; level <= levelUps; level++)
            {
                xp += PlayerLevelProgressionService.ExperienceRequiredForLevel(level);
            }

            return xp;
        }

        public static int TargetXp()
        {
            return Mathf.CeilToInt(RequiredXp() * Headroom);
        }

        /// <summary>Largest prop the beam can take once Radius and Magnet are maxed.</summary>
        public static float MaxReachableSize()
        {
            var beam = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(BeamPath));
            float radius = beam.FindProperty("_attractionRadius").floatValue;
            float perRadius = beam.FindProperty("_maxPropSizePerRadius").floatValue;
            float radiusMultiplier = 1f, magnet = 0f;
            foreach (string guid in AssetDatabase.FindAssets("t:SkillDefinition", new[] { SkillFolder }))
            {
                var skill = AssetDatabase.LoadAssetAtPath<SkillDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (skill == null)
                {
                    continue;
                }

                SkillRankData top = skill.GetRank(skill.MaxRank);
                if (skill.Type == SkillType.Radius)
                {
                    radiusMultiplier = Mathf.Max(1f, top.RadiusMultiplier);
                }
                else if (skill.Type == SkillType.Magnet)
                {
                    magnet = top.MagnetRadiusBonusPercent;
                }
            }

            return radius * radiusMultiplier * (1f + magnet) * (perRadius > 0f ? perRadius : 1f);
        }

        /// <summary>Same measure as TractorAbsorbableProp.MeasureSize (larger of footprint radius and 40% height).</summary>
        public static float SizeOf(TractorAbsorbableProp prop)
        {
            var so = new SerializedObject(prop);
            float overrideSize = so.FindProperty("_absorbSizeOverride").floatValue;
            if (overrideSize > 0f)
            {
                return overrideSize;
            }

            bool has = false;
            Bounds bounds = default;
            foreach (Renderer renderer in prop.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is ParticleSystemRenderer || renderer is TrailRenderer)
                {
                    continue;
                }

                if (has)
                {
                    bounds.Encapsulate(renderer.bounds);
                }
                else
                {
                    bounds = renderer.bounds;
                    has = true;
                }
            }

            return has ? Mathf.Max(Mathf.Max(bounds.extents.x, bounds.extents.z), bounds.size.y * 0.4f) : 0f;
        }

        public static int XpFor(float size)
        {
            return size >= MediumSize ? 2 : 1;
        }

        public struct Tally
        {
            public int Props, Reachable, Xp, Small, Medium, TooBig;

            public override string ToString()
            {
                return Props + " absorbable props (" + Reachable + " within the maxed beam: " + Small + " small x1 XP, " + Medium
                    + " medium x2 XP; " + TooBig + " too big ever) = " + Xp + " XP";
            }
        }

        /// <summary>Sets every prop's XP by the size rule (instance values) and counts the reachable XP.</summary>
        public static Tally ApplyAndCount(IEnumerable<TractorAbsorbableProp> props)
        {
            float maxSize = MaxReachableSize();
            var tally = new Tally();
            foreach (TractorAbsorbableProp prop in props)
            {
                tally.Props++;
                float size = SizeOf(prop);
                int xp = XpFor(size);
                var so = new SerializedObject(prop);
                SerializedProperty reward = so.FindProperty("_experienceReward");
                if (reward.intValue != xp)
                {
                    reward.intValue = xp;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                if (size > maxSize)
                {
                    tally.TooBig++;
                    continue;
                }

                tally.Reachable++;
                tally.Xp += xp;
                if (xp == 2)
                {
                    tally.Medium++;
                }
                else
                {
                    tally.Small++;
                }
            }

            return tally;
        }

        [MenuItem("Tools/Tower Defense/Levels/Report XP Budget (Level 2 + 3)")]
        public static void ReportFromMenu()
        {
            Debug.Log(Report());
        }

        public static string Report()
        {
            var lines = new List<string>
            {
                "[LevelXpBudget] Max all skills = " + LevelUpsToMaxSkills() + " level-ups = " + RequiredXp() + " XP; target with x" + Headroom
                + " headroom = " + TargetXp() + " XP; biggest prop the maxed beam takes = " + MaxReachableSize().ToString("0.00") + " m.",
            };
            foreach (string path in new[] { "Assets/_Game/Scenes/Levels/Level_02.unity", "Assets/_Game/Scenes/Levels/Level_03.unity" })
            {
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var props = Object.FindObjectsByType<TractorAbsorbableProp>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                var bounds = Object.FindFirstObjectByType<AlienDefense.Common.LevelBounds>();
                lines.Add(System.IO.Path.GetFileNameWithoutExtension(path) + ": " + Count(props) + "; playable "
                    + (bounds != null ? (bounds.Extents.x * 2f).ToString("0") + " x " + (bounds.Extents.y * 2f).ToString("0") + " m" : "?"));
            }

            return string.Join("\n", lines);
        }

        /// <summary>The <paramref name="take"/> most repeated props as "name xCount" - the variety check (user,
        /// 2026-10-12: no single object should turn up over and over).</summary>
        public static string TopKinds(IEnumerable<TractorAbsorbableProp> props, int take)
        {
            var counts = new Dictionary<string, int>();
            int total = 0;
            foreach (TractorAbsorbableProp prop in props)
            {
                string name = prop.name;
                int cut = name.IndexOf(" (");
                if (cut > 0)
                {
                    name = name.Substring(0, cut);
                }

                counts[name] = counts.TryGetValue(name, out int n) ? n + 1 : 1;
                total++;
            }

            var sorted = new List<KeyValuePair<string, int>>(counts);
            sorted.Sort((a, b) => b.Value.CompareTo(a.Value));
            var parts = new List<string>();
            for (int i = 0; i < Mathf.Min(take, sorted.Count); i++)
            {
                parts.Add(sorted[i].Key + " x" + sorted[i].Value + " (" + (sorted[i].Value * 100 / Mathf.Max(1, total)) + "%)");
            }

            return counts.Count + " kinds; " + string.Join(", ", parts);
        }

        /// <summary>Counts without changing anything.</summary>
        public static Tally Count(IEnumerable<TractorAbsorbableProp> props)
        {
            float maxSize = MaxReachableSize();
            var tally = new Tally();
            foreach (TractorAbsorbableProp prop in props)
            {
                tally.Props++;
                float size = SizeOf(prop);
                if (size > maxSize)
                {
                    tally.TooBig++;
                    continue;
                }

                int xp = new SerializedObject(prop).FindProperty("_experienceReward").intValue;
                tally.Reachable++;
                tally.Xp += xp;
                if (xp >= 2)
                {
                    tally.Medium++;
                }
                else
                {
                    tally.Small++;
                }
            }

            return tally;
        }
    }
}
