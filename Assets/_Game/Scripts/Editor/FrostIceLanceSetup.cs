using AlienDefense.Combat;
using AlienDefense.Towers;
using AlienDefense.Vfx;
using UnityEditor;
using UnityEngine;

namespace AlienDefense.EditorTools
{
    /// <summary>Rebuilds the Frost Tower's attack out of Assets/_Game/VFX/IceLance.prefab (copied, never modified):
    /// an ice-lance projectile leaving a MistTrail, a small spray of ice fragments on every hit (with the Slow), and a
    /// TinyShards shatter when the lance kills. The Slow builds up per enemy to the Ice Stun (StatusEffect_IceStun):
    /// crystals burst out of the ground around the enemy (IceStunVisual) and hold it still. Safe to re-run; the Slow's
    /// and the Stun's own tuning (durations, magnitude, hits needed) is left as it is on the assets.</summary>
    internal static class FrostIceLanceSetup
    {
        private const string SourcePath = "Assets/_Game/VFX/IceLance.prefab";
        private const string ProjectilePrefabPath = "Assets/_Game/Prefabs/Projectiles/Projectile_IceLance.prefab";
        private const string ProjectileDefinitionPath = "Assets/_Game/Data/Projectiles/Projectile_IceLance.asset";
        private const string HitPrefabPath = "Assets/_Game/Prefabs/Vfx/Vfx_IceHit.prefab";
        private const string HitDefinitionPath = "Assets/_Game/Data/Vfx/VfxDefinition_Vfx_IceHit.asset";
        private const string ShatterPrefabPath = "Assets/_Game/Prefabs/Vfx/Vfx_IceShatter.prefab";
        private const string ShatterDefinitionPath = "Assets/_Game/Data/Vfx/VfxDefinition_Vfx_IceShatter.asset";
        private const string OldEncasePrefabPath = "Assets/_Game/Prefabs/Vfx/Vfx_FrostEncase.prefab";
        private const string IceStunPrefabPath = "Assets/_Game/Prefabs/Vfx/Vfx_IceStun.prefab";
        private const string IceStunStatusPath = "Assets/_Game/Data/StatusEffects/StatusEffect_IceStun.asset";
        private const string FrostTowerPath = "Assets/_Game/Data/Towers/TowerDefinition_Tower_Frost.asset";
        private const string SlowStatusPath = "Assets/_Game/Data/StatusEffects/StatusEffect_Slow.asset";
        private const string IceMaterialPath = "Assets/UnityTechnologies/ParticlePack/EffectExamples/Magic Effects/Materials/Ice.mat";
        private const string IceMistMaterialPath = "Assets/UnityTechnologies/ParticlePack/EffectExamples/Magic Effects/Materials/IceMist.mat";
        private const string IceCrystalMaterialPath = "Assets/_Game/Materials/VFX/Ice/MAT_IceCrystal.mat";

        [MenuItem("AlienDefense/Setup/Towers/Setup Frost Tower Ice Lance")]
        private static void Run()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath) == null)
            {
                Debug.LogError("[FrostIceLanceSetup] Missing " + SourcePath);
                return;
            }

            VfxDefinition hit = BuildHitVfx();
            VfxDefinition shatter = BuildShatterVfx();
            StatusEffectDefinition iceStun = BuildIceStunStatus(BuildIceStunVfx());
            ProjectileDefinition projectile = BuildProjectile(hit, shatter);

            var tower = AssetDatabase.LoadAssetAtPath<TowerDefinition>(FrostTowerPath);
            var towerSo = new SerializedObject(tower);
            towerSo.FindProperty("_projectileDefinition").objectReferenceValue = projectile;
            towerSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(tower);

            // The Frost slow itself carries no visual any more (just the hit shards); it builds up to the Ice Stun.
            var slow = AssetDatabase.LoadAssetAtPath<StatusEffectDefinition>(SlowStatusPath);
            var slowSo = new SerializedObject(slow);
            slowSo.FindProperty("_attachedVfxPrefab").objectReferenceValue = null;
            slowSo.FindProperty("_procEffect").objectReferenceValue = iceStun;
            slowSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(slow);

            // The old single ice-block visual, replaced by the Ice Stun crystals.
            if (AssetDatabase.LoadAssetAtPath<GameObject>(OldEncasePrefabPath) != null)
            {
                AssetDatabase.DeleteAsset(OldEncasePrefabPath);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[FrostIceLanceSetup] Frost Tower: ice lances, small shard hits + slow, random-hit Ice Stun crystals, shatter kills.");
        }

        // ---------------------------------------------------------------------------------------------------------

        private static ProjectileDefinition BuildProjectile(VfxDefinition hit, VfxDefinition shatter)
        {
            var root = new GameObject("Projectile_IceLance");
            var controller = root.AddComponent<ProjectileController>();
            GameObject source = InstantiateSource();

            // The lance itself: one mesh particle, simulated in the projectile's own space so it flies with it. Its
            // mesh points down -X, so a 90 degree yaw lays the tip along +Z, the direction the projectile faces.
            Transform lance = source.transform;
            lance.name = "Lance";
            lance.SetParent(root.transform, false);
            ResetLocal(lance);

            var lancePs = lance.GetComponent<ParticleSystem>();
            ParticleSystem.SubEmittersModule subs = lancePs.subEmitters;
            for (int i = subs.subEmittersCount - 1; i >= 0; i--)
            {
                subs.RemoveSubEmitter(i);
            }

            subs.enabled = false;

            var main = lancePs.main;
            main.loop = false;
            main.prewarm = false;
            main.playOnAwake = false;
            main.duration = 1f;
            main.startDelay = 0f;
            main.startLifetime = 5f;
            main.startSpeed = 0f;
            main.gravityModifier = 0f;
            main.maxParticles = 1;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f); // random roll about its length
            main.startRotationY = Mathf.PI * 0.5f;
            main.startRotationZ = 0f;
            main.startSize3D = true;
            main.startSizeX = 1.6f; // ~1.6 m lance, thick enough to read from the gameplay camera
            main.startSizeY = 2.6f;
            main.startSizeZ = 2.6f;

            var emission = lancePs.emission;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

            var shape = lancePs.shape;
            shape.enabled = false;
            var velocity = lancePs.velocityOverLifetime;
            velocity.enabled = false;
            var collision = lancePs.collision;
            collision.enabled = false;
            var color = lancePs.colorOverLifetime;
            color.enabled = false;
            var trails = lancePs.trails;
            if (trails.enabled)
            {
                trails.worldSpace = true; // a streak behind the flight, not a stub glued to the lance
                trails.lifetime = 0.04f;  // fraction of the particle's 5 s life
            }

            // MistTrail: a free-standing world-space emitter that puffs mist per metre flown.
            Transform mistTrail = FindDeep(root.transform, "MistTrail");
            mistTrail.SetParent(root.transform, false);
            ResetLocal(mistTrail);
            var trailPs = mistTrail.GetComponent<ParticleSystem>();
            var tm = trailPs.main;
            tm.loop = true;
            tm.prewarm = false;
            tm.playOnAwake = false;
            tm.simulationSpace = ParticleSystemSimulationSpace.World;
            tm.scalingMode = ParticleSystemScalingMode.Hierarchy;
            tm.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            tm.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.6f);
            tm.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1f);
            tm.gravityModifier = 0f;
            tm.startColor = new Color(0.85f, 0.95f, 1f, 0.55f);
            tm.maxParticles = 200;
            var te = trailPs.emission;
            te.rateOverTime = 0f;
            te.rateOverDistance = 7f;
            te.SetBursts(new ParticleSystem.Burst[0]);

            DestroyDeep(root.transform, "Mist");
            DestroyDeep(root.transform, "TinyShards");
            DestroyDeep(root.transform, "IceBall");

            var so = new SerializedObject(controller);
            SerializedProperty attached = so.FindProperty("_attachedParticles");
            attached.arraySize = 2;
            attached.GetArrayElementAtIndex(0).objectReferenceValue = lancePs;
            attached.GetArrayElementAtIndex(1).objectReferenceValue = trailPs;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, ProjectilePrefabPath);
            Object.DestroyImmediate(root);

            var definition = LoadOrCreate<ProjectileDefinition>(ProjectileDefinitionPath);
            var dso = new SerializedObject(definition);
            dso.FindProperty("_id").stringValue = "projectile_ice_lance";
            dso.FindProperty("_displayName").stringValue = "Ice Lance";
            dso.FindProperty("_prefab").objectReferenceValue = prefab.GetComponent<ProjectileController>();
            dso.FindProperty("_speed").floatValue = 12f;
            dso.FindProperty("_maximumLifetime").floatValue = 4f;
            dso.FindProperty("_hitDistance").floatValue = 0.3f;
            dso.FindProperty("_hitVfxDefinition").objectReferenceValue = hit;
            dso.FindProperty("_killVfxDefinition").objectReferenceValue = shatter;
            dso.FindProperty("_poolPrewarmCount").intValue = 10;
            dso.FindProperty("_poolDefaultCapacity").intValue = 20;
            dso.FindProperty("_poolMaximumSize").intValue = 80;
            dso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            return definition;
        }

        /// <summary>Every normal Frost hit: a tiny cyan flash and a handful of small ice fragments spraying off the
        /// impact point - the same TinyShards crystals as the kill shatter, just far fewer, smaller and shorter-lived.
        /// No ice on the enemy: the big crystals are the Ice Stun proc's job.</summary>
        private static VfxDefinition BuildHitVfx()
        {
            var root = new GameObject("Vfx_IceHit");

            ParticleSystem shards = TakeSystem(root.transform, "TinyShards");
            var main = shards.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 2.5f);
            main.gravityModifier = 0.6f;
            main.startSize3D = true; // IceShardTiny is ~0.24 m long: this makes ~0.2-0.4 m fragments, readable in play
            main.startSizeX = new ParticleSystem.MinMaxCurve(0.9f, 1.6f);
            main.startSizeY = new ParticleSystem.MinMaxCurve(0.7f, 1.1f);
            main.startSizeZ = new ParticleSystem.MinMaxCurve(0.8f, 1.3f);
            main.startColor = Color.white;
            main.maxParticles = 16;
            // The opaque-ish faceted crystal ice (same as the stun crystals) - the lance's clear ice vanishes at this size.
            shards.GetComponent<ParticleSystemRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(IceCrystalMaterialPath);
            var emission = shards.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 4, 8) });
            var shape = shards.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere; // fragments fly off in every direction
            shape.radius = 0.1f;
            var velocity = shards.velocityOverLifetime;
            velocity.enabled = false;
            var size = shards.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.6f, 1f), new Keyframe(1f, 0f)));

            ParticleSystem flash = TakeSystem(root.transform, "Mist");
            flash.name = "Flash";
            ConfigureMistBurst(flash, 2, new ParticleSystem.MinMaxCurve(0.6f, 0.9f), 0.7f);
            var flashMain = flash.main;
            flashMain.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.2f);
            flashMain.startSpeed = 0f;
            flashMain.gravityModifier = 0f;
            flashMain.startColor = new Color(0.55f, 0.95f, 1f, 0.7f);

            return SavePooled(root, new[] { shards, flash }, HitPrefabPath, HitDefinitionPath, "vfx_ice_hit", 0.7f, 8);
        }

        private static VfxDefinition BuildShatterVfx()
        {
            var root = new GameObject("Vfx_IceShatter");

            ParticleSystem shards = TakeSystem(root.transform, "TinyShards");
            shards.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // the cone sprays up
            var main = shards.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
            main.startSize3D = true; // authored for a close-up; readable shards from the gameplay camera
            main.startSizeX = new ParticleSystem.MinMaxCurve(2.5f, 4f);
            main.startSizeY = new ParticleSystem.MinMaxCurve(1.5f, 2.5f);
            main.startSizeZ = new ParticleSystem.MinMaxCurve(2f, 3.5f);
            main.startColor = Color.white;
            UseClearIce(shards);
            var emission = shards.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 24, 32) });
            var shape = shards.shape;
            shape.radius = 0.35f;

            ParticleSystem mist = TakeSystem(root.transform, "Mist");
            ConfigureMistBurst(mist, 8, new ParticleSystem.MinMaxCurve(0.8f, 1.8f), 0.3f);

            return SavePooled(root, new[] { shards, mist }, ShatterPrefabPath, ShatterDefinitionPath, "vfx_ice_shatter", 1.4f, 6);
        }

        /// <summary>The Ice Stun visual: a prefab holding only IceStunVisual and its two materials. The component builds
        /// the crystals, frost patch, shards and mist itself at runtime and fits them to each enemy it lands on.</summary>
        private static GameObject BuildIceStunVfx()
        {
            var root = new GameObject("Vfx_IceStun");
            var visual = root.AddComponent<IceStunVisual>();
            var so = new SerializedObject(visual);
            so.FindProperty("_crystalMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(IceCrystalMaterialPath);
            so.FindProperty("_mistMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(IceMistMaterialPath);
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, IceStunPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>The Ice Stun effect itself: 2 s of no movement, with the crystals. Only its visual hook is (re)set here
        /// on later runs - its duration is gameplay tuning left to the asset.</summary>
        private static StatusEffectDefinition BuildIceStunStatus(GameObject visual)
        {
            var stun = AssetDatabase.LoadAssetAtPath<StatusEffectDefinition>(IceStunStatusPath);
            bool isNew = stun == null;
            if (isNew)
            {
                stun = ScriptableObject.CreateInstance<StatusEffectDefinition>();
                AssetDatabase.CreateAsset(stun, IceStunStatusPath);
            }

            var so = new SerializedObject(stun);
            if (isNew)
            {
                so.FindProperty("_id").stringValue = "status_ice_stun";
                so.FindProperty("_displayName").stringValue = "Ice Stun";
                so.FindProperty("_type").enumValueIndex = (int)StatusEffectType.Stun;
                so.FindProperty("_duration").floatValue = 2f;
                so.FindProperty("_magnitude").floatValue = 0f;
                so.FindProperty("_stackingRule").enumValueIndex = (int)StatusStackingRule.RefreshDurationOnly;
                so.FindProperty("_maxStacks").intValue = 1;
                so.FindProperty("_tintColor").colorValue = new Color(0.52f, 0.94f, 1f, 1f);
            }

            so.FindProperty("_attachedVfxPrefab").objectReferenceValue = visual;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(stun);
            return stun;
        }

        // ---------------------------------------------------------------------------------------------------------

        private static void ConfigureMistBurst(ParticleSystem mist, short count, ParticleSystem.MinMaxCurve size, float alpha)
        {
            var main = mist.main;
            main.loop = false;
            main.prewarm = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 2.5f);
            main.startSize = size;
            main.gravityModifier = 0.2f;
            main.startColor = new Color(0.85f, 0.95f, 1f, alpha);
            var emission = mist.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, count) });
        }

        /// <summary>The lance's bright translucent ice instead of the shards' own refraction material, which reads as
        /// dark teal specks from the gameplay camera.</summary>
        private static void UseClearIce(ParticleSystem system)
        {
            var ice = AssetDatabase.LoadAssetAtPath<Material>(IceMaterialPath);
            if (ice != null)
            {
                system.GetComponent<ParticleSystemRenderer>().sharedMaterial = ice;
            }
        }

        /// <summary>A fresh, unpacked copy of one of IceLance's systems, moved under <paramref name="parent"/> with
        /// everything else from the copy thrown away.</summary>
        private static ParticleSystem TakeSystem(Transform parent, string name)
        {
            GameObject source = InstantiateSource();
            Transform part = FindDeep(source.transform, name);
            part.SetParent(parent, false);
            ResetLocal(part);
            Object.DestroyImmediate(source);

            var ps = part.GetComponent<ParticleSystem>();
            ParticleSystem.SubEmittersModule subs = ps.subEmitters;
            subs.enabled = false;
            return ps;
        }

        private static GameObject InstantiateSource()
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath));
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            return instance;
        }

        private static VfxDefinition SavePooled(GameObject root, ParticleSystem[] systems, string prefabPath,
            string definitionPath, string id, float lifetime, int prewarm)
        {
            var pooled = root.AddComponent<PooledVfx>();
            var so = new SerializedObject(pooled);
            SerializedProperty array = so.FindProperty("_particleSystems");
            array.arraySize = systems.Length;
            for (int i = 0; i < systems.Length; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = systems[i];
            }

            so.FindProperty("_keepWorldUpright").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);

            var definition = LoadOrCreate<VfxDefinition>(definitionPath);
            var dso = new SerializedObject(definition);
            dso.FindProperty("_id").stringValue = id;
            dso.FindProperty("_prefab").objectReferenceValue = prefab.GetComponent<PooledVfx>();
            dso.FindProperty("_lifetime").floatValue = lifetime;
            dso.FindProperty("_poolPrewarmCount").intValue = prewarm;
            dso.FindProperty("_poolDefaultCapacity").intValue = prewarm * 2;
            dso.FindProperty("_poolMaximumSize").intValue = prewarm * 8;
            dso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }

            return asset;
        }

        private static void ResetLocal(Transform t)
        {
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            foreach (Transform t in parent.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name)
                {
                    return t;
                }
            }

            return null;
        }

        private static void DestroyDeep(Transform parent, string name)
        {
            Transform t = FindDeep(parent, name);
            if (t != null)
            {
                Object.DestroyImmediate(t.gameObject);
            }
        }
    }
}
