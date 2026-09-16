using System.Collections.Generic;
using System.Reflection;
using AlienDefense.Enemies;
using AlienDefense.Waves;
using NUnit.Framework;
using UnityEngine;

namespace AlienDefense.Tests.EditMode
{
    public class WaveSpawnCompositionTests
    {
        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, fieldName);
            field.SetValue(target, value);
        }

        private static EnemyDefinition CreateDefinition(string id, EnemyArchetype archetype, float healthFactor, float speedFactor)
        {
            var definition = ScriptableObject.CreateInstance<EnemyDefinition>();
            SetField(definition, "_id", id);
            SetField(definition, "_displayName", id);
            SetField(definition, "_archetype", archetype);
            SetField(definition, "_healthScaleFactor", healthFactor);
            SetField(definition, "_speedScaleFactor", speedFactor);
            SetField(definition, "_damageScaleFactor", 1f);
            SetField(definition, "_maxHealth", 100f);
            SetField(definition, "_moveSpeed", 2f);
            return definition;
        }

        private static EnemySpawnEntry CreateEntry(EnemyDefinition definition, int count)
        {
            var entry = new EnemySpawnEntry();
            SetField(entry, "_enemyDefinition", definition);
            SetField(entry, "_count", count);
            return entry;
        }

        [Test]
        public void BuildSpawnQueue_Sequential_PreservesBlocks()
        {
            EnemyDefinition normal = CreateDefinition("n", EnemyArchetype.Normal, 1f, 1f);
            EnemyDefinition fast = CreateDefinition("f", EnemyArchetype.Fast, 0.8f, 1f);

            var group = new EnemySpawnGroup();
            SetField(group, "_entries", new[] { CreateEntry(normal, 3), CreateEntry(fast, 2) });
            SetField(group, "_interleaveEntries", false);

            List<EnemyDefinition> queue = group.BuildSpawnQueue();
            Assert.AreEqual(5, queue.Count);
            Assert.AreSame(normal, queue[0]);
            Assert.AreSame(normal, queue[1]);
            Assert.AreSame(normal, queue[2]);
            Assert.AreSame(fast, queue[3]);
            Assert.AreSame(fast, queue[4]);

            Object.DestroyImmediate(normal);
            Object.DestroyImmediate(fast);
        }

        [Test]
        public void BuildSpawnQueue_Interleave_RespectsTankStreakLimit()
        {
            EnemyDefinition normal = CreateDefinition("n", EnemyArchetype.Normal, 1f, 1f);
            EnemyDefinition tank = CreateDefinition("t", EnemyArchetype.Tank, 1.25f, 0.25f);

            var group = new EnemySpawnGroup();
            SetField(group, "_entries", new[] { CreateEntry(tank, 3), CreateEntry(normal, 3) });
            SetField(group, "_interleaveEntries", true);

            List<EnemyDefinition> queue = group.BuildSpawnQueue();
            Assert.AreEqual(6, queue.Count);

            int tankStreak = 0;
            int maxTankStreak = 0;
            for (int i = 0; i < queue.Count; i++)
            {
                if (queue[i] == tank)
                {
                    tankStreak++;
                    if (tankStreak > maxTankStreak)
                    {
                        maxTankStreak = tankStreak;
                    }
                }
                else
                {
                    tankStreak = 0;
                }
            }

            Assert.LessOrEqual(maxTankStreak, 1);

            Object.DestroyImmediate(normal);
            Object.DestroyImmediate(tank);
        }

        [Test]
        public void LegacyFields_MigrateToEntries()
        {
            EnemyDefinition normal = CreateDefinition("n", EnemyArchetype.Normal, 1f, 1f);
            var group = new EnemySpawnGroup();
            SetField(group, "_enemyDefinition", normal);
            SetField(group, "_count", 4);
            SetField(group, "_entries", null);

            Assert.AreEqual(4, group.Count);
            Assert.AreEqual(4, group.BuildSpawnQueue().Count);

            Object.DestroyImmediate(normal);
        }

        [Test]
        public void ComputeModifiers_AppliesArchetypeFactorsAndSpeedClamp()
        {
            var go = new GameObject("WaveControllerTest");
            var controller = go.AddComponent<WaveController>();

            var settings = WaveSpawnSettings.CreateDefault();
            settings.HealthPerWaveStep = 0.10f;
            settings.SpeedPerWaveStep = 0.015f;
            settings.MaxSpeedMultiplier = 1.22f;
            SetField(controller, "_spawnSettings", settings);

            EnemyDefinition tank = CreateDefinition("t", EnemyArchetype.Tank, 1.25f, 0.25f);
            EnemySpawnModifiers mods = controller.ComputeModifiers(5, tank);

            // baseHp = 1.4, final = 1 + 0.4*1.25 = 1.5
            Assert.AreEqual(1.5f, mods.HealthMultiplier, 0.0001f);
            // baseSpd = 1.06, final = 1 + 0.06*0.25 = 1.015
            Assert.AreEqual(1.015f, mods.SpeedMultiplier, 0.0001f);

            Object.DestroyImmediate(tank);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void ComputeModifiers_AppliesLevelStrengthOnTopOfWaveGrowth()
        {
            var go = new GameObject("WaveControllerTest");
            var controller = go.AddComponent<WaveController>();

            var settings = WaveSpawnSettings.CreateDefault();
            settings.HealthPerWaveStep = 0.10f;
            settings.LevelHealthMultiplier = 0.5f;
            settings.LevelSpeedMultiplier = 0.8f;
            settings.LevelDamageMultiplier = 0f; // unset -> treated as 1
            SetField(controller, "_spawnSettings", settings);

            EnemySpawnModifiers first = controller.ComputeModifiers(1, null);
            Assert.AreEqual(0.5f, first.HealthMultiplier, 0.0001f);
            Assert.AreEqual(0.8f, first.SpeedMultiplier, 0.0001f);
            Assert.AreEqual(1f, first.DamageMultiplier, 0.0001f);

            // Wave 3: baseHp = 1.2, scaled by the level's 0.5.
            Assert.AreEqual(0.6f, controller.ComputeModifiers(3, null).HealthMultiplier, 0.0001f);

            Object.DestroyImmediate(go);
        }
    }
}
