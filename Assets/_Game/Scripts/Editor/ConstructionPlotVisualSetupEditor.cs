using System.Collections.Generic;
using AlienDefense.Base;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace AlienDefense.EditorTools
{
    /// <summary>Turns the Base's empty building plots into "materials delivered" construction sites, built from the
    /// Low Poly Construction and Majadroid House Construction Site packs.
    ///
    /// Visual only. Each plot keeps its root, BaseBuildingView, tap BoxCollider, Scaffolding, BuildingRoot and
    /// IndicatorAnchor untouched; only the old decorative EmptyPlot child (dirt + picket fence, no scripts) is
    /// replaced by a ConstructionVisualRoot, which BaseBuildingView toggles through its existing empty-plot slot.
    ///
    /// Neither pack ships prefabs (FBX only), so every model is wrapped once in a normalised prop prefab (pivot on
    /// the ground, scale matched to the base) under Assets/_Game/Prefabs/Base/Construction/Props. Third-party files
    /// are never edited. The two pieces neither pack has - a triangular warning sign and a rope-and-post boundary -
    /// are built from tiny generated meshes/primitives with shared materials.</summary>
    public static partial class ConstructionPlotVisualSetupEditor
    {
        private const string LowPolyFolder = "Assets/Low Poly Construction/Low Poly Construction/";
        private const string MajadroidFolder = "Assets/LowPoly-House-Construction-Site-By-Majadroid/LowPoly-House-Construction-Site-By-Majadroid/";
        private const string MajadroidMaterialsFbx = MajadroidFolder + "fbx files/Construction-Materials.fbx";
        private const string MajadroidPalette = MajadroidFolder + "ImphenziaPalette01-256-Gradient.png";

        private const string ConstructionFolder = "Assets/_Game/Prefabs/Base/Construction";
        private const string PropsFolder = ConstructionFolder + "/Props";
        private const string MaterialsFolder = "Assets/_Game/Art/Base/Materials";
        private const string MeshesFolder = "Assets/_Game/Art/Base/Meshes";
        private const string TexturesFolder = "Assets/_Game/Art/Base/Textures";
        private const string MainMenuPath = "Assets/_Game/Scenes/Menu/MainMenu.unity";

        public const string VisualRootName = "ConstructionVisualRoot";
        private const int BaseLayer = 17;

        /// <summary>Real-world-scale packs read too small from the base camera; everything is enlarged a little.</summary>
        private const float PropScale = 1.3f;

        private const float BoundaryHalf = 5.4f;

        /// <summary>Per plot: which variant and how the whole visual is turned, so neighbours never repeat.</summary>
        private static readonly (int variant, float yaw)[] Assignments =
        {
            (0, 0f), (1, 90f), (2, 180f), (0, 270f), (1, 0f), (2, 90f), (0, 180f), (1, 270f)
        };

        private static readonly List<string> Report = new List<string>();

        private static Material _palette;
        private static Material _dirt;
        private static Material _postWood;
        private static Material _rope;
        private static Material _signRed;
        private static Material _signYellow;
        private static Material _signDark;
        private static Material _signPole;

        [MenuItem("Tools/Tower Defense/Base/Setup Construction Plot Visuals")]
        public static void Setup()
        {
            Run(rebuildAssets: false);
        }

        [MenuItem("Tools/Tower Defense/Base/Rebuild Construction Plot Visuals")]
        public static void Rebuild()
        {
            Run(rebuildAssets: true);
        }

        private static void Run(bool rebuildAssets)
        {
            Report.Clear();
            EnsureAssets(rebuildAssets);

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
                Debug.LogError("[ConstructionPlotVisuals] MainMenu has no BaseWorld. Run Tools > Tower Defense > Base > " +
                    "Setup Base Scene first.");
                return;
            }

            int replaced = ApplyToWorld(world);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Report.Insert(0, $"Construction visuals applied to {replaced} plot(s).");
            Debug.Log("[ConstructionPlotVisuals]\n - " + string.Join("\n - ", Report));
        }

        // ------------------------------------------------------------------ Scene

        /// <summary>Swaps every empty-plot visual in the base for a construction-site variant. Idempotent: a plot
        /// that already has a ConstructionVisualRoot is replaced by a fresh one in the same slot.</summary>
        public static int ApplyToWorld(GameObject world)
        {
            GameObject[] variants = EnsureAssets(false);
            int index = 0;

            // Building plots: replace only the view's empty-plot visual.
            foreach (BaseBuildingView view in world.GetComponentsInChildren<BaseBuildingView>(true))
            {
                var so = new SerializedObject(view);
                SerializedProperty slot = so.FindProperty("_emptyPlotRoot");
                var old = slot.objectReferenceValue as GameObject;
                if (old != null && !IsPurelyVisual(old))
                {
                    Report.Add($"{view.name}: '{old.name}' carries scripts - left in place, visual NOT replaced.");
                    index++;
                    continue;
                }

                GameObject visual = CreateVisual(view.transform, index++, variants);
                if (old != null)
                {
                    visual.transform.SetSiblingIndex(old.transform.GetSiblingIndex());
                    Object.DestroyImmediate(old);
                }

                slot.objectReferenceValue = visual;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // Decorative expansion plots: whole child replaced, position kept.
            Transform expansions = FindDeep(world.transform, "ExpansionPlots");
            if (expansions != null)
            {
                var children = new List<Transform>();
                foreach (Transform child in expansions)
                {
                    children.Add(child);
                }

                foreach (Transform child in children)
                {
                    if (!IsPurelyVisual(child.gameObject))
                    {
                        Report.Add($"ExpansionPlots/{child.name} carries scripts - skipped.");
                        continue;
                    }

                    var holder = new GameObject("ExpansionPlot_" + index);
                    holder.transform.SetParent(expansions, false);
                    holder.transform.position = child.position;
                    holder.transform.SetSiblingIndex(child.GetSiblingIndex());
                    CreateVisual(holder.transform, index++, variants);
                    holder.layer = BaseLayer;
                    GameObjectUtility.SetStaticEditorFlags(holder, StaticEditorFlags.BatchingStatic);
                    Object.DestroyImmediate(child.gameObject);
                }
            }

            return index;
        }

        /// <summary>Instantiates variant (index) under the plot, turned per the assignment table, with its warning
        /// sign re-aimed at the base camera so it always reads from the default view.</summary>
        public static GameObject CreateVisual(Transform plot, int index, GameObject[] variants = null)
        {
            variants ??= EnsureAssets(false);
            (int variant, float yaw) = Assignments[index % Assignments.Length];

            var visual = (GameObject)PrefabUtility.InstantiatePrefab(variants[variant], plot);
            visual.name = VisualRootName;
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            // The base camera looks along +X+Z; a sign whose face (-Z) points back along that axis faces the player.
            Transform sign = FindDeep(visual.transform, "Construction_WarningSign");
            if (sign != null)
            {
                sign.rotation = Quaternion.Euler(0f, 45f + (index % 2 == 0 ? -12f : 10f), 0f);
            }

            foreach (Transform t in visual.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = BaseLayer;
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
            }

            foreach (Collider c in visual.GetComponentsInChildren<Collider>(true))
            {
                c.enabled = false;
            }

            return visual;
        }

        private static bool IsPurelyVisual(GameObject root)
        {
            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour != null)
                {
                    return false;
                }
            }

            return true;
        }

        // ------------------------------------------------------------------ Assets

        /// <summary>Creates (or, when rebuilding, regenerates) materials, meshes, prop wrappers, composites and the
        /// three plot variants. Returns the variants A, B, C.</summary>
        public static GameObject[] EnsureAssets(bool rebuild)
        {
            EnsureFolder(PropsFolder);
            EnsureFolder(MaterialsFolder);
            EnsureFolder(MeshesFolder);
            EnsureFolder(TexturesFolder);

            EnsureMaterials();

            // Props: pack models wrapped with the pivot on the ground and a game-scale factor.
            var lp = new Dictionary<string, GameObject>
            {
                ["Plank Holder"] = LowPolyProp("Plank Holder", 1f, rebuild, castShadows: true),
                ["Plank A"] = LowPolyProp("Plank A", 1.1f, rebuild, castShadows: false),
                ["Box A"] = LowPolyProp("Box A", PropScale, rebuild, castShadows: true),
                ["Box C"] = LowPolyProp("Box C", 0.8f, rebuild, castShadows: true),
                ["Barrel A"] = LowPolyProp("Barrel A", PropScale, rebuild, castShadows: true),
                ["Cone A"] = LowPolyProp("Cone A", PropScale, rebuild, castShadows: false),
                ["Wood Stand"] = LowPolyProp("Wood Stand", PropScale, rebuild, castShadows: false),
                ["Cement Bag A"] = LowPolyProp("Cement Bag A", PropScale, rebuild, castShadows: false),
                ["Spool A"] = LowPolyProp("Spool A", 1f, rebuild, castShadows: false),
                ["Pallet"] = LowPolyProp("Pallet", 1f, rebuild, castShadows: false),
                ["Scaffolding A"] = LowPolyProp("Scaffolding A", 1f, rebuild, castShadows: true)
            };

            var mj = new Dictionary<string, GameObject>
            {
                ["Planks Blue"] = MajadroidProp("Planks Blue", 0.8f, rebuild, castShadows: true),
                ["Planks Wood V1"] = MajadroidProp("Planks Wood V1", 0.9f, rebuild, castShadows: true),
                ["Planks Wood V3"] = MajadroidProp("Planks Wood V3", 0.9f, rebuild, castShadows: true),
                ["Barrel"] = MajadroidProp("Barrel", 1.05f, rebuild, castShadows: true),
                ["Box Stack Brown"] = MajadroidProp("Box Stack Brown", 1.1f, rebuild, castShadows: true)
            };

            // Reusable groups.
            GameObject woodPile = Composite("Construction_WoodPile", rebuild, root =>
            {
                Place(lp["Plank Holder"], root, new Vector3(0f, 0f, 0f), 0f);
                Place(lp["Plank A"], root, new Vector3(0.2f, 0f, -1.45f), 4f);
                Place(lp["Plank A"], root, new Vector3(-0.3f, 0f, -1.8f), -7f);
            });

            GameObject woodPileSmall = Composite("Construction_WoodPileSmall", rebuild, root =>
            {
                Place(mj["Planks Wood V1"], root, new Vector3(0f, 0f, 0f), 0f);
                Place(mj["Planks Wood V3"], root, new Vector3(0.15f, 0.54f, -0.2f), 9f);
            });

            GameObject metalPile = Composite("Construction_MetalPile", rebuild, root =>
            {
                Place(mj["Planks Blue"], root, new Vector3(-0.68f, 0f, 0f), 0f);
                Place(mj["Planks Blue"], root, new Vector3(0.7f, 0f, 0.25f), 6f);
            });

            GameObject cratePile = Composite("Construction_CratePile", rebuild, root =>
            {
                Place(lp["Box C"], root, new Vector3(0f, 0f, 0f), 0f);
                Place(lp["Box A"], root, new Vector3(1.75f, 0f, -0.35f), 16f);
            });

            GameObject barrelGroup = Composite("Construction_BarrelGroup", rebuild, root =>
            {
                Place(mj["Barrel"], root, new Vector3(0f, 0f, 0f), 0f);
                Place(mj["Barrel"], root, new Vector3(1.15f, 0f, 0.35f), 25f);
            });

            GameObject barrelGroupB = Composite("Construction_BarrelGroupB", rebuild, root =>
            {
                Place(lp["Barrel A"], root, new Vector3(0f, 0f, 0f), 0f);
                Place(lp["Barrel A"], root, new Vector3(1.6f, 0f, -0.25f), 40f);
            });

            // Built from small pieces, then baked into one mesh / one renderer each (a submesh per material), so
            // the boundary and sign cost one object per plot instead of dozens.
            GameObject sign = Composite("Construction_WarningSign", rebuild, root =>
            {
                BuildWarningSign(root);
                BakeToSingleRenderer(root, MeshesFolder + "/SM_ConstructionWarningSign.asset", castShadows: true);
            });
            GameObject fence = Composite("Construction_RopeFence", rebuild, root =>
            {
                BuildRopeFence(root, BoundaryHalf);
                BakeToSingleRenderer(root, MeshesFolder + "/SM_ConstructionRopeFence.asset", castShadows: false);
            });

            Mesh dirtA = EnsureDirtMesh("SM_ConstructionDirt_A", 11, rebuild);
            Mesh dirtB = EnsureDirtMesh("SM_ConstructionDirt_B", 23, rebuild);
            Mesh dirtC = EnsureDirtMesh("SM_ConstructionDirt_C", 37, rebuild);

            // Variants: ~40-55% of the middle left open for the future building; props hug the edges and corners.
            GameObject a = Composite("ConstructionPlot_Visual_A", rebuild, root =>
            {
                AddDirt(root, dirtA);
                Place(fence, root, Vector3.zero, 0f);
                Place(sign, root, new Vector3(-4.3f, 0f, -4.3f), 0f);
                Place(cratePile, root, new Vector3(2.6f, 0f, 3.7f), -12f);
                Place(woodPile, root, new Vector3(3.9f, 0f, -1.3f), 90f);
                Place(metalPile, root, new Vector3(-3.8f, 0f, 1.1f), -6f);
                Place(barrelGroup, root, new Vector3(0.2f, 0f, -4.4f), 10f);
                Place(lp["Cone A"], root, new Vector3(-1.7f, 0f, -4.7f), 0f);
            });

            GameObject b = Composite("ConstructionPlot_Visual_B", rebuild, root =>
            {
                AddDirt(root, dirtB);
                Place(fence, root, Vector3.zero, 0f);
                Place(sign, root, new Vector3(4.3f, 0f, -4.3f), 0f);
                Place(barrelGroupB, root, new Vector3(-4.3f, 0f, -3.9f), -15f);
                Place(cratePile, root, new Vector3(-3.3f, 0f, 3.9f), 12f);
                Place(woodPileSmall, root, new Vector3(4.2f, 0f, 1.2f), -5f);
                Place(lp["Scaffolding A"], root, new Vector3(0.9f, 0f, 4.5f), 90f);
                Place(lp["Wood Stand"], root, new Vector3(1.3f, 0f, -4.4f), 18f);
                Place(lp["Cement Bag A"], root, new Vector3(-1.5f, 0f, -4.6f), -10f);
            });

            GameObject c = Composite("ConstructionPlot_Visual_C", rebuild, root =>
            {
                AddDirt(root, dirtC);
                Place(fence, root, Vector3.zero, 0f);
                Place(sign, root, new Vector3(-4.3f, 0f, 4.3f), 0f);
                Place(woodPile, root, new Vector3(2.3f, 0f, 4.2f), 0f);
                Place(woodPileSmall, root, new Vector3(4.3f, 0f, 0.5f), 4f);
                Place(metalPile, root, new Vector3(-3.8f, 0f, -0.4f), 8f);
                Place(mj["Box Stack Brown"], root, new Vector3(3.8f, 0f, -3.9f), 20f);
                Place(lp["Spool A"], root, new Vector3(0.3f, 0f, -4.5f), 0f);
                Place(lp["Pallet"], root, new Vector3(-3.6f, 0f, -4.1f), 8f);
                Place(lp["Cone A"], root, new Vector3(-1.3f, 0f, 4.8f), 0f);
            });

            return new[] { a, b, c };
        }

        // ------------------------------------------------------------------ Props

        private static GameObject LowPolyProp(string modelName, float scale, bool rebuild, bool castShadows)
        {
            string path = PropsFolder + "/CP_LP_" + modelName.Replace(' ', '_') + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null && !rebuild)
            {
                return existing;
            }

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(LowPolyFolder + modelName + ".fbx");
            if (model == null)
            {
                Report.Add("MISSING ASSET: " + LowPolyFolder + modelName + ".fbx");
                return null;
            }

            var root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(path));
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            instance.transform.localScale *= scale;
            GroundAndCentre(instance);
            ConfigureRenderers(root, castShadows);
            return SavePrefab(root, path);
        }

        /// <summary>The Majadroid materials FBX is one file with every prop as a child; each prop is rebuilt from
        /// its mesh sub-asset with the palette material the FBX forgot to reference.</summary>
        private static GameObject MajadroidProp(string childName, float scale, bool rebuild, bool castShadows)
        {
            string path = PropsFolder + "/CP_MJ_" + childName.Replace(' ', '_') + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null && !rebuild)
            {
                return existing;
            }

            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(MajadroidMaterialsFbx);
            Transform source = fbx != null ? fbx.transform.Find(childName) : null;
            MeshFilter sourceFilter = source != null ? source.GetComponent<MeshFilter>() : null;
            if (sourceFilter == null)
            {
                Report.Add("MISSING ASSET: " + MajadroidMaterialsFbx + " > " + childName);
                return null;
            }

            var root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(path));
            var model = new GameObject("Model");
            model.transform.SetParent(root.transform, false);
            model.transform.localRotation = source.localRotation;
            model.transform.localScale = source.localScale * scale;
            model.AddComponent<MeshFilter>().sharedMesh = sourceFilter.sharedMesh;
            model.AddComponent<MeshRenderer>().sharedMaterial = _palette;
            GroundAndCentre(model);
            ConfigureRenderers(root, castShadows);
            return SavePrefab(root, path);
        }

        private static void ConfigureRenderers(GameObject root, bool castShadows)
        {
            foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                renderer.receiveShadows = castShadows;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }

            // Decoration never blocks taps or physics. Components inside a nested pack instance cannot be removed
            // from here, so those are only switched off.
            foreach (Collider c in root.GetComponentsInChildren<Collider>(true))
            {
                if (PrefabUtility.IsPartOfPrefabInstance(c))
                {
                    c.enabled = false;
                }
                else
                {
                    Object.DestroyImmediate(c);
                }
            }
        }

        // ------------------------------------------------------------------ Generated pieces

        private static void BuildWarningSign(GameObject root)
        {
            // Everything authored at 1 m and scaled up once, so the sign reads from the base camera.
            var body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = Vector3.one * 2.1f;

            GameObject pole = Primitive(PrimitiveType.Cylinder, "Pole", body.transform, _signPole, false);
            pole.transform.localPosition = new Vector3(0f, 0.62f, 0.03f);
            pole.transform.localScale = new Vector3(0.07f, 0.62f, 0.07f);

            Mesh triangle = EnsureTriangleMesh();
            var outer = new GameObject("Triangle");
            outer.transform.SetParent(body.transform, false);
            outer.transform.localPosition = new Vector3(0f, 1.05f, 0f);
            outer.AddComponent<MeshFilter>().sharedMesh = triangle;
            var outerRenderer = outer.AddComponent<MeshRenderer>();
            outerRenderer.sharedMaterial = _signRed;

            var inner = new GameObject("TriangleInner");
            inner.transform.SetParent(body.transform, false);
            inner.transform.localPosition = new Vector3(0f, 1.12f, -0.025f);
            inner.transform.localScale = new Vector3(0.72f, 0.72f, 1f);
            inner.AddComponent<MeshFilter>().sharedMesh = triangle;
            inner.AddComponent<MeshRenderer>().sharedMaterial = _signYellow;

            GameObject bar = Primitive(PrimitiveType.Cube, "Mark", body.transform, _signDark, false);
            bar.transform.localPosition = new Vector3(0f, 1.42f, -0.04f);
            bar.transform.localScale = new Vector3(0.075f, 0.24f, 0.02f);
            GameObject dot = Primitive(PrimitiveType.Cube, "Dot", body.transform, _signDark, false);
            dot.transform.localPosition = new Vector3(0f, 1.23f, -0.04f);
            dot.transform.localScale = new Vector3(0.075f, 0.075f, 0.02f);

            ConfigureRenderers(root, castShadows: false);
            outerRenderer.shadowCastingMode = ShadowCastingMode.On;
        }

        /// <summary>Low temporary boundary: wooden posts at the corners and mid-sides, one rope between each pair.</summary>
        private static void BuildRopeFence(GameObject root, float half)
        {
            var corners = new[]
            {
                new Vector3(-half, 0f, -half), new Vector3(0f, 0f, -half - 0.15f), new Vector3(half, 0f, -half),
                new Vector3(half + 0.1f, 0f, 0f), new Vector3(half, 0f, half), new Vector3(0f, 0f, half + 0.12f),
                new Vector3(-half, 0f, half), new Vector3(-half - 0.1f, 0f, 0f)
            };

            const float postHeight = 1.15f;
            const float ropeHeight = 0.92f;
            for (int i = 0; i < corners.Length; i++)
            {
                GameObject post = Primitive(PrimitiveType.Cylinder, "Post_" + i, root.transform, _postWood, false);
                post.transform.localPosition = corners[i] + new Vector3(0f, postHeight * 0.5f, 0f);
                post.transform.localScale = new Vector3(0.26f, postHeight * 0.5f, 0.26f);

                Vector3 from = corners[i] + Vector3.up * ropeHeight;
                Vector3 to = corners[(i + 1) % corners.Length] + Vector3.up * ropeHeight;
                // A slight sag: the rope's middle sits lower than the posts.
                Vector3 mid = (from + to) * 0.5f + Vector3.down * 0.12f;
                Rope(root.transform, "Rope_" + i + "a", from, mid);
                Rope(root.transform, "Rope_" + i + "b", mid, to);
            }
        }

        private static void Rope(Transform parent, string name, Vector3 from, Vector3 to)
        {
            // A thin box: 12 triangles instead of a cylinder's 80, and indistinguishable at base-camera distance.
            GameObject rope = Primitive(PrimitiveType.Cube, name, parent, _rope, false);
            Vector3 direction = to - from;
            rope.transform.localPosition = (from + to) * 0.5f;
            rope.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);
            rope.transform.localScale = new Vector3(0.09f, direction.magnitude, 0.09f);
        }

        private static void AddDirt(GameObject root, Mesh mesh)
        {
            var dirt = new GameObject("DirtGround");
            dirt.transform.SetParent(root.transform, false);
            dirt.transform.localPosition = new Vector3(0f, 0.035f, 0f);
            dirt.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = dirt.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _dirt;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        /// <summary>A flat rounded-square patch with a wobbly outline (fixed seed per variant), so the site edge is
        /// irregular without leaving the plot footprint.</summary>
        private static Mesh EnsureDirtMesh(string name, int seed, bool rebuild)
        {
            string path = MeshesFolder + "/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null && !rebuild)
            {
                return existing;
            }

            const int segments = 64;
            const float half = 5.75f;
            var vertices = new Vector3[segments + 1];
            var uvs = new Vector2[segments + 1];
            var normals = new Vector3[segments + 1];
            var triangles = new int[segments * 3];

            vertices[0] = Vector3.zero;
            uvs[0] = new Vector2(0.5f, 0.5f);
            normals[0] = Vector3.up;
            float s = seed * 0.37f;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                // Superellipse (n = 5) = square with rounded corners, then a low-frequency wobble.
                float squareness = Mathf.Pow(Mathf.Pow(Mathf.Abs(cos), 5f) + Mathf.Pow(Mathf.Abs(sin), 5f), -1f / 5f);
                float wobble = 0.22f * Mathf.Sin(3f * angle + s) + 0.15f * Mathf.Sin(7f * angle + 2f * s) +
                    0.09f * Mathf.Sin(13f * angle + 3f * s);
                float radius = half * squareness * 0.97f + wobble;
                var v = new Vector3(cos * radius, 0f, sin * radius);
                vertices[i + 1] = v;
                uvs[i + 1] = new Vector2(v.x / (half * 2.4f) + 0.5f, v.z / (half * 2.4f) + 0.5f);
                normals[i + 1] = Vector3.up;

                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = 1 + (i + 1) % segments;
                triangles[i * 3 + 2] = 1 + i;
            }

            Mesh mesh = existing != null ? existing : new Mesh();
            mesh.Clear();
            mesh.name = name;
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.normals = normals;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();

            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
            }
            else
            {
                EditorUtility.SetDirty(mesh);
            }

            return mesh;
        }

        /// <summary>Double-sided flat triangle, 1 m wide, apex up, lying in the XY plane with its base at y = 0.</summary>
        private static Mesh EnsureTriangleMesh()
        {
            string path = MeshesFolder + "/SM_WarningTriangle.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                return existing;
            }

            float h = Mathf.Sqrt(3f) * 0.5f;
            var bl = new Vector3(-0.5f, 0f, 0f);
            var apex = new Vector3(0f, h, 0f);
            var br = new Vector3(0.5f, 0f, 0f);
            var mesh = new Mesh
            {
                name = "SM_WarningTriangle",
                vertices = new[] { bl, apex, br, bl, apex, br },
                normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.forward, Vector3.forward, Vector3.forward },
                uv = new[] { Vector2.zero, new Vector2(0.5f, 1f), Vector2.right, Vector2.zero, new Vector2(0.5f, 1f), Vector2.right },
                triangles = new[] { 0, 1, 2, 5, 4, 3 }
            };
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        // ------------------------------------------------------------------ Materials

        private static void EnsureMaterials()
        {
            var paletteTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(MajadroidPalette);
            if (paletteTexture == null)
            {
                Report.Add("MISSING ASSET: " + MajadroidPalette + " - Majadroid props will render untextured.");
            }

            _palette = EnsureMaterial("MAT_Construction_MajadroidPalette", Color.white, 0.1f, paletteTexture);
            _dirt = EnsureMaterial("MAT_Construction_Dirt", Color.white, 0.05f, EnsureDirtTexture());
            _postWood = EnsureMaterial("MAT_Construction_PostWood", Hex("#8A5A2E"), 0.1f, null);
            _rope = EnsureMaterial("MAT_Construction_Rope", Hex("#E9C46A"), 0.1f, null);
            _signRed = EnsureMaterial("MAT_Construction_SignRed", Hex("#E4572E"), 0.2f, null);
            _signYellow = EnsureMaterial("MAT_Construction_SignYellow", Hex("#FFC93C"), 0.2f, null);
            _signDark = EnsureMaterial("MAT_Construction_SignDark", Hex("#2B2B2B"), 0.1f, null);
            _signPole = EnsureMaterial("MAT_Construction_SignPole", Hex("#9AA3AD"), 0.3f, null);
        }

        private static Material EnsureMaterial(string name, Color color, float smoothness, Texture texture)
        {
            string path = MaterialsFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.enableInstancing = true;
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0f);
            if (texture != null)
            {
                material.SetTexture("_BaseMap", texture);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>128 px dirt: two browns blended by value noise plus a few pebbles - enough variation to stop the
        /// patch reading as a flat brown square.</summary>
        private static Texture2D EnsureDirtTexture()
        {
            string path = TexturesFolder + "/T_ConstructionDirt.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null)
            {
                return existing;
            }

            const int size = 128;
            var light = Hex("#B9875A");
            var mid = Hex("#9C6B43");
            var dark = Hex("#7E5334");
            var texture = new Texture2D(size, size, TextureFormat.RGB24, false);
            var random = new System.Random(5);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float n = Mathf.PerlinNoise(x * 0.045f + 3.1f, y * 0.045f + 7.7f) * 0.7f +
                        Mathf.PerlinNoise(x * 0.14f + 11f, y * 0.14f + 2f) * 0.3f;
                    Color c = n < 0.5f ? Color.Lerp(dark, mid, n * 2f) : Color.Lerp(mid, light, (n - 0.5f) * 2f);
                    texture.SetPixel(x, y, c);
                }
            }

            for (int i = 0; i < 60; i++)
            {
                int px = random.Next(size);
                int py = random.Next(size);
                Color pebble = random.NextDouble() > 0.5 ? Hex("#C9A27A") : Hex("#6E4A2F");
                texture.SetPixel(px, py, pebble);
                texture.SetPixel((px + 1) % size, py, pebble);
            }

            texture.Apply();
            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 128;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ------------------------------------------------------------------ Helpers

        private static GameObject Composite(string name, bool rebuild, System.Action<GameObject> build)
        {
            string path = ConstructionFolder + "/" + name + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null && !rebuild)
            {
                return existing;
            }

            var root = new GameObject(name);
            build(root);
            return SavePrefab(root, path);
        }

        private static void Place(GameObject prefab, GameObject parent, Vector3 position, float yaw)
        {
            if (prefab == null)
            {
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.transform);
            instance.transform.localPosition = position;
            instance.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }

        /// <summary>Merges every mesh under root into one mesh asset (one submesh per material), replaces the
        /// children with a single MeshFilter/MeshRenderer on root, and saves the mesh so the prefab can reference it.</summary>
        private static void BakeToSingleRenderer(GameObject root, string meshPath, bool castShadows)
        {
            var byMaterial = new Dictionary<Material, List<CombineInstance>>();
            var order = new List<Material>();
            Matrix4x4 toRoot = root.transform.worldToLocalMatrix;
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null || filter.sharedMesh == null)
                {
                    continue;
                }

                Material material = renderer.sharedMaterial;
                if (!byMaterial.TryGetValue(material, out List<CombineInstance> list))
                {
                    list = new List<CombineInstance>();
                    byMaterial[material] = list;
                    order.Add(material);
                }

                list.Add(new CombineInstance
                {
                    mesh = filter.sharedMesh,
                    transform = toRoot * filter.transform.localToWorldMatrix
                });
            }

            var parts = new CombineInstance[order.Count];
            var temporary = new List<Mesh>();
            for (int i = 0; i < order.Count; i++)
            {
                var part = new Mesh();
                part.CombineMeshes(byMaterial[order[i]].ToArray(), mergeSubMeshes: true, useMatrices: true);
                temporary.Add(part);
                parts[i] = new CombineInstance { mesh = part, transform = Matrix4x4.identity };
            }

            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            Mesh baked = existing != null ? existing : new Mesh();
            baked.Clear();
            baked.name = System.IO.Path.GetFileNameWithoutExtension(meshPath);
            baked.CombineMeshes(parts, mergeSubMeshes: false, useMatrices: false);
            baked.RecalculateBounds();
            if (existing == null)
            {
                AssetDatabase.CreateAsset(baked, meshPath);
            }
            else
            {
                EditorUtility.SetDirty(baked);
            }

            foreach (Mesh part in temporary)
            {
                Object.DestroyImmediate(part);
            }

            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
            }

            root.AddComponent<MeshFilter>().sharedMesh = baked;
            var bakedRenderer = root.AddComponent<MeshRenderer>();
            bakedRenderer.sharedMaterials = order.ToArray();
            bakedRenderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            bakedRenderer.receiveShadows = false;
            bakedRenderer.lightProbeUsage = LightProbeUsage.Off;
            bakedRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private static GameObject Primitive(PrimitiveType type, string name, Transform parent, Material material, bool castShadows)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go;
        }

        /// <summary>Moves the model so its footprint is centred on the wrapper origin and it sits on y = 0.</summary>
        private static void GroundAndCentre(GameObject model)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            Vector3 rootPosition = model.transform.parent != null ? model.transform.parent.position : Vector3.zero;
            model.transform.position += new Vector3(rootPosition.x - bounds.center.x, rootPosition.y - bounds.min.y,
                rootPosition.z - bounds.center.z);
        }

        private static GameObject SavePrefab(GameObject root, string path)
        {
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name)
                {
                    return t;
                }
            }

            return null;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        private static Color Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out Color color) ? color : Color.white;
        }
    }
}
