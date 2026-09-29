using System.Collections.Generic;
using AlienDefense.Base;
using AlienDefense.UI.BaseBuilding;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AlienDefense.EditorTools
{
    /// <summary>The "under construction" state of a plot: a wooden scaffold ring around the future building, a
    /// first-build interior (foundation stubs, mixer, bricks, delivered materials), light dust and sparks, the
    /// shared completion burst, and the restyled construction progress bar.
    ///
    /// Visual only - the construction rules (start, timer, Finish, save) stay in BaseProgressionService. The site
    /// replaces the plot's old cube scaffolding in the slot BaseBuildingView already toggles while a build runs.
    ///
    /// Scaffolding is assembled from the Low Poly Construction pack's Plank A (posts, beams, boards, braces) and
    /// Ladder C, then baked into one mesh so a whole ring is one renderer.</summary>
    public static partial class ConstructionPlotVisualSetupEditor
    {
        public const string SiteRootName = "ConstructionSiteVisual";
        private const string ParticleMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat";
        private const string IconsFolder = "Assets/_Game/Art/Base/Icons";
        private const string MinimapPanelSprite = "Assets/_Game/Art/Sprite/Minimap/T_MinimapPanel.png";
        private const string FontPath = "Assets/_Game/Font/Fredoka-Bold SDF.asset";

        private const float LevelHeight = 2.1f;
        private const float FootprintMargin = 1.2f;

        /// <summary>(prefab name, half extent, levels). Chosen by the building's footprint.</summary>
        private static readonly (string name, float half, int levels)[] SiteSizes =
        {
            ("ConstructionSite_Small", 3.6f, 2),
            (SiteRootName, 5f, 3),
            ("ConstructionSite_Large", 6.3f, 3)
        };

        [MenuItem("Tools/Tower Defense/Base/Setup Construction Sites")]
        public static void SetupConstructionSites()
        {
            RunConstructionSites(rebuild: false);
        }

        [MenuItem("Tools/Tower Defense/Base/Rebuild Construction Sites")]
        public static void RebuildConstructionSites()
        {
            RunConstructionSites(rebuild: true);
        }

        private static void RunConstructionSites(bool rebuild)
        {
            Report.Clear();
            EnsureConstructionSites(rebuild);

            Scene scene = SceneManager.GetSceneByPath(MainMenuPath);
            if (!scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Single);
            }

            GameObject world = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "BaseWorld")
                {
                    world = root;
                }
            }

            if (world == null)
            {
                Debug.LogError("[ConstructionSites] MainMenu has no BaseWorld. Run Setup Base Scene first.");
                return;
            }

            int sites = ApplyConstructionSites(world, scene, rebuild);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Report.Insert(0, $"Construction sites applied to {sites} plot(s); progress bar restyled; completion FX wired.");
            Debug.Log("[ConstructionSites]\n - " + string.Join("\n - ", Report));
        }

        // ------------------------------------------------------------------ Scene

        /// <summary>Puts a fitted construction site on every building plot (replacing whatever filled the view's
        /// scaffolding slot), wires the shared completion FX into the world presenter and restyles the progress
        /// bar of the indicator template. Safe to run repeatedly.</summary>
        public static int ApplyConstructionSites(GameObject world, Scene scene, bool rebuildAssets = false)
        {
            GameObject[] sites = EnsureConstructionSites(rebuildAssets);
            int count = 0;

            foreach (BaseBuildingView view in world.GetComponentsInChildren<BaseBuildingView>(true))
            {
                var so = new SerializedObject(view);
                SerializedProperty slot = so.FindProperty("_scaffoldingRoot");
                var old = slot.objectReferenceValue as GameObject;
                if (old != null && !IsPurelyVisual(old))
                {
                    Report.Add($"{view.name}: '{old.name}' carries scripts - left in place.");
                    continue;
                }

                GameObject site = CreateConstructionSite(view.transform, GetFootprint(view.Definition), sites);
                if (old != null)
                {
                    site.transform.SetSiblingIndex(old.transform.GetSiblingIndex());
                    Object.DestroyImmediate(old);
                }

                slot.objectReferenceValue = site;
                Transform interior = FindDeep(site.transform, "InteriorProps");
                so.FindProperty("_constructionInteriorRoot").objectReferenceValue = interior != null ? interior.gameObject : null;
                so.ApplyModifiedPropertiesWithoutUndo();
                count++;
            }

            WireConstructionExtras(world, scene);
            return count;
        }

        /// <summary>The shared completion FX in the world and the restyled progress bar on the indicator template,
        /// both wired into BaseWorldPresenter.</summary>
        public static void WireConstructionExtras(GameObject world, Scene scene)
        {
            BaseBuildCompleteFx fx = EnsureCompletionFxInstance(world);
            BaseWorldPresenter presenter = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                presenter = presenter != null ? presenter : root.GetComponentInChildren<BaseWorldPresenter>(true);
            }

            if (presenter != null)
            {
                var presenterSo = new SerializedObject(presenter);
                presenterSo.FindProperty("_completionFx").objectReferenceValue = fx;
                SerializedProperty template = presenterSo.FindProperty("_indicatorTemplate");
                presenterSo.ApplyModifiedPropertiesWithoutUndo();

                if (template.objectReferenceValue is BaseBuildingIndicatorView indicator)
                {
                    RebuildIndicatorTimer(indicator);
                }
            }
            else
            {
                Report.Add("No BaseWorldPresenter found - completion FX and progress bar NOT wired.");
            }
        }

        /// <summary>Instantiates the site size that fits the footprint, stretched a little (±25%) to hug it, on the
        /// base layer, static, inactive (BaseBuildingView switches it on while a build runs).</summary>
        public static GameObject CreateConstructionSite(Transform plot, Vector3 footprint, GameObject[] sites = null)
        {
            sites ??= EnsureConstructionSites(false);
            float needX = footprint.x + FootprintMargin * 2f;
            float needZ = footprint.z + FootprintMargin * 2f;
            float need = Mathf.Max(needX, needZ);

            int index = need <= SiteSizes[0].half * 2f + 1f ? 0 : need <= SiteSizes[1].half * 2f + 1.5f ? 1 : 2;
            float half = SiteSizes[index].half;

            var site = (GameObject)PrefabUtility.InstantiatePrefab(sites[index], plot);
            site.name = SiteRootName;
            site.transform.localPosition = Vector3.zero;
            site.transform.localRotation = Quaternion.identity;
            site.transform.localScale = new Vector3(Mathf.Clamp(needX / (half * 2f), 0.75f, 1.25f), 1f,
                Mathf.Clamp(needZ / (half * 2f), 0.75f, 1.25f));

            foreach (Transform t in site.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = BaseLayer;
                // Particle systems move their particles; everything else is static scenery.
                if (t.GetComponent<ParticleSystem>() == null)
                {
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
                }
            }

            site.SetActive(false);
            return site;
        }

        private static Vector3 GetFootprint(BaseBuildingDefinition definition)
        {
            if (definition != null && definition.TryGetLevel(1, out BuildingLevelDefinition level1) && level1.VisualPrefab != null)
            {
                BoxCollider box = level1.VisualPrefab.GetComponent<BoxCollider>();
                if (box != null)
                {
                    return box.size;
                }
            }

            return new Vector3(8f, 6f, 8f);
        }

        private static BaseBuildCompleteFx EnsureCompletionFxInstance(GameObject world)
        {
            BaseBuildCompleteFx existing = world.GetComponentInChildren<BaseBuildCompleteFx>(true);
            if (existing != null)
            {
                return existing;
            }

            Transform fxRoot = world.transform.Find("FX");
            if (fxRoot == null)
            {
                fxRoot = new GameObject("FX").transform;
                fxRoot.SetParent(world.transform, false);
                fxRoot.gameObject.layer = BaseLayer;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(EnsureCompletionFxPrefab(false), fxRoot);
            foreach (Transform t in instance.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = BaseLayer;
            }

            return instance.GetComponent<BaseBuildCompleteFx>();
        }

        // ------------------------------------------------------------------ Site prefabs

        public static GameObject[] EnsureConstructionSites(bool rebuild)
        {
            EnsureAssets(false);
            var result = new GameObject[SiteSizes.Length];
            for (int i = 0; i < SiteSizes.Length; i++)
            {
                (string name, float half, int levels) = SiteSizes[i];
                result[i] = Composite(name, rebuild, root => BuildSite(root, name, half, levels));
            }

            EnsureCompletionFxPrefab(rebuild);
            return result;
        }

        private static void BuildSite(GameObject root, string name, float half, int levels)
        {
            GameObject planks = LowPolyModel("Plank A");
            GameObject ladder = LowPolyModel("Ladder C");
            Material postMaterial = FindEmbeddedMaterial("Brick B", "Brown 2");

            // Foundation dirt the size of the site.
            var foundation = new GameObject("Foundation");
            foundation.transform.SetParent(root.transform, false);
            foundation.transform.localScale = new Vector3(half / 5f, 1f, half / 5f);
            AddDirt(foundation, EnsureDirtMesh("SM_ConstructionDirt_B", 23, false));

            // Scaffolding ring, baked to a single renderer.
            var scaffolding = new GameObject("ScaffoldingRoot");
            scaffolding.transform.SetParent(root.transform, false);
            if (planks != null)
            {
                BuildScaffoldRing(scaffolding.transform, planks, ladder, postMaterial, half, levels);
                BakeToSingleRenderer(scaffolding, MeshesFolder + "/SM_" + name + "_Scaffolding.asset", castShadows: true);
            }
            else
            {
                Report.Add("MISSING ASSET: " + LowPolyFolder + "Plank A.fbx - scaffolding not built.");
            }

            // First-build interior: partial foundation and delivered materials in the (still empty) middle.
            var interior = new GameObject("InteriorProps");
            interior.transform.SetParent(root.transform, false);
            float inner = half - 1.4f;
            PlacePillarStubs(interior.transform, inner * 0.6f);
            PlaceModel(interior.transform, "Cement Mixer", new Vector3(inner * 0.35f, 0f, -inner * 0.45f), 30f, 1.3f);
            PlaceModel(interior.transform, "Brick B", new Vector3(-inner * 0.45f, 0f, -inner * 0.35f), -12f, 1f);
            var materials = new GameObject("MaterialProps");
            materials.transform.SetParent(interior.transform, false);
            Place(AssetDatabase.LoadAssetAtPath<GameObject>(ConstructionFolder + "/Construction_WoodPileSmall.prefab"),
                materials, new Vector3(-inner * 0.55f, 0f, inner * 0.45f), 90f);
            Place(AssetDatabase.LoadAssetAtPath<GameObject>(ConstructionFolder + "/Construction_CratePile.prefab"),
                materials, new Vector3(inner * 0.35f, 0f, inner * 0.5f), -10f);
            if (half > 4f)
            {
                Place(AssetDatabase.LoadAssetAtPath<GameObject>(ConstructionFolder + "/Construction_BarrelGroup.prefab"),
                    materials, new Vector3(inner * 0.7f, 0f, -inner * 0.05f), 20f);
            }

            // Cheap ambient FX: a little dust and an occasional spark while the build runs.
            var fx = new GameObject("ConstructionFX");
            fx.transform.SetParent(root.transform, false);
            BuildDust(fx.transform, half);
            BuildSparks(fx.transform, half, levels);

            ConfigureRenderers(interior, castShadows: false);
        }

        /// <summary>Posts at the corners and along each side, a beam ring and walk boards per level, diagonal
        /// braces, one taller corner post and a ladder - enough irregularity to read as "work in progress".</summary>
        private static void BuildScaffoldRing(Transform parent, GameObject plank, GameObject ladder, Material postMaterial,
            float half, int levels)
        {
            float top = levels * LevelHeight;
            int spans = Mathf.Max(2, Mathf.CeilToInt(half * 2f / 3.6f));
            var corners = new[]
            {
                new Vector3(-half, 0f, -half), new Vector3(half, 0f, -half),
                new Vector3(half, 0f, half), new Vector3(-half, 0f, half)
            };

            for (int side = 0; side < 4; side++)
            {
                Vector3 a = corners[side];
                Vector3 b = corners[(side + 1) % 4];

                // Posts (the next side's first post is this side's last - skip it to avoid doubles).
                for (int s = 0; s < spans; s++)
                {
                    Vector3 p = Vector3.Lerp(a, b, s / (float)spans);
                    float height = top + 0.45f + (side == 2 && s == 0 ? 0.9f : 0f);
                    Plank(parent, plank, p, p + Vector3.up * height, 1.9f, 0.8f, postMaterial);
                }

                // Beam per level; the top beam on the back side is left out, like a ring still being assembled.
                for (int level = 1; level <= levels; level++)
                {
                    if (side == 2 && level == levels)
                    {
                        continue;
                    }

                    Vector3 y = Vector3.up * (level * LevelHeight);
                    Plank(parent, plank, a + y, b + y, 1.3f, 0.6f, null);
                }

                // Walk boards: two planks laid flat just outside the ring, on alternating levels per side.
                int boardLevel = 1 + side % Mathf.Max(1, levels - 1);
                Vector3 outward = Vector3.Cross(Vector3.up, (b - a).normalized);
                for (int k = 0; k < 2; k++)
                {
                    Vector3 offset = outward * (0.25f + k * 0.42f) + Vector3.up * (boardLevel * LevelHeight + 0.14f);
                    Plank(parent, plank, a + offset, b + offset, 0.8f, 1.1f, null);
                }

                // One diagonal brace per side, alternating direction.
                Vector3 braceFrom = side % 2 == 0 ? a : b;
                Vector3 braceTo = Vector3.Lerp(a, b, side % 2 == 0 ? 1f / spans : 1f - 1f / spans) + Vector3.up * LevelHeight;
                Plank(parent, plank, braceFrom + Vector3.up * 0.2f, braceTo, 1f, 0.55f, null);
            }

            if (ladder != null)
            {
                // Leaning against the front-right corner, outside the ring.
                var holder = new GameObject("Ladder");
                holder.transform.SetParent(parent, false);
                holder.transform.localPosition = new Vector3(half + 0.55f, 0f, -half + 1.1f);
                holder.transform.localRotation = Quaternion.Euler(0f, 0f, 10f);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(ladder, holder.transform);
                instance.transform.localScale *= 1.2f;
                GroundAndCentre(instance);
            }
        }

        /// <summary>One Plank A stretched between two points. The plank's long axis is its local X (4.56 m); the
        /// two thickness factors widen it into a post or flatten it into a board.</summary>
        private static void Plank(Transform parent, GameObject plank, Vector3 from, Vector3 to, float thicknessY,
            float thicknessZ, Material overrideMaterial)
        {
            Vector3 direction = to - from;
            var holder = new GameObject("Plank");
            holder.transform.SetParent(parent, false);
            holder.transform.localPosition = (from + to) * 0.5f;
            holder.transform.localRotation = Quaternion.FromToRotation(Vector3.right, direction.normalized);
            holder.transform.localScale = new Vector3(direction.magnitude / 4.56f, thicknessY, thicknessZ);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(plank, holder.transform);
            if (overrideMaterial != null)
            {
                foreach (MeshRenderer renderer in instance.GetComponentsInChildren<MeshRenderer>())
                {
                    renderer.sharedMaterial = overrideMaterial;
                }
            }
        }

        private static void PlacePillarStubs(Transform parent, float offset)
        {
            GameObject pillar = LowPolyModel("Pillar A");
            if (pillar == null)
            {
                return;
            }

            var stubs = new GameObject("FoundationStubs");
            stubs.transform.SetParent(parent, false);
            float[] heights = { 1.2f, 0.7f, 1.5f, 0.9f };
            for (int i = 0; i < 4; i++)
            {
                float x = i % 2 == 0 ? -offset : offset;
                float z = i < 2 ? -offset : offset;
                var holder = new GameObject("Stub_" + i);
                holder.transform.SetParent(stubs.transform, false);
                holder.transform.localPosition = new Vector3(x, 0f, z);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(pillar, holder.transform);
                instance.transform.localScale = Vector3.Scale(instance.transform.localScale, new Vector3(0.7f, 0.7f, heights[i] / 3f));
                GroundAndCentre(instance);
            }
        }

        private static void PlaceModel(Transform parent, string modelName, Vector3 position, float yaw, float scale)
        {
            GameObject model = LowPolyModel(modelName);
            if (model == null)
            {
                return;
            }

            var holder = new GameObject(modelName.Replace(' ', '_'));
            holder.transform.SetParent(parent, false);
            holder.transform.localPosition = position;
            holder.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, holder.transform);
            instance.transform.localScale *= scale;
            GroundAndCentre(instance);
        }

        private static GameObject LowPolyModel(string modelName)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(LowPolyFolder + modelName + ".fbx");
            if (model == null)
            {
                Report.Add("MISSING ASSET: " + LowPolyFolder + modelName + ".fbx");
            }

            return model;
        }

        private static Material FindEmbeddedMaterial(string modelName, string materialName)
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(LowPolyFolder + modelName + ".fbx"))
            {
                if (asset is Material material && material.name == materialName)
                {
                    return material;
                }
            }

            return null;
        }

        // ------------------------------------------------------------------ Particles

        private static Material ParticleMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(ParticleMaterialPath);
            if (material == null)
            {
                Report.Add("MISSING ASSET: " + ParticleMaterialPath + " - particles will render with the default material.");
            }

            return material;
        }

        private static ParticleSystem NewSystem(Transform parent, string name, int maxParticles)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = system.main;
            main.maxParticles = maxParticles;
            main.playOnAwake = true;
            main.loop = true;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = ParticleMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return system;
        }

        private static void BuildDust(Transform parent, float half)
        {
            ParticleSystem dust = NewSystem(parent, "DustParticles", 8);
            ParticleSystem.MainModule main = dust.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.1f, 1.9f);
            main.startColor = new Color(0.82f, 0.7f, 0.52f, 0.35f);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            ParticleSystem.EmissionModule emission = dust.emission;
            emission.rateOverTime = 2.5f;

            ParticleSystem.ShapeModule shape = dust.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(half * 1.6f, 0.2f, half * 1.6f);
            shape.rotation = new Vector3(-90f, 0f, 0f);

            ParticleSystem.ColorOverLifetimeModule color = dust.colorOverLifetime;
            color.enabled = true;
            color.color = FadeInOut(new Color(0.82f, 0.7f, 0.52f));

            ParticleSystem.SizeOverLifetimeModule size = dust.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.2f));
        }

        private static void BuildSparks(Transform parent, float half, int levels)
        {
            ParticleSystem sparks = NewSystem(parent, "SmallSparkParticles", 12);
            sparks.transform.localPosition = new Vector3(-half * 0.5f, LevelHeight + 0.2f, -half);
            ParticleSystem.MainModule main = sparks.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 4.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.28f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.92f, 0.4f), new Color(1f, 0.6f, 0.15f));
            main.gravityModifier = 1.2f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            ParticleSystem.EmissionModule emission = sparks.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0.6f, 4, 4, -1, 1.6f) });

            ParticleSystem.ShapeModule shape = sparks.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 40f;
            shape.radius = 0.1f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
        }

        /// <summary>Golden flash + dust puff + sparkles, all emitted on demand by BaseBuildCompleteFx.</summary>
        private static GameObject EnsureCompletionFxPrefab(bool rebuild)
        {
            string path = ConstructionFolder + "/FX_BuildComplete.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null && !rebuild)
            {
                return existing;
            }

            var root = new GameObject("FX_BuildComplete");

            ParticleSystem flash = NewSystem(root.transform, "GoldenFlash", 2);
            ParticleSystem.MainModule flashMain = flash.main;
            flashMain.startLifetime = 0.45f;
            flashMain.startSpeed = 0f;
            flashMain.startSize = 22f;
            flashMain.startColor = new Color(1f, 0.86f, 0.35f, 0.8f);
            ConfigureBurstOnly(flash);
            ParticleSystem.ColorOverLifetimeModule flashColor = flash.colorOverLifetime;
            flashColor.enabled = true;
            flashColor.color = FadeOut(new Color(1f, 0.86f, 0.35f));
            ParticleSystem.SizeOverLifetimeModule flashSize = flash.sizeOverLifetime;
            flashSize.enabled = true;
            flashSize.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.4f, 1f, 1.2f));

            ParticleSystem dust = NewSystem(root.transform, "DustBurst", 20);
            ParticleSystem.MainModule dustMain = dust.main;
            dustMain.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
            dustMain.startSpeed = new ParticleSystem.MinMaxCurve(3f, 6f);
            dustMain.startSize = new ParticleSystem.MinMaxCurve(2.4f, 4f);
            dustMain.startColor = new Color(0.9f, 0.8f, 0.64f, 0.6f);
            ConfigureBurstOnly(dust);
            ParticleSystem.ShapeModule dustShape = dust.shape;
            dustShape.shapeType = ParticleSystemShapeType.Circle;
            dustShape.radius = 4.5f;
            dustShape.rotation = new Vector3(-90f, 0f, 0f);
            ParticleSystem.ColorOverLifetimeModule dustColor = dust.colorOverLifetime;
            dustColor.enabled = true;
            dustColor.color = FadeOut(new Color(0.9f, 0.8f, 0.64f));

            ParticleSystem sparkles = NewSystem(root.transform, "Sparkles", 30);
            ParticleSystem.MainModule sparkMain = sparkles.main;
            sparkMain.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
            sparkMain.startSpeed = new ParticleSystem.MinMaxCurve(8f, 13f);
            sparkMain.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.1f);
            sparkMain.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 0.5f), new Color(1f, 0.7f, 0.2f));
            sparkMain.gravityModifier = 0.9f;
            ConfigureBurstOnly(sparkles);
            ParticleSystem.ShapeModule sparkShape = sparkles.shape;
            sparkShape.shapeType = ParticleSystemShapeType.Hemisphere;
            sparkShape.radius = 1.5f;
            sparkShape.rotation = new Vector3(-90f, 0f, 0f);

            var fx = root.AddComponent<BaseBuildCompleteFx>();
            var so = new SerializedObject(fx);
            SerializedProperty systems = so.FindProperty("_systems");
            SerializedProperty counts = so.FindProperty("_counts");
            SerializedProperty raised = so.FindProperty("_raised");
            ParticleSystem[] all = { flash, dust, sparkles };
            int[] amounts = { 1, 16, 22 };
            bool[] atRoof = { true, false, true };
            systems.arraySize = all.Length;
            counts.arraySize = all.Length;
            raised.arraySize = all.Length;
            for (int i = 0; i < all.Length; i++)
            {
                systems.GetArrayElementAtIndex(i).objectReferenceValue = all[i];
                counts.GetArrayElementAtIndex(i).intValue = amounts[i];
                raised.GetArrayElementAtIndex(i).boolValue = atRoof[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return SavePrefab(root, path);
        }

        /// <summary>No emission of its own: the system just runs, and BaseBuildCompleteFx emits into it.</summary>
        private static void ConfigureBurstOnly(ParticleSystem system)
        {
            ParticleSystem.MainModule main = system.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;
        }

        private static Gradient FadeInOut(Color color)
        {
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(0f, 1f) });
            return gradient;
        }

        private static Gradient FadeOut(Color color)
        {
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            return gradient;
        }

        // ------------------------------------------------------------------ Progress bar

        /// <summary>Replaces the indicator template's timer with the construction bar: dark navy rounded
        /// background, swinging hammer on the left, yellow-orange fill in the middle, whole seconds on the right.
        /// Screen-space like the rest of the indicator (the base already projects widgets onto its overlay canvas),
        /// so no world-space canvas is added.</summary>
        public static void RebuildIndicatorTimer(BaseBuildingIndicatorView view)
        {
            Transform template = view.transform;
            Transform old = template.Find("Timer");
            int sibling = old != null ? old.GetSiblingIndex() : template.childCount;
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            Sprite panel = AssetDatabase.LoadAssetAtPath<Sprite>(MinimapPanelSprite);

            RectTransform timer = UiRect("Timer", template, new Vector2(0f, -74f), new Vector2(272f, 66f));
            timer.SetSiblingIndex(sibling);
            var group = timer.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            Image background = timer.gameObject.AddComponent<Image>();
            background.sprite = panel;
            background.type = Image.Type.Sliced;
            background.color = new Color(0.1f, 0.16f, 0.3f, 0.95f);
            background.raycastTarget = false;

            RectTransform hammerRect = UiRect("HammerIcon", timer, new Vector2(-104f, 4f), new Vector2(60f, 60f));
            hammerRect.pivot = new Vector2(0.35f, 0.2f);
            Image hammer = hammerRect.gameObject.AddComponent<Image>();
            hammer.sprite = EnsureHammerIcon();
            hammer.preserveAspect = true;
            hammer.raycastTarget = false;

            RectTransform track = UiRect("Track", timer, new Vector2(6f, 0f), new Vector2(128f, 26f));
            Image trackImage = track.gameObject.AddComponent<Image>();
            trackImage.sprite = panel;
            trackImage.type = Image.Type.Sliced;
            trackImage.color = new Color(0.04f, 0.07f, 0.14f, 1f);
            trackImage.raycastTarget = false;

            RectTransform fillRect = UiRect("Fill", timer, new Vector2(6f, 0f), new Vector2(124f, 22f));
            Image fill = fillRect.gameObject.AddComponent<Image>();
            fill.sprite = EnsureProgressFillSprite();
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0.4f;
            fill.raycastTarget = false;

            RectTransform textRect = UiRect("TimeText", timer, new Vector2(102f, 1f), new Vector2(76f, 54f));
            var text = textRect.gameObject.AddComponent<TextMeshProUGUI>();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font != null)
            {
                text.font = font;
            }

            text.text = "5s";
            text.fontSize = 34f;
            text.enableAutoSizing = true;
            text.fontSizeMin = 20f;
            text.fontSizeMax = 34f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;

            timer.gameObject.SetActive(false);

            var so = new SerializedObject(view);
            so.FindProperty("_timerRoot").objectReferenceValue = timer.gameObject;
            so.FindProperty("_timerFill").objectReferenceValue = fill;
            so.FindProperty("_timerText").objectReferenceValue = text;
            so.FindProperty("_timerGroup").objectReferenceValue = group;
            so.FindProperty("_timerIcon").objectReferenceValue = hammerRect;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static RectTransform UiRect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        /// <summary>No hammer icon ships with the project: render one from the Low Poly Construction hammer.</summary>
        private static Sprite EnsureHammerIcon()
        {
            string path = IconsFolder + "/T_Icon_Hammer.png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null)
            {
                return sprite;
            }

            GameObject hammer = LowPolyModel("Hammer");
            if (hammer == null)
            {
                return null;
            }

            var root = new GameObject("HammerIconRoot");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(hammer, root.transform);
            instance.transform.localRotation = Quaternion.Euler(-35f, 0f, 0f) * instance.transform.localRotation;
            sprite = BaseSceneSetupTool.RenderIcon(root, path, 128);
            Report.Add("Hammer icon rendered from Low Poly Construction/Hammer.fbx -> " + path);
            return sprite;
        }

        /// <summary>A capsule with a baked yellow-to-orange gradient and a soft highlight, used as a Filled image.</summary>
        private static Sprite EnsureProgressFillSprite()
        {
            string path = TexturesFolder + "/T_UI_ConstructionFill.png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null)
            {
                return sprite;
            }

            const int width = 128;
            const int height = 32;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color top = new Color(1f, 0.87f, 0.32f);
            Color bottom = new Color(0.98f, 0.58f, 0.12f);
            float radius = height * 0.5f;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, radius, width - radius);
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, radius));
                    float alpha = Mathf.Clamp01(radius - distance + 0.5f);
                    float t = y / (float)(height - 1);
                    Color c = Color.Lerp(bottom, top, t);
                    if (y > height * 0.62f && y < height * 0.8f)
                    {
                        c = Color.Lerp(c, Color.white, 0.35f);
                    }

                    c.a = alpha;
                    texture.SetPixel(x, y, c);
                }
            }

            texture.Apply();
            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
