using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace AlienDefense.EditorTools
{
    /// <summary>Environment pass for the base: painted ground, tiered cliffs, clustered nature and plot-edge
    /// dressing. Purely visual scenery - roads, plots, buildings, camera and UI are never moved.
    ///
    /// Ground: one quad with a baked 1024 px colour map (four grass tones, sun-dried patches, worn road shoulders,
    ///   soft transition rings around plots, dirt at the cliff feet, worn trails). One material, one draw call.
    /// Cliffs: a generated low-poly mesh of three stepped shelves along the west, north and east edges - beige rock
    ///   walls with a grass lip and grass ledges, plus a plateau on top. One mesh, three materials.
    /// Nature: clusters (tree + bushes + rock + grass tufts) placed in pockets - dense at the cliff feet and map edges,
    ///   light near roads, none on plots, roads or the river. Everything is a reused pack prefab, static, no colliders.
    ///
    /// Deterministic (fixed seeds): rebuilding produces the same map.</summary>
    public static partial class BaseSceneSetupTool
    {
        private static readonly string[] DressingRoots =
            { "Ground", "Mountains", "Nature", "PaintedGround", "Cliffs", "NatureClusters", "PlotDressing", "RoadsideGrass", "RiverShoreProps" };

        private const string MeshesFolder = "Assets/_Game/Art/Base/Meshes";
        private const string TexturesFolder = "Assets/_Game/Art/Base/Textures";

        // The basin the base sits in: cliffs rise beyond these lines (west, north, east). South is the river.
        private const float CliffWest = -60f;
        private const float CliffNorth = 48f;
        private const float CliffEast = 62f;
        private const float CliffSouthEnd = -30f;
        private const float CliffCornerRadius = 14f;
        private const int CliffTiers = 3;
        private const float TierHeight = 6f;
        private const float TierDepth = 5.2f;
        private const float PlateauDepth = 45f;
        private const float BuildingPlotClear = 8.2f;
        private const float ExpansionPlotClear = 6.6f;

        private static readonly Color GrassDark = Hex("#5F9C36");
        private static readonly Color GrassMid = Hex("#82BD45");
        private static readonly Color GrassLight = Hex("#9FD35A");
        private static readonly Color GrassDry = Hex("#B2C960");
        private static readonly Color GrassWorn = Hex("#B6B56C");
        private static readonly Color DirtLight = Hex("#C4A271");
        private static readonly Color DirtDark = Hex("#A7845A");
        private static readonly Color SandTint = Hex("#E2D29A");
        private static readonly Color WetSand = Hex("#B9A777");

        private static readonly (Vector2 a, Vector2 b)[] Trails =
        {
            (new Vector2(50f, 0f), new Vector2(57f, 16f)), (new Vector2(57f, 16f), new Vector2(58f, 34f)),
            (new Vector2(-42f, 0f), new Vector2(-50f, -12f)), (new Vector2(-50f, -12f), new Vector2(-54f, -24f)),
            (new Vector2(0f, 24f), new Vector2(5f, 34f)), (new Vector2(5f, 34f), new Vector2(-4f, 45f)),
            (new Vector2(-20f, 26f), new Vector2(-34f, 36f))
        };

        [MenuItem("Tools/Tower Defense/Base/Rebuild Environment Dressing")]
        public static void RebuildEnvironmentDressing()
        {
            Missing.Clear();
            Scene scene = SceneManager.GetSceneByPath(MainMenuPath);
            if (!scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Single);
            }

            GameObject world = FindRoot(scene, WorldRootName);
            Transform environment = world != null ? world.transform.Find("Environment") : null;
            if (environment == null)
            {
                Debug.LogError("[BaseEnvironment] MainMenu has no BaseWorld/Environment. Run Setup Base Scene first.");
                return;
            }

            foreach (string name in DressingRoots)
            {
                Transform old = environment.Find(name);
                while (old != null)
                {
                    Object.DestroyImmediate(old.gameObject);
                    old = environment.Find(name);
                }
            }

            BuildEnvironmentDressing(environment);

            foreach (string name in DressingRoots)
            {
                Transform built = environment.Find(name);
                if (built != null)
                {
                    SetLayerRecursively(built.gameObject, BaseLayer);
                    MarkEnvironmentStatic(built.gameObject);
                    DisableDecorColliders(built.gameObject);
                }
            }

            // The world presenter combines this subtree into static batches the first time the base is shown.
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                var presenter = root.GetComponentInChildren<AlienDefense.UI.BaseBuilding.BaseWorldPresenter>(true);
                if (presenter != null)
                {
                    var so = new SerializedObject(presenter);
                    so.FindProperty("_staticBatchRoot").objectReferenceValue = environment.gameObject;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            ReportMissing();
            Debug.Log("[BaseEnvironment] Environment dressing rebuilt.");
        }

        private static void BuildEnvironmentDressing(Transform environment)
        {
            List<Vector3> rim = BuildRimPath();
            BuildPaintedGround(environment, rim);
            List<(Vector3 position, int tier)> ledgeSpots = BuildCliffs(environment, rim);

            var random = new System.Random(1234);
            var placed = new List<(Vector3 position, float radius)>();
            var pools = new NaturePools();

            DressPlots(environment, pools, placed, random);
            DressLedges(environment, pools, ledgeSpots, random);
            BuildRiverShoreProps(environment, pools, placed, random);
            BuildNatureClusters(environment, pools, placed, random);
            BuildRoadsideGrass(environment, pools, placed, random);
        }

        // ------------------------------------------------------------------ Rim path

        /// <summary>The cliff foot line: up the west edge, along the north edge, down the east edge, with rounded
        /// corners, resampled every ~2.8 m. Its outward normal (Cross(direction, up)) points into the cliffs.</summary>
        private static List<Vector3> BuildRimPath()
        {
            var raw = new List<Vector3>();
            float r = CliffCornerRadius;
            raw.Add(new Vector3(CliffWest, 0f, CliffSouthEnd));
            raw.Add(new Vector3(CliffWest, 0f, CliffNorth - r));
            for (int i = 1; i < 8; i++)
            {
                float a = Mathf.Lerp(180f, 90f, i / 8f) * Mathf.Deg2Rad;
                raw.Add(new Vector3(CliffWest + r + Mathf.Cos(a) * r, 0f, CliffNorth - r + Mathf.Sin(a) * r));
            }

            raw.Add(new Vector3(CliffWest + r, 0f, CliffNorth));
            raw.Add(new Vector3(CliffEast - r, 0f, CliffNorth));
            for (int i = 1; i < 8; i++)
            {
                float a = Mathf.Lerp(90f, 0f, i / 8f) * Mathf.Deg2Rad;
                raw.Add(new Vector3(CliffEast - r + Mathf.Cos(a) * r, 0f, CliffNorth - r + Mathf.Sin(a) * r));
            }

            raw.Add(new Vector3(CliffEast, 0f, CliffNorth - r));
            raw.Add(new Vector3(CliffEast, 0f, CliffEastSouthEnd));

            // Resample to an even spacing.
            var result = new List<Vector3> { raw[0] };
            const float step = 2.8f;
            float carried = 0f;
            for (int i = 0; i < raw.Count - 1; i++)
            {
                Vector3 a = raw[i];
                Vector3 b = raw[i + 1];
                float length = Vector3.Distance(a, b);
                float t = step - carried;
                while (t <= length)
                {
                    result.Add(Vector3.Lerp(a, b, t / length));
                    t += step;
                }

                carried = length - (t - step);
            }

            if ((result[result.Count - 1] - raw[raw.Count - 1]).sqrMagnitude > 0.5f)
            {
                result.Add(raw[raw.Count - 1]);
            }

            return result;
        }

        private static Vector3 RimNormal(List<Vector3> rim, int i)
        {
            Vector3 a = rim[Mathf.Max(0, i - 1)];
            Vector3 b = rim[Mathf.Min(rim.Count - 1, i + 1)];
            return Vector3.Cross((b - a).normalized, Vector3.up).normalized;
        }

        // ------------------------------------------------------------------ Cliffs

        private static float Hash(int a, int b)
        {
            unchecked
            {
                int h = a * 73856093 ^ b * 19349663;
                h = (h << 13) ^ h;
                return ((h * (h * h * 15731 + 789221) + 1376312589) & 0x7fffffff) / 2147483647f;
            }
        }

        private static float TierOffset(int tier, int sample, float s)
        {
            if (tier >= CliffTiers)
            {
                return TierOffset(CliffTiers - 1, sample, s) + PlateauDepth;
            }

            float wobble = 1.5f * Mathf.Sin(s * 0.085f + tier * 1.7f) + 0.8f * Mathf.Sin(s * 0.21f + tier * 3.1f);
            float jag = (Hash(sample, tier + 11) - 0.5f) * 0.9f;
            return tier * TierDepth + wobble + jag + (tier == 0 ? 0f : 1.2f);
        }

        private static List<(Vector3 position, int tier)> BuildCliffs(Transform parent, List<Vector3> rim)
        {
            Material rockA = EnsureMaterial("MAT_Base_CliffRock", Hex("#CFC2A8"), 0.05f);
            Material rockB = EnsureMaterial("MAT_Base_CliffRockDark", Hex("#B8AB92"), 0.05f);
            Material grass = EnsureMaterial("MAT_Base_CliffGrass", Hex("#84BC47"), 0.05f);
            grass.SetColor("_BaseColor", Hex("#84BC47"));
            EditorUtility.SetDirty(grass);

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var submeshes = new[] { new List<int>(), new List<int>(), new List<int>(), new List<int>() };
            var ledges = new List<(Vector3, int)>();

            int count = rim.Count;
            var arc = new float[count];
            for (int i = 1; i < count; i++)
            {
                arc[i] = arc[i - 1] + Vector3.Distance(rim[i - 1], rim[i]);
            }

            float total = arc[count - 1];

            float Height(int tier, int i)
            {
                if (tier < 0)
                {
                    return 0f;
                }

                // Taper at the two ends so the cliff sinks into the ground instead of stopping as a cut section.
                float t = Mathf.Clamp01(arc[i] / 20f) * Mathf.Clamp01((total - arc[i]) / 20f);
                float scale = Mathf.Lerp(0.2f, 1f, Smooth(0f, 1f, t));
                float variation = 1f + (Hash(i / 3, tier + 40) - 0.5f) * 0.18f;
                return (tier + 1) * TierHeight * scale * variation;
            }

            Vector3 Point(int i, float offset, float y)
            {
                return rim[i] + RimNormal(rim, i) * offset + Vector3.up * y;
            }

            for (int tier = 0; tier < CliffTiers; tier++)
            {
                int wall = tier % 2 == 0 ? 0 : 1;
                for (int i = 0; i < count - 1; i++)
                {
                    int j = i + 1;
                    float oi = TierOffset(tier, i, arc[i]);
                    float oj = TierOffset(tier, j, arc[j]);
                    float bottomI = Height(tier - 1, i);
                    float bottomJ = Height(tier - 1, j);
                    float topI = Height(tier, i);
                    float topJ = Height(tier, j);
                    const float lip = 0.45f;
                    Vector3 inward = -RimNormal(rim, i);

                    // Faceted wall: two rows, the middle row pushed in or out a little per sample.
                    float midI = (bottomI + topI - lip) * 0.5f;
                    float midJ = (bottomJ + topJ - lip) * 0.5f;
                    float jitterI = (Hash(i, tier) - 0.5f) * 1.1f;
                    float jitterJ = (Hash(j, tier) - 0.5f) * 1.1f;

                    Vector3 bi = Point(i, oi, bottomI);
                    Vector3 bj = Point(j, oj, bottomJ);
                    Vector3 mi = Point(i, oi + jitterI, midI);
                    Vector3 mj = Point(j, oj + jitterJ, midJ);
                    Vector3 ti = Point(i, oi + 0.2f, topI - lip);
                    Vector3 tj = Point(j, oj + 0.2f, topJ - lip);
                    // The bottom tier is left open in front of the river cave; the cave arch frames that gap.
                    bool caveOpening = tier == 0 && (IsCaveRimSample(rim[i]) || IsCaveRimSample(rim[j]));
                    if (!caveOpening)
                    {
                        AddQuad(vertices, normals, submeshes[wall], bi, bj, mj, mi, inward);
                        AddQuad(vertices, normals, submeshes[wall], mi, mj, tj, ti, inward);
                    }

                    // Grass lip draped over the top edge, then the ledge back to the next tier's foot.
                    Vector3 gi = Point(i, oi - 0.35f, topI);
                    Vector3 gj = Point(j, oj - 0.35f, topJ);
                    if (!caveOpening)
                    {
                        AddQuad(vertices, normals, submeshes[3], ti, tj, gj, gi, (inward + Vector3.up).normalized);
                    }

                    float backI = TierOffset(tier + 1, i, arc[i]);
                    float backJ = TierOffset(tier + 1, j, arc[j]);
                    Vector3 ri = Point(i, backI, topI);
                    Vector3 rj = Point(j, backJ, topJ);
                    // Over the cave the bottom ledge would slice through the opening; the cave's rock hood roofs it.
                    if (!caveOpening)
                    {
                        AddQuad(vertices, normals, submeshes[2], gi, gj, rj, ri, Vector3.up);
                    }

                    if (i % 2 == 1)
                    {
                        float u = 0.3f + Hash(i, tier + 70) * 0.4f;
                        float along = tier == CliffTiers - 1 ? 1.5f + Hash(i, 91) * 7f : (backI - oi) * u;
                        ledges.Add((Point(i, oi + along, topI), tier));
                    }
                }

                // End caps close each shelf at both ends of the rim.
                foreach (int end in new[] { 0, count - 1 })
                {
                    Vector3 direction = end == 0 ? -(rim[1] - rim[0]).normalized : (rim[count - 1] - rim[count - 2]).normalized;
                    float o = TierOffset(tier, end, arc[end]);
                    float back = TierOffset(tier + 1, end, arc[end]);
                    Vector3 a = Point(end, o, Height(tier - 1, end));
                    Vector3 b = Point(end, back, Height(tier - 1, end));
                    Vector3 c = Point(end, back, Height(tier, end));
                    Vector3 d = Point(end, o, Height(tier, end));
                    AddQuad(vertices, normals, submeshes[wall], a, b, c, d, direction);
                }
            }

            EnsureFolder(MeshesFolder);
            string path = MeshesFolder + "/SM_Base_Cliffs.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool isNew = mesh == null;
            mesh = mesh != null ? mesh : new Mesh();
            mesh.Clear();
            mesh.name = "SM_Base_Cliffs";
            mesh.indexFormat = vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            // World-mapped UVs: the ledges sample the same painted grass map as the ground.
            var uvs = new Vector2[vertices.Count];
            for (int i = 0; i < uvs.Length; i++)
            {
                uvs[i] = new Vector2((vertices[i].x + GroundHalfSize) / (GroundHalfSize * 2f), (vertices[i].z + GroundHalfSize) / (GroundHalfSize * 2f));
            }

            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 4;
            for (int i = 0; i < 4; i++)
            {
                mesh.SetTriangles(submeshes[i], i);
            }

            mesh.RecalculateBounds();
            if (isNew)
            {
                AssetDatabase.CreateAsset(mesh, path);
            }
            else
            {
                EditorUtility.SetDirty(mesh);
            }

            var cliffs = new GameObject("Cliffs");
            cliffs.transform.SetParent(parent, false);
            cliffs.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = cliffs.AddComponent<MeshRenderer>();
            Material ledgeGrass = AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/MAT_Base_GroundPainted.mat") ?? grass;
            renderer.sharedMaterials = new[] { rockA, rockB, ledgeGrass, grass };
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return ledges;
        }

        /// <summary>Flat-shaded quad (own vertices), wound so its face points along <paramref name="facing"/>.</summary>
        private static void AddQuad(List<Vector3> vertices, List<Vector3> normals, List<int> triangles,
            Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 facing)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a);
            if (normal.sqrMagnitude < 1e-8f)
            {
                normal = Vector3.Cross(c - a, d - a);
            }

            if (Vector3.Dot(normal, facing) < 0f)
            {
                (b, d) = (d, b);
                normal = -normal;
            }

            normal.Normalize();
            int start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);
            for (int i = 0; i < 4; i++)
            {
                normals.Add(normal);
            }

            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }

        // ------------------------------------------------------------------ Painted ground

        private static void BuildPaintedGround(Transform parent, List<Vector3> rim)
        {
            EnsureFolder(TexturesFolder);
            EnsureFolder(MeshesFolder);
            Texture2D texture = PaintGroundTexture(rim);

            var material = EnsureMaterial("MAT_Base_GroundPainted", Color.white, 0.02f);
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(material);

            string meshPath = MeshesFolder + "/SM_Base_Ground.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            bool isNew = mesh == null;
            mesh = mesh != null ? mesh : new Mesh();
            mesh.Clear();
            mesh.name = "SM_Base_Ground";
            float h = GroundHalfSize;
            mesh.vertices = new[] { new Vector3(-h, 0f, -h), new Vector3(-h, 0f, h), new Vector3(h, 0f, h), new Vector3(h, 0f, -h) };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            if (isNew)
            {
                AssetDatabase.CreateAsset(mesh, meshPath);
            }
            else
            {
                EditorUtility.SetDirty(mesh);
            }

            var ground = new GameObject("PaintedGround");
            ground.transform.SetParent(parent, false);
            ground.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = ground.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        /// <summary>Bakes the ground colour map: world XZ [-110, 110] -> 1024 px. All the variation lives in the
        /// texture, so the ground stays one quad with one material.</summary>
        private static Texture2D PaintGroundTexture(List<Vector3> rim)
        {
            const int size = 1024;
            float world = GroundHalfSize * 2f;
            var pixels = new Color32[size * size];

            // Rectangles that are roads or driveways (centre XZ, half size XZ).
            var roads = new List<(Vector2 c, Vector2 h)>();
            foreach ((Vector3 center, Vector2 s) in Roads)
            {
                roads.Add((new Vector2(center.x, center.z), s * 0.5f));
            }

            foreach ((string _, Vector3 p) in Plots)
            {
                roads.Add((new Vector2(p.x, Mathf.Sign(p.z) * 3.8f), new Vector2(1.5f, 1.9f)));
            }

            var plotSquares = new List<(Vector2 c, float h)>();
            foreach ((string _, Vector3 p) in Plots) plotSquares.Add((new Vector2(p.x, p.z), 5.6f));
            foreach (Vector3 p in ExpansionPlots) plotSquares.Add((new Vector2(p.x, p.z), 5.6f));

            for (int y = 0; y < size; y++)
            {
                float wz = -GroundHalfSize + (y + 0.5f) / size * world;
                for (int x = 0; x < size; x++)
                {
                    float wx = -GroundHalfSize + (x + 0.5f) / size * world;
                    var p = new Vector2(wx, wz);

                    // Four grass tones from large soft blotches, plus fine speckle.
                    float large = Fbm(wx * 0.018f, wz * 0.018f, 3);
                    float medium = Fbm(wx * 0.06f + 40f, wz * 0.06f + 40f, 2);
                    Color c = Color.Lerp(GrassDark, GrassMid, Smooth(0.3f, 0.55f, large));
                    c = Color.Lerp(c, GrassLight, Smooth(0.48f, 0.7f, medium) * 0.85f);
                    float dry = Fbm(wx * 0.03f + 90f, wz * 0.03f + 17f, 2);
                    c = Color.Lerp(c, GrassDry, Smooth(0.54f, 0.68f, dry) * 0.8f);
                    float speckle = Mathf.PerlinNoise(wx * 0.9f + 5f, wz * 0.9f + 9f);
                    c *= 0.96f + speckle * 0.08f;

                    // Worn patches that go through worn grass to dirt.
                    float worn = Fbm(wx * 0.045f + 300f, wz * 0.045f + 120f, 3);
                    c = Color.Lerp(c, GrassWorn, Smooth(0.64f, 0.72f, worn) * 0.8f);
                    c = Color.Lerp(c, DirtLight, Smooth(0.72f, 0.78f, worn) * 0.85f);

                    float edgeNoise = Mathf.PerlinNoise(wx * 0.35f + 11f, wz * 0.35f + 3f);

                    // Road shoulders: dirt right at the edge, worn grass a little further out.
                    float road = float.MaxValue;
                    foreach ((Vector2 rc, Vector2 rh) in roads)
                    {
                        road = Mathf.Min(road, RectDistance(p, rc, rh));
                    }

                    c = Color.Lerp(c, GrassWorn, (1f - Smooth(1f, 3.8f + edgeNoise * 1.5f, road)) * 0.6f);
                    c = Color.Lerp(c, DirtLight, (1f - Smooth(0.2f, 1.1f + edgeNoise * 1.3f, road)) * 0.85f);

                    // Soft ring around every plot so the dirt blends into the grass.
                    foreach ((Vector2 pc, float ph) in plotSquares)
                    {
                        float d = RectDistance(p, pc, new Vector2(ph, ph));
                        if (d < 3.2f)
                        {
                            c = Color.Lerp(c, GrassWorn, (1f - Smooth(0f, 2.4f + edgeNoise, d)) * 0.55f);
                            c = Color.Lerp(c, DirtDark, (1f - Smooth(0f, 0.8f + edgeNoise * 0.8f, d)) * 0.5f);
                        }
                    }

                    // Dirt and scree at the cliff feet.
                    if (wx < CliffWest + 8f || wx > CliffEast - 8f || wz > CliffNorth - 8f)
                    {
                        float cliff = PolylineDistance(p, rim);
                        c = Color.Lerp(c, DirtLight, (1f - Smooth(0.5f, 3.5f + edgeNoise * 2f, cliff)) * 0.7f);
                    }

                    // Riverbank transition, following the curved river: light grass -> sand -> wet sand at the water.
                    float bank = RiverBankDistance(p);
                    if (bank < 6f)
                    {
                        c = Color.Lerp(c, GrassDry, (1f - Smooth(1.6f, 4f + edgeNoise * 1.2f, bank)) * 0.45f);
                        c = Color.Lerp(c, SandTint, (1f - Smooth(0.5f, 1.9f + edgeNoise * 0.9f, bank)) * 0.9f);
                        c = Color.Lerp(c, WetSand, (1f - Smooth(0f, 0.7f + edgeNoise * 0.3f, bank)) * 0.75f);
                    }

                    // Worn foot trails.
                    float trail = float.MaxValue;
                    foreach ((Vector2 a, Vector2 b) in Trails)
                    {
                        trail = Mathf.Min(trail, SegmentDistance(p, a, b));
                    }

                    c = Color.Lerp(c, GrassWorn, (1f - Smooth(0.6f, 1.8f + edgeNoise, trail)) * 0.75f);
                    c = Color.Lerp(c, DirtLight, (1f - Smooth(0.1f, 0.8f + edgeNoise * 0.6f, trail)) * 0.5f);

                    c.a = 1f;
                    pixels[y * size + x] = c;
                }
            }

            var texture = new Texture2D(size, size, TextureFormat.RGB24, true);
            texture.SetPixels32(pixels);
            texture.Apply();
            string path = TexturesFolder + "/T_Base_GroundPaint.png";
            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 1024;
            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>GLSL-style smoothstep (edge0, edge1, x). Mathf.SmoothStep is an interpolation, not this.</summary>
        private static float Smooth(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        private static float Fbm(float x, float y, int octaves)
        {
            float value = 0f;
            float amplitude = 0.5f;
            float sum = 0f;
            for (int i = 0; i < octaves; i++)
            {
                value += Mathf.PerlinNoise(x, y) * amplitude;
                sum += amplitude;
                x = x * 2.03f + 17.1f;
                y = y * 2.03f + 9.3f;
                amplitude *= 0.5f;
            }

            return value / sum;
        }

        private static float RectDistance(Vector2 p, Vector2 center, Vector2 half)
        {
            float dx = Mathf.Max(Mathf.Abs(p.x - center.x) - half.x, 0f);
            float dy = Mathf.Max(Mathf.Abs(p.y - center.y) - half.y, 0f);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            return Vector2.Distance(p, a + ab * t);
        }

        private static float PolylineDistance(Vector2 p, List<Vector3> line)
        {
            float best = float.MaxValue;
            for (int i = 0; i < line.Count - 1; i++)
            {
                best = Mathf.Min(best, SegmentDistance(p, new Vector2(line[i].x, line[i].z), new Vector2(line[i + 1].x, line[i + 1].z)));
            }

            return best;
        }

        // ------------------------------------------------------------------ Nature

        private sealed class NaturePools
        {
            public readonly List<(GameObject prefab, float min, float max)> Trees = new List<(GameObject, float, float)>();
            public readonly List<(GameObject prefab, float min, float max)> Pines = new List<(GameObject, float, float)>();
            public readonly List<(GameObject prefab, float min, float max)> Bushes = new List<(GameObject, float, float)>();
            public readonly List<(GameObject prefab, float min, float max)> Rocks = new List<(GameObject, float, float)>();
            public readonly List<(GameObject prefab, float min, float max)> BigRocks = new List<(GameObject, float, float)>();
            public readonly List<(GameObject prefab, float min, float max)> Grass = new List<(GameObject, float, float)>();
            public readonly List<(GameObject prefab, float min, float max)> Flowers = new List<(GameObject, float, float)>();
            public readonly List<(GameObject prefab, float min, float max)> Stumps = new List<(GameObject, float, float)>();

            public NaturePools()
            {
                Add(Trees, "Env_Tree_02", 1.25f, 1.6f);
                Add(Trees, "Env_Tree_04", 1.2f, 1.55f);
                Add(Trees, "Env_Tree_05", 1.15f, 1.45f);
                Add(Trees, "Env_Tree_07", 1.25f, 1.6f);
                Add(Trees, "Env_Tree_01", 1.1f, 1.35f);
                Add(Pines, "PT_Pine_Tree_03_green", 0.62f, 0.85f);
                Add(Pines, "Env_Tree_05", 1.25f, 1.6f);
                Add(Bushes, "Env_Bush_02", 1.8f, 2.5f);
                Add(Bushes, "Env_Bush_01", 1.5f, 2.1f);
                Add(Bushes, "PT_Generic_Shrub_01_green", 1f, 1.4f);
                // Rocks: generated low-poly boulders in the cliff palette (the pack rocks read blue-grey here).
                GameObject[] boulders = EnsureBoulderPrefabs();
                foreach (GameObject boulder in boulders)
                {
                    Rocks.Add((boulder, 0.4f, 0.7f));
                    BigRocks.Add((boulder, 0.9f, 1.4f));
                }
                Add(Grass, "PT_Grass_02_v1", 1.9f, 2.6f);
                Add(Grass, "PT_High_Grass_02_v1", 1.8f, 2.5f);
                Add(Grass, "PT_Grass_02", 1.8f, 2.5f);
                Add(Flowers, "PT_Poppy_02", 1.4f, 1.8f);
                Add(Flowers, "PT_Caesars_Mushroom_01", 1.6f, 2.2f);
                Add(Stumps, "PT_Pine_Tree_03_stump", 1.4f, 1.8f);
                Add(Stumps, "PT_Fruit_Tree_01_stump", 1.2f, 1.6f);
            }

            private static void Add(List<(GameObject, float, float)> pool, string name, float min, float max)
            {
                GameObject prefab = FindPrefab(name);
                if (prefab != null)
                {
                    pool.Add((prefab, min, max));
                }
                else
                {
                    Missing.Add("Nature prefab " + name + " - skipped.");
                }
            }
        }

        /// <summary>Three faceted boulders (subdivided icosahedron, jittered, flattened, flat-shaded; 80 triangles)
        /// in the cliff rock colours, so loose rocks and cliffs read as the same stone.</summary>
        private static GameObject[] EnsureBoulderPrefabs()
        {
            const string folder = "Assets/_Game/Prefabs/Base/Environment";
            EnsureFolder(folder);
            EnsureFolder(MeshesFolder);
            Material light = EnsureMaterial("MAT_Base_CliffRock", Hex("#CFC2A8"), 0.05f);
            Material dark = EnsureMaterial("MAT_Base_CliffRockDark", Hex("#B8AB92"), 0.05f);

            var result = new GameObject[3];
            for (int variant = 0; variant < 3; variant++)
            {
                string path = folder + "/BP_Boulder_" + (char)('A' + variant) + ".prefab";
                var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (existing != null)
                {
                    result[variant] = existing;
                    continue;
                }

                Mesh mesh = BuildBoulderMesh(variant, MeshesFolder + "/SM_Boulder_" + (char)('A' + variant) + ".asset");
                var go = new GameObject("BP_Boulder_" + (char)('A' + variant));
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = variant == 1 ? dark : light;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                result[variant] = PrefabUtility.SaveAsPrefabAsset(go, path);
                Object.DestroyImmediate(go);
            }

            return result;
        }

        private static Mesh BuildBoulderMesh(int seed, string path)
        {
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var verts = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
            };
            int[] faces =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
            };

            // One subdivision (midpoints shared through a cache).
            var cache = new Dictionary<long, int>();
            int Mid(int a, int b)
            {
                long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (cache.TryGetValue(key, out int index)) return index;
                verts.Add((verts[a] + verts[b]) * 0.5f);
                cache[key] = verts.Count - 1;
                return verts.Count - 1;
            }

            var tris = new List<int>();
            for (int i = 0; i < faces.Length; i += 3)
            {
                int a = faces[i], b = faces[i + 1], c = faces[i + 2];
                int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                tris.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }

            // Jitter the radius per vertex and flatten: a chunky, stable boulder.
            for (int i = 0; i < verts.Count; i++)
            {
                float radius = 0.8f + Hash(i, seed + 7) * 0.4f;
                Vector3 v = verts[i].normalized * radius;
                v.y *= 0.62f;
                v.x *= 1f + seed * 0.12f;
                verts[i] = v + Vector3.up * 0.35f;
            }

            // Flat shading: three unique vertices per triangle.
            var positions = new List<Vector3>();
            var normals = new List<Vector3>();
            var indices = new List<int>();
            for (int i = 0; i < tris.Count; i += 3)
            {
                Vector3 a = verts[tris[i]], b = verts[tris[i + 1]], c = verts[tris[i + 2]];
                Vector3 n = Vector3.Cross(b - a, c - a).normalized;
                if (Vector3.Dot(n, (a + b + c) / 3f - Vector3.up * 0.35f) < 0f)
                {
                    (b, c) = (c, b);
                    n = -n;
                }

                int start = positions.Count;
                positions.Add(a);
                positions.Add(b);
                positions.Add(c);
                normals.Add(n);
                normals.Add(n);
                normals.Add(n);
                indices.Add(start);
                indices.Add(start + 1);
                indices.Add(start + 2);
            }

            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool isNew = mesh == null;
            mesh = mesh != null ? mesh : new Mesh();
            mesh.Clear();
            mesh.name = System.IO.Path.GetFileNameWithoutExtension(path);
            mesh.SetVertices(positions);
            mesh.SetNormals(normals);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds();
            if (isNew) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        /// <summary>True where nothing decorative may stand: roads (+margin), driveways, plots, river, beyond the
        /// cliff feet, and outside the map.</summary>
        private static bool Blocked(Vector3 p, float radius)
        {
            if (Mathf.Abs(p.x) > GroundHalfSize - 4f || Mathf.Abs(p.z) > GroundHalfSize - 4f)
            {
                return true;
            }

            var q = new Vector2(p.x, p.z);
            foreach ((Vector3 center, Vector2 s) in Roads)
            {
                if (RectDistance(q, new Vector2(center.x, center.z), s * 0.5f) < 1.4f + radius) return true;
            }

            foreach ((string _, Vector3 plot) in Plots)
            {
                if (RectDistance(q, new Vector2(plot.x, Mathf.Sign(plot.z) * 3.8f), new Vector2(1.5f, 1.9f)) < 1f + radius) return true;
                if (RectDistance(q, new Vector2(plot.x, plot.z), new Vector2(BuildingPlotClear, BuildingPlotClear)) < radius) return true;
            }

            foreach (Vector3 plot in ExpansionPlots)
            {
                if (RectDistance(q, new Vector2(plot.x, plot.z), new Vector2(ExpansionPlotClear, ExpansionPlotClear)) < radius) return true;
            }

            if (RiverBankDistance(new Vector2(p.x, p.z)) < 1.8f + radius) return true;

            // The basin ends at the cliff feet (the cliff mesh starts there).
            if ((p.z > CliffSouthEnd - 2f && p.x < CliffWest + 1f + radius) || (p.z > CliffEastSouthEnd - 2f && p.x > CliffEast - 1f - radius) ||
                p.z > CliffNorth - 1f - radius)
            {
                return true;
            }

            return false;
        }

        private static bool Overlaps(Vector3 p, float radius, List<(Vector3 position, float radius)> placed)
        {
            for (int i = 0; i < placed.Count; i++)
            {
                float min = (placed[i].radius + radius) * 0.7f;
                if ((placed[i].position - p).sqrMagnitude < min * min)
                {
                    return true;
                }
            }

            return false;
        }

        private static GameObject PlaceNature(Transform parent, List<(GameObject prefab, float min, float max)> pool,
            Vector3 position, System.Random random)
        {
            if (pool.Count == 0)
            {
                return null;
            }

            (GameObject prefab, float min, float max) = pool[random.Next(pool.Count)];
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.position = position;
            instance.transform.rotation = Quaternion.Euler(0f, Range(random, 0f, 360f), 0f);
            instance.transform.localScale = Vector3.one * Range(random, min, max);
            return instance;
        }

        /// <summary>Places one item of a cluster: a few tries inside the ring, respecting blocked areas and spacing.</summary>
        private static bool TryPlaceInCluster(Transform parent, List<(GameObject, float, float)> pool, Vector3 center,
            float minRing, float maxRing, float radius, List<(Vector3, float)> placed, System.Random random, bool checkBlocked = true)
        {
            // Rings and radii are authored for 1x props; the props are scaled up for the far base camera.
            const float spread = 1.35f;
            minRing *= spread;
            maxRing *= spread;
            radius *= spread;
            for (int attempt = 0; attempt < 6; attempt++)
            {
                float angle = Range(random, 0f, Mathf.PI * 2f);
                float distance = Range(random, minRing, maxRing);
                Vector3 p = center + new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
                if ((checkBlocked && Blocked(p, radius)) || Overlaps(p, radius, placed))
                {
                    continue;
                }

                if (PlaceNature(parent, pool, p, random) != null)
                {
                    placed.Add((p, radius));
                    return true;
                }
            }

            return false;
        }

        /// <summary>Pockets of tree + bushes + rock + grass. Dense against the cliffs and toward the map edges, sparse
        /// in the middle of town, none on roads or plots.</summary>
        private static void BuildNatureClusters(Transform parent, NaturePools pools, List<(Vector3, float)> placed,
            System.Random random)
        {
            var root = new GameObject("NatureClusters").transform;
            root.SetParent(parent, false);

            var centers = new List<Vector3>();
            int clusters = 0;
            for (int attempt = 0; attempt < 9000 && clusters < 200; attempt++)
            {
                var c = new Vector3(Range(random, -100f, 100f), 0f, Range(random, -100f, CliffNorth));
                if (Blocked(c, 1.5f))
                {
                    continue;
                }

                bool inBasin = c.z > CliffSouthEnd;
                float edge = inBasin ? Mathf.Min(c.x - CliffWest, CliffEast - c.x, CliffNorth - c.z) : 40f;
                float fromCentre = new Vector2(c.x - 5f, c.z).magnitude;
                float weight = 0.3f + 0.7f * Mathf.Clamp01(1f - edge / 18f) + (fromCentre > 30f ? 0.35f : 0f);
                if (!inBasin)
                {
                    // South of the river: mostly seen at the bottom of the screen; a fuller forest reads well there.
                    weight = c.z > -80f ? 0.75f : 0.45f;
                }

                if (random.NextDouble() > weight)
                {
                    continue;
                }

                float spacing = edge < 12f ? 7f : 9.5f;
                bool tooClose = false;
                foreach (Vector3 other in centers)
                {
                    if ((other - c).sqrMagnitude < spacing * spacing)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (tooClose)
                {
                    continue;
                }

                centers.Add(c);
                clusters++;
                var group = new GameObject("Cluster_" + clusters).transform;
                group.SetParent(root, false);

                double kind = random.NextDouble();
                if (edge < 10f)
                {
                    // Cliff foot: pine, bushes, a big rock and scree.
                    TryPlaceInCluster(group, pools.Pines, c, 0f, 1.2f, 1.6f, placed, random);
                    TryPlaceInCluster(group, pools.BigRocks, c, 1.2f, 2.6f, 1.2f, placed, random);
                    for (int i = 0; i < 2; i++) TryPlaceInCluster(group, pools.Bushes, c, 1.2f, 3f, 0.9f, placed, random);
                    TryPlaceInCluster(group, pools.Rocks, c, 1.5f, 3.2f, 0.5f, placed, random);
                    for (int i = 0; i < 3; i++) TryPlaceInCluster(group, pools.Grass, c, 1.8f, 3.8f, 0.4f, placed, random);
                }
                else if (kind < 0.45)
                {
                    // Grove: 1-2 trees, 2-3 bushes, a rock, grass, maybe a stump.
                    int trees = random.NextDouble() < 0.5 ? 1 : 2;
                    for (int i = 0; i < trees; i++) TryPlaceInCluster(group, pools.Trees, c, 0f, 1.6f, 1.4f, placed, random);
                    int bushes = 2 + random.Next(2);
                    for (int i = 0; i < bushes; i++) TryPlaceInCluster(group, pools.Bushes, c, 1.2f, 3f, 0.8f, placed, random);
                    TryPlaceInCluster(group, pools.Rocks, c, 1f, 2.8f, 0.5f, placed, random);
                    for (int i = 0; i < 3 + random.Next(2); i++) TryPlaceInCluster(group, pools.Grass, c, 1.5f, 3.8f, 0.4f, placed, random);
                    if (random.NextDouble() < 0.3) TryPlaceInCluster(group, pools.Stumps, c, 1.5f, 3.2f, 0.5f, placed, random);
                }
                else if (kind < 0.8)
                {
                    // Bush pocket with flowers.
                    for (int i = 0; i < 2 + random.Next(2); i++) TryPlaceInCluster(group, pools.Bushes, c, 0f, 2.2f, 0.8f, placed, random);
                    TryPlaceInCluster(group, pools.Rocks, c, 0.8f, 2.4f, 0.5f, placed, random);
                    for (int i = 0; i < 2 + random.Next(2); i++) TryPlaceInCluster(group, pools.Grass, c, 1.2f, 3.2f, 0.4f, placed, random);
                    if (random.NextDouble() < 0.45) TryPlaceInCluster(group, pools.Flowers, c, 0.8f, 2.8f, 0.3f, placed, random);
                }
                else
                {
                    // Rock outcrop.
                    TryPlaceInCluster(group, pools.BigRocks, c, 0f, 0.8f, 1.2f, placed, random);
                    TryPlaceInCluster(group, pools.Rocks, c, 1f, 2.2f, 0.5f, placed, random);
                    for (int i = 0; i < 2 + random.Next(2); i++) TryPlaceInCluster(group, pools.Grass, c, 1f, 2.8f, 0.4f, placed, random);
                    if (random.NextDouble() < 0.35) TryPlaceInCluster(group, pools.Flowers, c, 0.8f, 2.4f, 0.3f, placed, random);
                    if (random.NextDouble() < 0.35) TryPlaceInCluster(group, pools.Stumps, c, 1f, 2.6f, 0.5f, placed, random);
                }
            }
        }

        /// <summary>Grass, bushes, rocks and stumps on some cliff ledges, and a tree line along the plateau edge.</summary>
        private static void DressLedges(Transform parent, NaturePools pools, List<(Vector3 position, int tier)> spots,
            System.Random random)
        {
            var root = new GameObject("LedgeDressing").transform;
            root.SetParent(parent.Find("Cliffs") != null ? parent.Find("Cliffs") : parent, false);
            foreach ((Vector3 spot, int tier) in spots)
            {
                double roll = random.NextDouble();
                if (tier == CliffTiers - 1)
                {
                    if (roll < 0.75) PlaceNature(root, roll < 0.45 ? pools.Pines : pools.Trees, spot, random);
                    if (random.NextDouble() < 0.5) PlaceNature(root, pools.Bushes, spot + new Vector3(Range(random, -2f, 2f), 0f, Range(random, -2f, 2f)), random);
                    continue;
                }

                if (roll < 0.35)
                {
                    PlaceNature(root, pools.Bushes, spot, random);
                    PlaceNature(root, pools.Grass, spot + new Vector3(Range(random, -1.5f, 1.5f), 0f, Range(random, -1.5f, 1.5f)), random);
                }
                else if (roll < 0.55)
                {
                    PlaceNature(root, pools.Rocks, spot, random);
                    PlaceNature(root, pools.Grass, spot + new Vector3(Range(random, -1.2f, 1.2f), 0f, Range(random, -1.2f, 1.2f)), random);
                }
                else if (roll < 0.72)
                {
                    PlaceNature(root, pools.Trees, spot, random);
                }
            }
        }

        /// <summary>Embeds each plot in the landscape: small clusters at some corners and weeds/grass tufts along
        /// the edges, kept off the road side and off the plot itself.</summary>
        private static void DressPlots(Transform parent, NaturePools pools, List<(Vector3, float)> placed, System.Random random)
        {
            var root = new GameObject("PlotDressing").transform;
            root.SetParent(parent, false);

            var plots = new List<(Vector3 center, float ring)>();
            foreach ((string _, Vector3 p) in Plots) plots.Add((p, BuildingPlotClear + 0.6f));
            foreach (Vector3 p in ExpansionPlots) plots.Add((p, ExpansionPlotClear + 0.5f));

            foreach ((Vector3 center, float ring) in plots)
            {
                var group = new GameObject("Plot_" + center.x.ToString("0") + "_" + center.z.ToString("0")).transform;
                group.SetParent(root, false);

                // Corner clusters on 2-3 corners.
                int cornerSkip = random.Next(4);
                for (int corner = 0; corner < 4; corner++)
                {
                    if (corner == cornerSkip && random.NextDouble() < 0.7)
                    {
                        continue;
                    }

                    var cc = center + new Vector3(corner % 2 == 0 ? -ring : ring, 0f, corner < 2 ? -ring : ring);
                    if (Blocked(cc, 0.6f))
                    {
                        continue;
                    }

                    TryPlaceInCluster(group, pools.Bushes, cc, 0f, 0.8f, 0.8f, placed, random);
                    TryPlaceInCluster(group, pools.Grass, cc, 0.8f, 2f, 0.4f, placed, random);
                    if (random.NextDouble() < 0.6) TryPlaceInCluster(group, pools.Rocks, cc, 0.6f, 1.8f, 0.4f, placed, random);
                    if (random.NextDouble() < 0.3) TryPlaceInCluster(group, pools.Stumps, cc, 0.8f, 2f, 0.5f, placed, random);
                }

                // Weeds and grass tufts along the sides.
                for (int i = 0; i < 7; i++)
                {
                    int side = random.Next(4);
                    float along = Range(random, -ring, ring);
                    float out_ = ring + Range(random, -0.3f, 0.8f);
                    Vector3 p = center + (side == 0 ? new Vector3(along, 0f, -out_) : side == 1 ? new Vector3(out_, 0f, along)
                        : side == 2 ? new Vector3(along, 0f, out_) : new Vector3(-out_, 0f, along));
                    if (Blocked(p, 0.3f) || Overlaps(p, 0.35f, placed))
                    {
                        continue;
                    }

                    PlaceNature(group, random.NextDouble() < 0.25 ? pools.Flowers : pools.Grass, p, random);
                    placed.Add((p, 0.35f));
                }
            }
        }

        /// <summary>A light scatter of grass tufts along the road shoulders - roads stay clearly readable.</summary>
        private static void BuildRoadsideGrass(Transform parent, NaturePools pools, List<(Vector3, float)> placed, System.Random random)
        {
            var root = new GameObject("RoadsideGrass").transform;
            root.SetParent(parent, false);
            foreach ((Vector3 center, Vector2 size) in Roads)
            {
                bool alongX = size.x > size.y;
                float length = alongX ? size.x : size.y;
                int tufts = Mathf.RoundToInt(length / 4.5f);
                for (int i = 0; i < tufts; i++)
                {
                    float along = Range(random, -length * 0.5f, length * 0.5f);
                    float side = (random.NextDouble() < 0.5 ? -1f : 1f) * (RoadWidth * 0.5f + Range(random, 1.7f, 2.6f));
                    Vector3 p = center + (alongX ? new Vector3(along, 0f, side) : new Vector3(side, 0f, along));
                    // Only the road clearance is relaxed here; plots, river and cliffs still block.
                    if (Overlaps(p, 0.4f, placed) || BlockedExceptRoads(p, 0.3f))
                    {
                        continue;
                    }

                    PlaceNature(root, pools.Grass, p, random);
                    placed.Add((p, 0.4f));
                }
            }
        }

        private static bool BlockedExceptRoads(Vector3 p, float radius)
        {
            var q = new Vector2(p.x, p.z);
            foreach ((Vector3 center, Vector2 s) in Roads)
            {
                if (RectDistance(q, new Vector2(center.x, center.z), s * 0.5f) < 0.6f) return true;
            }

            foreach ((string _, Vector3 plot) in Plots)
            {
                if (RectDistance(q, new Vector2(plot.x, Mathf.Sign(plot.z) * 3.8f), new Vector2(1.5f, 1.9f)) < 0.8f) return true;
                if (RectDistance(q, new Vector2(plot.x, plot.z), new Vector2(BuildingPlotClear, BuildingPlotClear)) < radius) return true;
            }

            foreach (Vector3 plot in ExpansionPlots)
            {
                if (RectDistance(q, new Vector2(plot.x, plot.z), new Vector2(ExpansionPlotClear, ExpansionPlotClear)) < radius) return true;
            }

            return RiverBankDistance(new Vector2(p.x, p.z)) < 1.2f;
        }
    }
}
