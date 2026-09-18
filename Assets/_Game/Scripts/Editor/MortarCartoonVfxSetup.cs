using System;
using System.Collections.Generic;
using System.IO;
using AlienDefense.Combat;
using AlienDefense.Towers;
using AlienDefense.Vfx;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AlienDefense.EditorTools
{
    /// <summary>Cartoon mortar muzzle / readable yellow shell + short thick tracer / compact fire impact, plus
    /// alternate L/R muzzles and barrel recoil. Gameplay values (damage, APS, range, splash) are untouched.
    /// Safe to re-run.</summary>
    internal static class MortarCartoonVfxSetup
    {
        private const string Folder = "Assets/_Game/VFX/Mortar";
        private const string MaterialFolder = Folder + "/Materials";
        private const string TextureFolder = Folder + "/Textures";

        private const string MuzzlePrefabPath = Folder + "/MuzzleFlash_Mortar_Cartoon.prefab";
        private const string ImpactPrefabPath = Folder + "/MortarImpact_Cartoon.prefab";
        private const string ProjectilePrefabPath = "Assets/_Game/Prefabs/Projectiles/Projectile_Mortar.prefab";

        private const string MuzzleDefinitionPath = "Assets/_Game/Data/Vfx/VfxDefinition_Vfx_MuzzleFlash_Mortar.asset";
        private const string ImpactDefinitionPath = "Assets/_Game/Data/Vfx/VfxDefinition_Vfx_MortarImpact.asset";
        private const string ProjectileDefinitionPath = "Assets/_Game/Data/Projectiles/Projectile_Mortar.asset";

        private const string TowerPrefabPath = "Assets/_Game/Prefabs/Towers/Tower_Mortar.prefab";
        private const string TowerDefinitionPath = "Assets/_Game/Data/Towers/TowerDefinition_Tower_Mortar.asset";

        private const string HeadPathSuffix = "turret_mount1/turret_head1";
        private const string LeftBarrelName = "turret_head1_barrel_l";
        private const string RightBarrelName = "turret_head1_barrel_r";
        private const string LeftMuzzleName = "MuzzlePoint_Left";
        private const string RightMuzzleName = "MuzzlePoint_Right";

        // Barrel origin is ~z 0.86 in head space; current FirePoint sat at z 2.2 — tip local Z on each barrel.
        private const float BarrelTipLocalZ = 1.34f;

        private const float MuzzleLifetime = 0.1f;
        private const float ImpactLifetime = 0.35f;

        // Muzzle palette.
        private static readonly Color FlashWhite = Hex(0xFFF6CC);
        private static readonly Color FlashYellow = Hex(0xFFE34F);
        private static readonly Color FlashOrange = Hex(0xFF9F2F);

        // Projectile / tracer palette.
        private static readonly Color TracerWhite = Hex(0xFFF0A6);
        private static readonly Color TracerYellow = Hex(0xFFD14C);
        private static readonly Color TracerOrange = Hex(0xFF932E);

        // Impact fire palette.
        private static readonly Color ImpactFlashCenter = Hex(0xFFF8D6);
        private static readonly Color ImpactFlashMid = Hex(0xFFE44E);
        private static readonly Color ImpactFlashOuter = Hex(0xFFB236);
        private static readonly Color YellowCore = Hex(0xFFD83E);
        private static readonly Color OrangeFireA = Hex(0xFF9A2F);
        private static readonly Color OrangeFireB = Hex(0xFF7929);
        private static readonly Color OrangeFireC = Hex(0xF45A28);
        private static readonly Color RedOuter = Hex(0xE94A2B);
        private static readonly Color SparkYellow = Hex(0xFFD64A);
        private static readonly Color SparkOrange = Hex(0xFF9A2C);
        private static readonly Color SmokeWarm = Hex(0x75645A);
        private static readonly Color SmokeDark = Hex(0x62554D);

        // Legacy aliases used by texture generators.
        private static readonly Color CoreGold = YellowCore;

        private sealed class Materials
        {
            public Material AdditiveBlob;
            public Material AlphaBlob;
            public Material AdditiveStarburst;
            public Material AdditiveRing;
            public Material Trail;
        }

        [MenuItem("AlienDefense/Setup/VFX/Mortar Cartoon VFX")]
        private static void Run()
        {
            Apply();
            Debug.Log("[MortarCartoonVfxSetup] Mortar muzzle, tracer, impact, alternate barrels and recoil ready.");
        }

        /// <summary>Builds (or refreshes) mortar cartoon VFX assets and wires Tower_Mortar.</summary>
        public static void Apply()
        {
            EditorFolderUtility.EnsureFolder(Folder);
            EditorFolderUtility.EnsureFolder(MaterialFolder);
            EditorFolderUtility.EnsureFolder(TextureFolder);
            EditorFolderUtility.EnsureFolder("Assets/_Game/Data/Vfx");
            EditorFolderUtility.EnsureFolder("Assets/_Game/Data/Projectiles");
            EditorFolderUtility.EnsureFolder("Assets/_Game/Prefabs/Projectiles");

            Texture2D blob = EnsureTexture("T_VFX_Mortar_Blob", 96, 96, BlobPixel);
            Texture2D starburst = EnsureTexture("T_VFX_Mortar_Starburst", 128, 128, StarburstPixel);
            Texture2D ring = EnsureTexture("T_VFX_Mortar_Ring", 128, 128, RingPixel);
            var materials = new Materials
            {
                AdditiveBlob = EnsureParticleMaterial("MAT_VFX_Mortar_Blob", blob, additive: true),
                AlphaBlob = EnsureParticleMaterial("MAT_VFX_Mortar_Smoke", blob, additive: false),
                AdditiveStarburst = EnsureParticleMaterial("MAT_VFX_Mortar_Starburst", starburst, additive: true),
                AdditiveRing = EnsureParticleMaterial("MAT_VFX_Mortar_Ring", ring, additive: true),
                Trail = EnsureParticleMaterial("MAT_VFX_Mortar_Trail", null, additive: true),
            };

            VfxDefinition muzzleDef = BuildMuzzle(materials);
            VfxDefinition impactDef = BuildImpact(materials);
            ProjectileDefinition projectileDef = BuildProjectile(impactDef, materials);
            WireTowerDefinition(projectileDef, muzzleDef);
            WireTowerPrefab();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MortarCartoonVfxSetup] Saved chunky muzzle, thick tracer, 0.35s fire impact and Kinetic popup.");
            if (Application.isBatchMode)
                MortarVfxPreview.Render();
        }

        // ------------------------------------------------------------------------------------------ muzzle

        private static VfxDefinition BuildMuzzle(Materials m)
        {
            var root = new GameObject("MuzzleFlash_Mortar_Cartoon");
            var systems = new List<ParticleSystem>();

            ParticleSystem core = Burst(root.transform, "CoreFlash", 1, 0f, systems);
            var coreMain = core.main;
            coreMain.startLifetime = 0.05f;
            coreMain.startSpeed = 0f;
            coreMain.startSize = 0.38f;
            coreMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            DisableShape(core);
            SetSizeCurve(core, Key(0f, 0.55f), Key(0.25f, 1f), Key(1f, 0.2f));
            SetColorOverLifetime(core,
                new[] { CK(FlashWhite, 0f), CK(FlashYellow, 1f) },
                new[] { AK(1f, 0f), AK(0f, 1f) });
            SetBillboard(core, m.AdditiveBlob, sortingOrder: 5);

            ParticleSystem star = Burst(root.transform, "StarFlash", 1, 0f, systems);
            var starMain = star.main;
            starMain.startLifetime = 0.085f;
            starMain.startSpeed = 0f;
            starMain.startSize = 0.72f;
            starMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            DisableShape(star);
            SetSizeCurve(star, Key(0f, 0.4f), Key(0.2f, 1.05f), Key(1f, 0.45f));
            SetColorOverLifetime(star,
                new[] { CK(FlashWhite, 0f), CK(FlashYellow, 0.45f), CK(FlashOrange, 1f) },
                new[] { AK(1f, 0f), AK(0.85f, 0.35f), AK(0f, 1f) });
            SetBillboard(star, m.AdditiveStarburst, sortingOrder: 6);

            ParticleSystem forwardFlash = Burst(root.transform, "ForwardFlash", 1, 0f, systems);
            var forwardMain = forwardFlash.main;
            forwardMain.startLifetime = new ParticleSystem.MinMaxCurve(0.05f, 0.08f);
            forwardMain.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 5f);
            forwardMain.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.1f);
            forwardMain.startColor = new ParticleSystem.MinMaxGradient(FlashWhite, FlashYellow);
            var forwardShape = forwardFlash.shape;
            forwardShape.enabled = true;
            forwardShape.shapeType = ParticleSystemShapeType.Cone;
            forwardShape.angle = 6f;
            forwardShape.radius = 0.01f;
            SetSizeCurve(forwardFlash, Key(0f, 1f), Key(1f, 0.15f));
            SetColorOverLifetime(forwardFlash,
                new[] { CK(FlashWhite, 0f), CK(FlashYellow, 0.5f), CK(FlashOrange, 1f) },
                new[] { AK(1f, 0f), AK(0.7f, 0.45f), AK(0f, 1f) });
            SetStretchedBillboard(forwardFlash, m.AdditiveBlob, sortingOrder: 5, lengthScale: 1.9f,
                velocityScale: 0.14f);

            ParticleSystem sparks = Burst(root.transform, "Sparks", 5, 0.008f, systems);
            var sparkMain = sparks.main;
            sparkMain.startLifetime = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
            sparkMain.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4f);
            sparkMain.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.06f);
            sparkMain.startColor = new ParticleSystem.MinMaxGradient(FlashYellow, FlashOrange);
            sparkMain.gravityModifier = 0.35f;
            var sparkShape = sparks.shape;
            sparkShape.enabled = true;
            sparkShape.shapeType = ParticleSystemShapeType.Cone;
            sparkShape.angle = 28f;
            sparkShape.radius = 0.02f;
            SetSizeCurve(sparks, Key(0f, 1f), Key(1f, 0.15f));
            SetBillboard(sparks, m.AdditiveBlob, sortingOrder: 4);

            AttachPooledVfx(root, systems, keepWorldUpright: false);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, MuzzlePrefabPath);
            Object.DestroyImmediate(root);

            return EnsureVfxDefinition(MuzzleDefinitionPath, "vfx_muzzle_mortar", prefab, MuzzleLifetime, 4, 8, 32);
        }

        // ------------------------------------------------------------------------------------------ impact

        private static VfxDefinition BuildImpact(Materials m)
        {
            var root = new GameObject("MortarImpact_Cartoon");
            var systems = new List<ParticleSystem>();

            // Compact cartoon fire burst (~15–25 particles), distinct from Rocket's tall V-plumes.
            ParticleSystem flash = Burst(root.transform, "ImpactFlash", 1, 0f, systems);
            var flashMain = flash.main;
            flashMain.startLifetime = 0.08f;
            flashMain.startSpeed = 0f;
            flashMain.startSize = 0.95f;
            flashMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            DisableShape(flash);
            SetSizeCurve(flash, Key(0f, 0.3f), Key(0.2f, 1.1f), Key(1f, 0f));
            SetColorOverLifetime(flash,
                new[] { CK(ImpactFlashCenter, 0f), CK(ImpactFlashMid, 0.4f), CK(ImpactFlashOuter, 1f) },
                new[] { AK(1f, 0f), AK(0.85f, 0.35f), AK(0f, 1f) });
            SetBillboard(flash, m.AdditiveStarburst, sortingOrder: 8);

            ParticleSystem yellow = Burst(root.transform, "YellowCore", 4, 0.015f, systems);
            var yellowMain = yellow.main;
            yellowMain.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.2f);
            yellowMain.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
            yellowMain.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.5f);
            yellowMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            yellowMain.gravityModifier = -0.15f;
            var yellowShape = yellow.shape;
            yellowShape.enabled = true;
            yellowShape.shapeType = ParticleSystemShapeType.Sphere;
            yellowShape.radius = 0.1f;
            SetSizeCurve(yellow, Key(0f, 0.85f), Key(0.35f, 1.05f), Key(1f, 0.25f));
            SetColorOverLifetime(yellow,
                new[] { CK(ImpactFlashCenter, 0f), CK(YellowCore, 0.5f), CK(OrangeFireA, 1f) },
                new[] { AK(1f, 0f), AK(0.9f, 0.45f), AK(0f, 1f) });
            SetBillboard(yellow, m.AdditiveBlob, sortingOrder: 7);

            ParticleSystem orange = Burst(root.transform, "OrangeFire", 7, 0.03f, systems);
            var orangeMain = orange.main;
            orangeMain.startLifetime = new ParticleSystem.MinMaxCurve(0.14f, 0.24f);
            orangeMain.startSpeed = new ParticleSystem.MinMaxCurve(1f, 2.2f);
            orangeMain.startSize = new ParticleSystem.MinMaxCurve(0.32f, 0.48f);
            orangeMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            orangeMain.gravityModifier = -0.2f;
            var orangeShape = orange.shape;
            orangeShape.enabled = true;
            orangeShape.shapeType = ParticleSystemShapeType.Sphere;
            orangeShape.radius = 0.14f;
            SetSizeCurve(orange, Key(0f, 1f), Key(1f, 0.3f));
            SetColorOverLifetime(orange,
                new[] { CK(OrangeFireA, 0f), CK(OrangeFireB, 0.45f), CK(OrangeFireC, 1f) },
                new[] { AK(1f, 0f), AK(0.8f, 0.5f), AK(0f, 1f) });
            SetBillboard(orange, m.AdditiveBlob, sortingOrder: 6);

            ParticleSystem red = Burst(root.transform, "RedOuterFire", 4, 0.04f, systems);
            var redMain = red.main;
            redMain.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.2f);
            redMain.startSpeed = new ParticleSystem.MinMaxCurve(1.4f, 2.6f);
            redMain.startSize = new ParticleSystem.MinMaxCurve(0.28f, 0.4f);
            redMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            redMain.gravityModifier = -0.1f;
            var redShape = red.shape;
            redShape.enabled = true;
            redShape.shapeType = ParticleSystemShapeType.Sphere;
            redShape.radius = 0.16f;
            SetSizeCurve(red, Key(0f, 1f), Key(1f, 0.35f));
            SetColorOverLifetime(red,
                new[] { CK(OrangeFireC, 0f), CK(RedOuter, 1f) },
                new[] { AK(0.55f, 0f), AK(0f, 1f) });
            SetBillboard(red, m.AdditiveBlob, sortingOrder: 5);

            ParticleSystem ring = Burst(root.transform, "Shockwave", 1, 0.02f, systems);
            var ringMain = ring.main;
            ringMain.startLifetime = 0.16f;
            ringMain.startSpeed = 0f;
            ringMain.startSize = 0.7f;
            DisableShape(ring);
            SetSizeCurve(ring, Key(0f, 0.3f), Key(1f, 1.2f));
            SetColorOverLifetime(ring,
                new[] { CK(FlashYellow, 0f), CK(FlashOrange, 1f) },
                new[] { AK(0.5f, 0f), AK(0f, 1f) });
            SetBillboard(ring, m.AdditiveRing, sortingOrder: 3);

            ParticleSystem sparks = Burst(root.transform, "Sparks", 8, 0.035f, systems);
            var sparkMain = sparks.main;
            sparkMain.startLifetime = new ParticleSystem.MinMaxCurve(0.1f, 0.22f);
            sparkMain.startSpeed = new ParticleSystem.MinMaxCurve(2f, 3.8f);
            sparkMain.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.09f);
            sparkMain.startColor = new ParticleSystem.MinMaxGradient(SparkYellow, SparkOrange);
            sparkMain.gravityModifier = 0.6f;
            var sparkShape = sparks.shape;
            sparkShape.enabled = true;
            sparkShape.shapeType = ParticleSystemShapeType.Sphere;
            sparkShape.radius = 0.12f;
            SetSizeCurve(sparks, Key(0f, 1f), Key(1f, 0.1f));
            SetBillboard(sparks, m.AdditiveBlob, sortingOrder: 7);

            ParticleSystem smoke = Burst(root.transform, "SmokePuffs", 3, 0.06f, systems);
            var smokeMain = smoke.main;
            smokeMain.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.4f);
            smokeMain.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
            smokeMain.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.55f);
            smokeMain.startColor = new ParticleSystem.MinMaxGradient(SmokeWarm, SmokeDark);
            smokeMain.gravityModifier = -0.12f;
            var smokeShape = smoke.shape;
            smokeShape.enabled = true;
            smokeShape.shapeType = ParticleSystemShapeType.Sphere;
            smokeShape.radius = 0.1f;
            SetSizeCurve(smoke, Key(0f, 0.75f), Key(1f, 1.25f));
            SetColorOverLifetime(smoke, White(), new[] { AK(0.25f, 0f), AK(0.15f, 0.4f), AK(0f, 1f) });
            SetBillboard(smoke, m.AlphaBlob, sortingOrder: 1);

            AttachPooledVfx(root, systems, keepWorldUpright: true);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, ImpactPrefabPath);
            Object.DestroyImmediate(root);
            return EnsureVfxDefinition(ImpactDefinitionPath, "vfx_mortar_impact", prefab, ImpactLifetime, 4, 8, 32);
        }

        // ------------------------------------------------------------------------------------------ projectile

        private static ProjectileDefinition BuildProjectile(VfxDefinition hitVfx, Materials materials)
        {
            GameObject prefab = BuildOrRefreshProjectilePrefab(materials);

            var definition = AssetDatabase.LoadAssetAtPath<ProjectileDefinition>(ProjectileDefinitionPath);
            bool created = definition == null;
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<ProjectileDefinition>();
                AssetDatabase.CreateAsset(definition, ProjectileDefinitionPath);
            }

            var so = new SerializedObject(definition);
            so.FindProperty("_id").stringValue = "projectile_mortar";
            so.FindProperty("_displayName").stringValue = "Mortar Shell";
            so.FindProperty("_prefab").objectReferenceValue = prefab.GetComponent<ProjectileController>();
            so.FindProperty("_hitVfxDefinition").objectReferenceValue = hitVfx;
            so.FindProperty("_killVfxDefinition").objectReferenceValue = null;
            so.FindProperty("_damagePopupStyle").enumValueIndex = (int)DamagePopupStyle.Kinetic;

            // Defaults are only needed when this tool creates the asset. Re-running the visual setup must never
            // overwrite motion, hit or pool tuning that a designer has already authored in the project.
            if (created)
            {
                so.FindProperty("_speed").floatValue = 10f;
                so.FindProperty("_maximumLifetime").floatValue = 5f;
                so.FindProperty("_hitDistance").floatValue = 0.25f;
                so.FindProperty("_arcHeight").floatValue = 0f;
                so.FindProperty("_poolPrewarmCount").intValue = 24;
                so.FindProperty("_poolDefaultCapacity").intValue = 48;
                so.FindProperty("_poolMaximumSize").intValue = 160;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static GameObject BuildOrRefreshProjectilePrefab(Materials materials)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePrefabPath);
            if (existing != null)
            {
                GameObject contents = PrefabUtility.LoadPrefabContents(ProjectilePrefabPath);
                try
                {
                    ApplyProjectileVisuals(contents, materials);
                    PrefabUtility.SaveAsPrefabAsset(contents, ProjectilePrefabPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }

                return AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePrefabPath);
            }

            var root = new GameObject("Projectile_Mortar");
            int projectileLayer = LayerMask.NameToLayer("Projectile");
            if (projectileLayer >= 0)
            {
                root.layer = projectileLayer;
            }

            var controller = root.AddComponent<ProjectileController>();

            var visualRoot = new GameObject("VisualRoot");
            visualRoot.transform.SetParent(root.transform, false);

            GameObject mesh = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            mesh.name = "Mesh";
            mesh.transform.SetParent(visualRoot.transform, false);
            mesh.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            mesh.transform.localScale = new Vector3(0.08f, 0.08f, 0.32f);
            Object.DestroyImmediate(mesh.GetComponent<Collider>());

            var trailObject = new GameObject("Trail");
            trailObject.transform.SetParent(root.transform, false);
            trailObject.AddComponent<TrailRenderer>();

            var glowObject = new GameObject("Glow");
            glowObject.transform.SetParent(root.transform, false);
            glowObject.AddComponent<ParticleSystem>();

            var embersObject = new GameObject("Embers");
            embersObject.transform.SetParent(root.transform, false);
            embersObject.AddComponent<ParticleSystem>();

            ApplyProjectileVisuals(root, materials);

            PrefabUtility.SaveAsPrefabAsset(root, ProjectilePrefabPath);
            Object.DestroyImmediate(root);
            return AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePrefabPath);
        }

        private static void ApplyProjectileVisuals(GameObject root, Materials materials)
        {
            Material coreMaterial = EditorMaterialUtility.CreateOrLoadMaterial(
                "Mat_Projectile_Mortar", "Universal Render Pipeline/Unlit", TracerYellow);
            Color brightCore = new Color(
                Mathf.Min(1f, TracerWhite.r * 1.15f),
                Mathf.Min(1f, TracerWhite.g * 1.15f),
                Mathf.Min(1f, TracerWhite.b * 1.05f),
                1f);
            coreMaterial.SetColor("_BaseColor", brightCore);
            coreMaterial.SetColor("_Color", brightCore);
            EditorUtility.SetDirty(coreMaterial);

            // ~1.8× previous capsule so Note10 Simulator can read the shell at gameplay camera distance.
            const float shellWidth = 0.08f;
            const float shellLength = 0.32f;
            Transform meshTransform = root.transform.Find("VisualRoot/Mesh");
            var renderer = meshTransform != null ? meshTransform.GetComponent<MeshRenderer>() : null;
            if (renderer != null)
            {
                // Capsule primitive is Y-up; rotate so length follows projectile +Z forward.
                if (meshTransform.GetComponent<MeshFilter>()?.sharedMesh != null &&
                    meshTransform.GetComponent<MeshFilter>().sharedMesh.name.Contains("Capsule"))
                {
                    meshTransform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    meshTransform.localScale = new Vector3(shellWidth, shellLength * 0.5f, shellWidth);
                }
                else
                {
                    meshTransform.localRotation = Quaternion.identity;
                    meshTransform.localScale = new Vector3(shellWidth, shellWidth, shellLength);
                }

                renderer.sharedMaterial = coreMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            }

            Transform trailTransform = root.transform.Find("Trail");
            var trail = trailTransform != null ? trailTransform.GetComponent<TrailRenderer>() : null;
            if (trail != null)
            {
                trail.time = 0.09f;
                trail.widthMultiplier = 1f;
                trail.widthCurve = new AnimationCurve(
                    new Keyframe(0f, shellWidth * 0.6f),
                    new Keyframe(0.45f, shellWidth * 0.35f),
                    new Keyframe(1f, 0f));
                trail.minVertexDistance = 0.02f;
                trail.numCornerVertices = 2;
                trail.numCapVertices = 2;
                trail.textureMode = LineTextureMode.Stretch;
                trail.sharedMaterial = materials.Trail;
                trail.colorGradient = MakeGradient(
                    new[] { CK(TracerWhite, 0f), CK(TracerYellow, 0.45f), CK(TracerOrange, 1f) },
                    new[] { AK(0.9f, 0f), AK(0.6f, 0.5f), AK(0f, 1f) });
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                trail.receiveShadows = false;
                trail.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                trail.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            }

            ParticleSystem glow = EnsureChildParticle(root.transform, "Glow");
            var glowMain = glow.main;
            glowMain.loop = true;
            glowMain.duration = 5f;
            glowMain.startLifetime = 0.12f;
            glowMain.startSpeed = 0f;
            glowMain.startSize = shellWidth * 1.35f;
            glowMain.startColor = new Color(TracerYellow.r, TracerYellow.g, TracerYellow.b, 0.45f);
            glowMain.simulationSpace = ParticleSystemSimulationSpace.Local;
            glowMain.maxParticles = 2;
            glowMain.playOnAwake = false;
            DisableShape(glow);
            var glowEmission = glow.emission;
            glowEmission.enabled = true;
            glowEmission.rateOverTime = 8f;
            glowEmission.rateOverDistance = 0f;
            glowEmission.SetBursts(Array.Empty<ParticleSystem.Burst>());
            SetBillboard(glow, materials.AdditiveBlob, sortingOrder: 2);

            ParticleSystem embers = EnsureChildParticle(root.transform, "Embers");
            embers.transform.localPosition = new Vector3(0f, 0f, -shellLength * 0.35f);
            var emberMain = embers.main;
            emberMain.loop = true;
            emberMain.duration = 1f;
            emberMain.startLifetime = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
            emberMain.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
            emberMain.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.055f);
            emberMain.startColor = new ParticleSystem.MinMaxGradient(TracerYellow, TracerOrange);
            emberMain.simulationSpace = ParticleSystemSimulationSpace.World;
            emberMain.maxParticles = 4;
            emberMain.playOnAwake = false;
            emberMain.gravityModifier = 0.15f;
            var emberShape = embers.shape;
            emberShape.enabled = true;
            emberShape.shapeType = ParticleSystemShapeType.Cone;
            emberShape.angle = 18f;
            emberShape.radius = 0.02f;
            emberShape.rotation = new Vector3(0f, 180f, 0f);
            var emberEmission = embers.emission;
            emberEmission.enabled = true;
            emberEmission.rateOverTime = 10f;
            emberEmission.rateOverDistance = 0f;
            emberEmission.SetBursts(Array.Empty<ParticleSystem.Burst>());
            SetSizeCurve(embers, Key(0f, 1f), Key(1f, 0.2f));
            SetBillboard(embers, materials.AdditiveBlob, sortingOrder: 1);

            var controller = root.GetComponent<ProjectileController>();
            if (controller != null)
            {
                var controllerSerialized = new SerializedObject(controller);
                controllerSerialized.FindProperty("_trail").objectReferenceValue = trail;
                SerializedProperty attached = controllerSerialized.FindProperty("_attachedParticles");
                attached.arraySize = 2;
                attached.GetArrayElementAtIndex(0).objectReferenceValue = glow;
                attached.GetArrayElementAtIndex(1).objectReferenceValue = embers;
                controllerSerialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static ParticleSystem EnsureChildParticle(Transform root, string name)
        {
            Transform child = root.Find(name);
            if (child == null)
            {
                child = new GameObject(name).transform;
                child.SetParent(root, false);
            }

            var ps = child.GetComponent<ParticleSystem>();
            if (ps == null)
            {
                ps = child.gameObject.AddComponent<ParticleSystem>();
            }

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return ps;
        }

        // ------------------------------------------------------------------------------------------ tower wiring

        private static void WireTowerDefinition(ProjectileDefinition projectile, VfxDefinition muzzle)
        {
            var definition = AssetDatabase.LoadAssetAtPath<TowerDefinition>(TowerDefinitionPath);
            if (definition == null)
            {
                Debug.LogError("[MortarCartoonVfxSetup] Missing " + TowerDefinitionPath);
                return;
            }

            var so = new SerializedObject(definition);
            so.FindProperty("_projectileDefinition").objectReferenceValue = projectile;
            so.FindProperty("_muzzleVfxDefinition").objectReferenceValue = muzzle;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }

        private static void WireTowerPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(TowerPrefabPath);
            try
            {
                Transform head = FindHead(root.transform);
                var attack = root.GetComponent<TowerAttackController>();
                if (head == null || attack == null)
                {
                    Debug.LogError("[MortarCartoonVfxSetup] Tower_Mortar is missing turret_head1 or TowerAttackController.");
                    return;
                }

                Transform leftBarrel = head.Find(LeftBarrelName);
                Transform rightBarrel = head.Find(RightBarrelName);
                if (leftBarrel == null || rightBarrel == null)
                {
                    Debug.LogError("[MortarCartoonVfxSetup] Could not find barrel_l / barrel_r under turret_head1.");
                    return;
                }

                Transform leftMuzzle = EnsureMuzzle(leftBarrel, LeftMuzzleName);
                Transform rightMuzzle = EnsureMuzzle(rightBarrel, RightMuzzleName);

                // Retire the centred FirePoint so only the barrel tips fire.
                Transform oldFirePoint = head.Find("FirePoint");
                if (oldFirePoint != null)
                {
                    Object.DestroyImmediate(oldFirePoint.gameObject);
                }

                var attackSo = new SerializedObject(attack);
                attackSo.FindProperty("_firePoint").objectReferenceValue = leftMuzzle;
                SerializedProperty extras = attackSo.FindProperty("_extraFirePoints");
                extras.arraySize = 1;
                extras.GetArrayElementAtIndex(0).objectReferenceValue = rightMuzzle;
                attackSo.FindProperty("_alternateFirePoints").boolValue = true;
                attackSo.ApplyModifiedPropertiesWithoutUndo();

                var recoil = root.GetComponent<MortarBarrelRecoil>();
                if (recoil == null)
                {
                    recoil = root.AddComponent<MortarBarrelRecoil>();
                }

                var recoilSo = new SerializedObject(recoil);
                recoilSo.FindProperty("_attack").objectReferenceValue = attack;
                recoilSo.FindProperty("_leftBarrel").objectReferenceValue = leftBarrel;
                recoilSo.FindProperty("_rightBarrel").objectReferenceValue = rightBarrel;
                recoilSo.FindProperty("_recoilDistance").floatValue = 0.06f;
                recoilSo.FindProperty("_recoilDuration").floatValue = 0.1f;
                recoilSo.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, TowerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Transform FindHead(Transform root)
        {
            foreach (Transform child in root)
            {
                Transform head = child.Find(HeadPathSuffix);
                if (head != null)
                {
                    return head;
                }
            }

            return null;
        }

        private static Transform EnsureMuzzle(Transform barrel, string name)
        {
            Transform muzzle = barrel.Find(name);
            if (muzzle == null)
            {
                muzzle = new GameObject(name).transform;
                muzzle.SetParent(barrel, false);
            }

            muzzle.localPosition = new Vector3(0f, 0f, BarrelTipLocalZ);
            muzzle.localRotation = Quaternion.identity;
            muzzle.localScale = Vector3.one;
            return muzzle;
        }

        // ------------------------------------------------------------------------------------------ helpers

        private static VfxDefinition EnsureVfxDefinition(string path, string id, GameObject prefab, float lifetime,
            int prewarm, int capacity, int max)
        {
            var definition = AssetDatabase.LoadAssetAtPath<VfxDefinition>(path);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<VfxDefinition>();
                AssetDatabase.CreateAsset(definition, path);
            }

            var so = new SerializedObject(definition);
            so.FindProperty("_id").stringValue = id;
            so.FindProperty("_prefab").objectReferenceValue = prefab.GetComponent<PooledVfx>();
            so.FindProperty("_lifetime").floatValue = lifetime;
            so.FindProperty("_poolPrewarmCount").intValue = prewarm;
            so.FindProperty("_poolDefaultCapacity").intValue = capacity;
            so.FindProperty("_poolMaximumSize").intValue = max;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static void AttachPooledVfx(GameObject root, List<ParticleSystem> systems, bool keepWorldUpright)
        {
            var pooled = root.AddComponent<PooledVfx>();
            var pso = new SerializedObject(pooled);
            SerializedProperty array = pso.FindProperty("_particleSystems");
            array.arraySize = systems.Count;
            for (int i = 0; i < systems.Count; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = systems[i];
            }

            pso.FindProperty("_keepWorldUpright").boolValue = keepWorldUpright;
            pso.ApplyModifiedPropertiesWithoutUndo();
        }

        private static ParticleSystem NewSystem(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.prewarm = false;
            main.gravityModifier = 0f;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startColor = Color.white;
            return ps;
        }

        private static ParticleSystem Burst(Transform parent, string name, short count, float delay, List<ParticleSystem> collect)
        {
            ParticleSystem ps = NewSystem(parent, name);
            var main = ps.main;
            main.loop = false;
            main.duration = 0.5f;
            main.startDelay = delay;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = count;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, count) });
            collect.Add(ps);
            return ps;
        }

        private static void DisableShape(ParticleSystem ps)
        {
            var shape = ps.shape;
            shape.enabled = false;
        }

        private static void SetSizeCurve(ParticleSystem ps, params Keyframe[] keys)
        {
            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            float max = 0f;
            foreach (Keyframe k in keys)
            {
                max = Mathf.Max(max, k.value);
            }

            var normalised = new Keyframe[keys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                normalised[i] = new Keyframe(keys[i].time, keys[i].value / max);
            }

            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(max, new AnimationCurve(normalised));
        }

        private static void SetColorOverLifetime(ParticleSystem ps, GradientColorKey[] colors, GradientAlphaKey[] alphas)
        {
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(colors, alphas);
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        private static void SetBillboard(ParticleSystem ps, Material material, int sortingOrder)
        {
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            renderer.sortingOrder = sortingOrder;
        }

        private static void SetStretchedBillboard(ParticleSystem ps, Material material, int sortingOrder,
            float lengthScale, float velocityScale)
        {
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.lengthScale = lengthScale;
            renderer.velocityScale = velocityScale;
            renderer.cameraVelocityScale = 0f;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            renderer.sortingOrder = sortingOrder;
        }

        private static GradientColorKey[] White() => new[] { CK(Color.white, 0f), CK(Color.white, 1f) };
        private static Keyframe Key(float time, float value) => new Keyframe(time, value);
        private static GradientColorKey CK(Color color, float time) => new GradientColorKey(color, time);
        private static GradientAlphaKey AK(float alpha, float time) => new GradientAlphaKey(alpha, time);

        private static Gradient MakeGradient(GradientColorKey[] colors, GradientAlphaKey[] alphas)
        {
            var gradient = new Gradient();
            gradient.SetKeys(colors, alphas);
            return gradient;
        }

        private static Color Hex(int rgb)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
        }

        private static Material EnsureParticleMaterial(string name, Texture2D texture, bool additive)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = material == null;
            if (created)
            {
                material = new Material(EditorMaterialUtility.FindShaderWithFallback(
                    "Universal Render Pipeline/Particles/Unlit"));
            }
            else if (EditorMaterialUtility.IsShaderBroken(material.shader) ||
                     material.shader.name != "Universal Render Pipeline/Particles/Unlit")
            {
                material.shader = EditorMaterialUtility.FindShaderWithFallback(
                    "Universal Render Pipeline/Particles/Unlit");
            }

            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetColor("_Color", Color.white);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", additive ? 2f : 0f);
            material.SetFloat("_BlendOp", (float)UnityEngine.Rendering.BlendOp.Add);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", additive
                ? (float)UnityEngine.Rendering.BlendMode.One
                : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_DstBlendAlpha", additive
                ? (float)UnityEngine.Rendering.BlendMode.One
                : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            material.SetFloat("_SoftParticlesEnabled", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
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

        private static Texture2D EnsureTexture(string name, int width, int height, Func<float, float, Color> pixel)
        {
            string path = TextureFolder + "/" + name + ".png";
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    texture.SetPixel(x, y, pixel((x + 0.5f) / width, (y + 0.5f) / height));
                }
            }

            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static float Polar(float u, float v, out float angle)
        {
            float dx = u - 0.5f;
            float dy = v - 0.5f;
            angle = Mathf.Atan2(dy, dx);
            return Mathf.Sqrt(dx * dx + dy * dy) * 2f;
        }

        private static Color BlobPixel(float u, float v)
        {
            float r = Polar(u, v, out float angle);
            float edge = 0.9f + 0.05f * Mathf.Sin(angle * 5f) + 0.03f * Mathf.Sin(angle * 3f + 1.3f);
            // A broad alpha falloff prevents an opaque-looking disc around each tracer and hit.
            float alpha = Mathf.Pow(1f - Mathf.SmoothStep(0f, edge, r), 2f);
            return new Color(1f, 1f, 1f, alpha);
        }

        private static Color StarburstPixel(float u, float v)
        {
            float r = Polar(u, v, out float angle);
            float longSpikes = Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * 4f)), 18f);
            float shortSpikes = Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * 4f + Mathf.PI * 0.25f)), 18f) * 0.6f;
            float reach = 0.32f + Mathf.Max(longSpikes, shortSpikes) * 0.66f;
            float alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(reach - 0.12f, reach, r));
            Color color = Color.Lerp(FlashWhite, FlashYellow, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 0.7f, r)));
            color.a = Mathf.Clamp01(alpha);
            return color;
        }

        private static Color RingPixel(float u, float v)
        {
            float r = Polar(u, v, out _);
            const float center = 0.62f;
            const float halfWidth = 0.1f;
            float distance = Mathf.Abs(r - center);
            float alpha = 1f - Mathf.SmoothStep(halfWidth * 0.35f, halfWidth, distance);
            return new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
        }
    }
}
