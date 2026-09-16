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
        private const string LanceMaterialPath = "Assets/_Game/Materials/VFX/Ice/MAT_IceLance.mat";

        // Flying lance look (IceLance_Low mesh: ~1.02 m along X, ~0.16 m thick).
        private const float SpearMeshLength = 1.02f;
        private const float SpearMeshWidth = 0.156f;
        private const float SpearLength = 2.0f;
        private const float SpearWidth = 0.36f;
        private const float SpearYaw = -90f; // the mesh tip is at +X: this yaw points it along the projectile forward (+Z)
        private const float TrailSeconds = 0.16f;
        private const float TrailWidth = 0.27f;   // 75% of the lance width
        private const float TailOffset = -0.45f;  // just inside the lance's tail (tip forward along +Z)

        private const string TrailMaterialPath = "Assets/_Game/Materials/VFX/Ice/MAT_IceTrail.mat";
        private const string ColdMistMaterialPath = "Assets/_Game/Materials/VFX/Ice/MAT_ColdMist.mat";
        private const string SoftTrailTexturePath = "Assets/_Game/Art/Textures/VFX/T_SoftTrail.png";
        private const string MistTexturePath = "Assets/UnityTechnologies/ParticlePack/EffectExamples/Magic Effects/Textures/DustPuffSmall.png";
        private const string TinyShardModelPath = "Assets/UnityTechnologies/ParticlePack/EffectExamples/Magic Effects/Models/IceShard.FBX";

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

            // The lance itself: one mesh particle, simulated in the projectile's own space so it flies with it; its
            // orientation (tip at +X, yawed onto +Z) is set in ConfigureSpear.
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

            Transform mistTrail = FindDeep(root.transform, "MistTrail");
            mistTrail.SetParent(root.transform, false);
            ResetLocal(mistTrail);

            DestroyDeep(root.transform, "Mist");
            DestroyDeep(root.transform, "TinyShards");
            DestroyDeep(root.transform, "IceBall");

            // Look, size, trail, mist and the VisualRoot hierarchy.
            ApplyProjectileVisual(root);

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

        [MenuItem("AlienDefense/Setup/Towers/Apply Ice Lance Projectile Visual")]
        private static void ApplyProjectileVisualToPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(ProjectilePrefabPath);
            try
            {
                ApplyProjectileVisual(root);
                PrefabUtility.SaveAsPrefabAsset(root, ProjectilePrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            Debug.Log("[FrostIceLanceSetup] Ice lance projectile visual applied to " + ProjectilePrefabPath);
        }

        /// <summary>What the flying lance looks like - visual only, the projectile's gameplay (speed, hit distance,
        /// damage, slow) is untouched. The Particle Pack's Ice material is a Scene Color refraction that is invisible
        /// over the ground from the gameplay camera, so the lance uses AlienDefense/IceLance instead. Behind it, in
        /// falling order of visibility: a short soft cyan TrailRenderer, a faint cold-vapour mist and a few tiny ice
        /// chips. The spear draws on top of all three (renderer priority) so they can never hide it.</summary>
        private static void ApplyProjectileVisual(GameObject root)
        {
            var controller = root.GetComponent<ProjectileController>();

            // ProjectileController turns the root to face the flight direction; the visuals live under their own root.
            Transform visualRoot = root.transform.Find("VisualRoot");
            if (visualRoot == null)
            {
                visualRoot = new GameObject("VisualRoot").transform;
                visualRoot.SetParent(root.transform, false);
            }

            ResetLocal(visualRoot);

            Transform spear = FindDeep(root.transform, "IceSpear") ?? FindDeep(root.transform, "Lance");
            Transform mist = FindDeep(root.transform, "ColdMist") ?? FindDeep(root.transform, "IceMist") ?? FindDeep(root.transform, "MistTrail");
            if (spear == null || mist == null)
            {
                Debug.LogError("[FrostIceLanceSetup] Projectile is missing its lance or mist system.");
                return;
            }

            spear.name = "IceSpear";
            spear.SetParent(visualRoot, false);
            ResetLocal(spear);
            spear.SetSiblingIndex(0);

            // The spear's tail sits ~0.6 m behind the root (tip forward along +Z); trail, mist and chips start just
            // inside it so they visibly leave the lance rather than float behind it.
            Transform trail = FindOrCreateChild(visualRoot, "IceTrail", 1);
            PlaceAtTail(trail, 0f);

            mist.name = "ColdMist";
            mist.SetParent(visualRoot, false);
            mist.SetSiblingIndex(2);
            PlaceAtTail(mist, 180f); // the cone emits backwards, against the flight

            Transform tiny = FindOrCreateChild(visualRoot, "TinyIceParticles", 3);
            PlaceAtTail(tiny, 180f);

            ParticleSystem spearPs = ConfigureSpear(spear.GetComponent<ParticleSystem>());
            TrailRenderer trailRenderer = ConfigureTrail(trail);
            ParticleSystem mistPs = ConfigureColdMist(mist.GetComponent<ParticleSystem>());
            ParticleSystem tinyPs = ConfigureTinyIce(tiny);

            var so = new SerializedObject(controller);
            so.FindProperty("_trail").objectReferenceValue = trailRenderer;
            SerializedProperty attached = so.FindProperty("_attachedParticles");
            attached.arraySize = 3;
            attached.GetArrayElementAtIndex(0).objectReferenceValue = spearPs;
            attached.GetArrayElementAtIndex(1).objectReferenceValue = mistPs;
            attached.GetArrayElementAtIndex(2).objectReferenceValue = tinyPs;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static ParticleSystem ConfigureSpear(ParticleSystem spear)
        {
            // One mesh particle that lives for the whole flight, simulated in the projectile's space (no double
            // movement: start speed 0, the controller moves the root). IceLance_Low is ~1.02 m long along X with a
            // ~0.16 m round body, so the scale below makes a ~2 m x 0.36 m lance (5.5:1) - readable from the ~24 m gameplay camera.
            var main = spear.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = 5f;
            main.startSpeed = 0f;
            main.maxParticles = 1;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startColor = Color.white;
            main.startRotation3D = true;
            main.startRotationX = 0f;
            main.startRotationY = SpearYaw * Mathf.Deg2Rad;
            main.startRotationZ = 0f;
            main.startSize3D = true;
            main.startSizeX = SpearLength / SpearMeshLength;
            main.startSizeY = SpearWidth / SpearMeshWidth;
            main.startSizeZ = SpearWidth / SpearMeshWidth;

            var emission = spear.emission;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

            var color = spear.colorOverLifetime;
            color.enabled = false;
            var sizeOverLifetime = spear.sizeOverLifetime;
            sizeOverLifetime.enabled = true; // pops in over the first ~0.04 s, then holds
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(new Keyframe(0f, 0.3f), new Keyframe(0.008f, 1f), new Keyframe(1f, 1f)));

            // The trail is the IceTrail TrailRenderer - one trail per projectile.
            var trails = spear.trails;
            trails.enabled = false;

            var renderer = spear.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.alignment = ParticleSystemRenderSpace.Local;
            renderer.sharedMaterial = EnsureLanceMaterial();
            renderer.trailMaterial = null;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = 3; // always drawn over its own trail, mist and chips
            return spear;
        }

        private static TrailRenderer ConfigureTrail(Transform host)
        {
            var trail = host.GetComponent<TrailRenderer>();
            if (trail == null)
            {
                trail = host.gameObject.AddComponent<TrailRenderer>();
            }

            // 0.16 s at 12 m/s is ~1.9 m, about one lance length; narrower than the lance and fading to nothing.
            trail.time = TrailSeconds;
            trail.minVertexDistance = 0.05f;
            trail.widthMultiplier = TrailWidth;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.70f, 0.95f, 1f), 0f),
                    new GradientColorKey(new Color(0.45f, 0.85f, 1f), 0.5f),
                    new GradientColorKey(new Color(0.45f, 0.81f, 1f), 1f),
                },
                new[] { new GradientAlphaKey(0.45f, 0f), new GradientAlphaKey(0.22f, 0.5f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = gradient;
            trail.textureMode = LineTextureMode.Stretch;
            trail.alignment = LineAlignment.View;
            trail.numCapVertices = 2;
            trail.numCornerVertices = 0;
            trail.emitting = true;
            trail.autodestruct = false;
            trail.generateLightingData = false;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            // Base colour a touch over 1 so the existing Bloom lifts it slightly - no light, no extra glow pass.
            trail.sharedMaterial = EnsureParticleMaterial(TrailMaterialPath, EnsureSoftTrailTexture(), new Color(1.15f, 1.25f, 1.3f, 1f));
            trail.sortingOrder = 2;
            return trail;
        }

        private static ParticleSystem ConfigureColdMist(ParticleSystem mist)
        {
            // Faint cold vapour left hanging behind the lance (world space) and gone in a few tenths of a second.
            var main = mist.main;
            main.duration = 1f;
            main.loop = true;
            main.prewarm = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.38f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
            // Larger than a close-up would use: from the ~24 m gameplay camera 0.1-0.2 m puffs are a pixel or two.
            main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 0f;
            main.startColor = new Color(0.847f, 0.98f, 1f, 1f); // #D8FAFF; opacity comes from Color over Lifetime
            main.maxParticles = 25;

            // Time + distance so short, fast flights still leave an even line of puffs (~5 per metre at 12 m/s).
            var emission = mist.emission;
            emission.rateOverTime = 20f;
            emission.rateOverDistance = 3.5f;
            emission.SetBursts(new ParticleSystem.Burst[0]);

            var shape = mist.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 10f;
            shape.radius = 0.05f;
            shape.radiusThickness = 1f;

            var colorOverLifetime = mist.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var mistGradient = new Gradient();
            mistGradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.918f, 0.992f, 1f), 0f),    // #EAFDFF
                    new GradientColorKey(new Color(0.741f, 0.933f, 1f), 0.45f), // #BDEEFF
                    new GradientColorKey(new Color(0.447f, 0.812f, 1f), 1f),    // #72CFFF
                },
                new[] { new GradientAlphaKey(0.5f, 0f), new GradientAlphaKey(0.24f, 0.45f), new GradientAlphaKey(0f, 1f) }); // the puff texture is itself soft, so this reads as ~20%
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(mistGradient);

            var sizeOverLifetime = mist.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(1f, 1.2f)));

            var noise = mist.noise;
            noise.enabled = true;
            noise.strength = 0.06f;
            noise.frequency = 0.6f;
            noise.scrollSpeed = 0.1f;
            noise.octaveCount = 1;
            noise.quality = ParticleSystemNoiseQuality.Low;

            var velocity = mist.velocityOverLifetime;
            velocity.enabled = false;
            var subs = mist.subEmitters;
            subs.enabled = false;
            var collision = mist.collision;
            collision.enabled = false;

            var renderer = mist.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.sharedMaterial = EnsureParticleMaterial(ColdMistMaterialPath,
                AssetDatabase.LoadAssetAtPath<Texture2D>(MistTexturePath), Color.white);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = 0;
            return mist;
        }

        private static ParticleSystem ConfigureTinyIce(Transform host)
        {
            var tiny = host.GetComponent<ParticleSystem>();
            if (tiny == null)
            {
                tiny = host.gameObject.AddComponent<ParticleSystem>();
            }

            // A few spinning ice chips shed off the tail - mesh chips (IceShardTiny, ~0.23 m) so they read as ice.
            var main = tiny.main;
            main.duration = 1f;
            main.loop = true;
            main.prewarm = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.35f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
            main.startSize3D = false;
            main.startSize = new ParticleSystem.MinMaxCurve(0.55f, 0.85f); // ~0.13-0.2 m chips - smaller vanish at this camera
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(Color.white, new Color(0.75f, 0.95f, 1f, 1f));
            main.gravityModifier = 0.15f;
            main.maxParticles = 8;

            var emission = tiny.emission;
            emission.enabled = true;
            emission.rateOverTime = 10f;
            emission.rateOverDistance = 0f;
            emission.SetBursts(new ParticleSystem.Burst[0]);

            var shape = tiny.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 20f;
            shape.radius = 0.06f;

            var rotation = tiny.rotationOverLifetime;
            rotation.enabled = true;
            rotation.separateAxes = true;
            rotation.x = new ParticleSystem.MinMaxCurve(-4f, 4f);
            rotation.y = new ParticleSystem.MinMaxCurve(-4f, 4f);
            rotation.z = new ParticleSystem.MinMaxCurve(-4f, 4f);

            var sizeOverLifetime = tiny.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.6f, 1f), new Keyframe(1f, 0f)));

            var renderer = tiny.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.alignment = ParticleSystemRenderSpace.Local;
            renderer.mesh = LoadMesh(TinyShardModelPath, "IceShardTiny");
            renderer.sharedMaterial = EnsureLanceMaterial(); // the same readable ice as the spear
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = 1;
            return tiny;
        }

        private static void PlaceAtTail(Transform t, float yaw)
        {
            t.localPosition = new Vector3(0f, 0f, TailOffset);
            t.localRotation = Quaternion.Euler(0f, yaw, 0f);
            t.localScale = Vector3.one;
        }

        private static Transform FindOrCreateChild(Transform parent, string name, int siblingIndex)
        {
            Transform child = parent.Find(name);
            if (child == null)
            {
                child = new GameObject(name).transform;
                child.SetParent(parent, false);
            }

            child.SetSiblingIndex(siblingIndex);
            return child;
        }

        private static Mesh LoadMesh(string modelPath, string meshName)
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(modelPath))
            {
                if (asset is Mesh mesh && mesh.name == meshName)
                {
                    return mesh;
                }
            }

            Debug.LogError("[FrostIceLanceSetup] Mesh " + meshName + " not found in " + modelPath);
            return null;
        }

        /// <summary>URP Particles/Unlit, alpha-blended, soft particles off (no depth-texture read on mobile).</summary>
        private static Material EnsureParticleMaterial(string path, Texture2D texture, Color baseColor)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = material == null;
            if (created)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            }

            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", baseColor);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f); // alpha
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            material.SetFloat("_SoftParticlesEnabled", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_SOFTPARTICLES_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            if (created)
            {
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                EditorUtility.SetDirty(material);
            }

            return material;
        }

        /// <summary>16x64 white strip whose alpha falls off smoothly across its width, so the trail has soft edges.</summary>
        private static Texture2D EnsureSoftTrailTexture()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(SoftTrailTexturePath);
            if (existing != null)
            {
                return existing;
            }

            var texture = new Texture2D(16, 64, TextureFormat.RGBA32, false);
            for (int y = 0; y < 64; y++)
            {
                float across = Mathf.Abs(y / 63f * 2f - 1f);
                float alpha = Mathf.SmoothStep(1f, 0f, across);
                alpha = Mathf.Sqrt(alpha); // fuller body, soft edges
                for (int x = 0; x < 16; x++)
                {
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            System.IO.File.WriteAllBytes(SoftTrailTexturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(SoftTrailTexturePath);

            // The project's texture preset imports PNGs as sprites; this one is a plain clamped texture.
            var importer = (TextureImporter)AssetImporter.GetAtPath(SoftTrailTexturePath);
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(SoftTrailTexturePath);
        }

        private static Material EnsureLanceMaterial()
        {
            Shader shader = Shader.Find("AlienDefense/IceLance");
            if (shader == null)
            {
                Debug.LogError("[FrostIceLanceSetup] Shader AlienDefense/IceLance not found.");
                return null;
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(LanceMaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "MAT_IceLance" };
                AssetDatabase.CreateAsset(material, LanceMaterialPath);
            }

            // Pale cyan body with a thin white core and blue-cyan edges; mostly opaque so the silhouette reads.
            material.shader = shader;
            material.SetColor("_CoreColor", new Color(0.961f, 1f, 1f, 1f));   // #F5FFFF
            material.SetColor("_BodyColor", new Color(0.50f, 0.90f, 1f, 1f));  // between #B9F5FF and #63DFFF, saturated to survive ACES
            material.SetColor("_EdgeColor", new Color(0.22f, 0.72f, 1f, 1f));  // deeper #63DFFF
            material.SetFloat("_CoreWidth", 0.14f);
            material.SetFloat("_EdgeStart", 0.45f);
            material.SetFloat("_BodyAlpha", 0.82f);
            material.SetFloat("_EdgeAlpha", 0.95f);
            material.SetFloat("_FacetShading", 0.3f);
            material.SetFloat("_GlintStrength", 0.5f);
            material.SetFloat("_Emission", 0.15f); // light Bloom only - more washes the cyan out to white
            EditorUtility.SetDirty(material);
            return material;
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
