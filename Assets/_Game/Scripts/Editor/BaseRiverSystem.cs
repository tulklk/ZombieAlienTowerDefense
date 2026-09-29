using System.Collections.Generic;
using AlienDefense.Base.River;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace AlienDefense.EditorTools
{
    /// <summary>The Base map's river: a curved turquoise channel from the west edge, under the bridge, bending north-
    /// east into a cave in the east cliff (the cliff face that looks toward the camera). Boats and ducks live on it.
    ///
    /// Geometry is generated from one Catmull-Rom centreline with a half-width per point; the same data drives the
    /// water mesh, the painted shoreline on the ground, where scenery may stand, the RiverChannel the ducks use to
    /// stay in the water, and the boat route. Changing the control points reshapes all of it consistently.
    ///
    /// The river actors (boats, ducks, ripples) live under BaseWorld/RiverSystem - NOT under Environment - because
    /// Environment is static-batched and these move. Third-party boat/duck assets are never modified: boats are
    /// wrapped with URP copies of their Standard materials, ducks are re-meshed from the .blend into mesh assets
    /// (lighter, and no longer dependent on Blender being installed to import).</summary>
    public static partial class BaseSceneSetupTool
    {
        private const string RiverSystemName = "RiverSystem";
        private const string RiverPrefabFolder = "Assets/_Game/Prefabs/Base/River";
        private const string RiverMaterialFolder = "Assets/_Game/Art/Base/Materials/River";
        private const string RiverMeshFolder = "Assets/_Game/Models/Optimized/Base/River";
        private const float RiverWaterY = 0.06f;

        // Cave in the east cliff (whose walls face the base camera).
        private const float CliffEastSouthEnd = -62f;
        private const float CaveZ = -26f;
        private const float CaveHalfSpan = 8f;
        private const float CaveArchInner = 7f;
        private const float CaveArchOuter = 9.4f;
        private const float CaveDepth = 21f;

        /// <summary>(x, z, half-width). West edge -> bridge at x = 0 (kept perpendicular there) -> north-east into
        /// the cave at (62, -26) and on into the mountain.</summary>
        private static readonly (float x, float z, float halfWidth)[] RiverControl =
        {
            (-118f, -41f, 4.7f), (-96f, -46.5f, 4.3f), (-74f, -44f, 5.1f), (-52f, -38.5f, 4.6f),
            (-30f, -45.5f, 4.4f), (-12f, -44.2f, 4.0f), (0f, -44f, 4.0f), (11f, -44f, 4.2f),
            (24f, -40.5f, 4.8f), (37f, -36f, 4.4f), (48f, -31.5f, 4.1f), (56f, -27.8f, 3.8f),
            (62f, -26f, 3.6f), (70f, -22f, 3.4f), (80f, -17f, 3.2f)
        };

        private static List<(Vector3 point, float halfWidth)> _riverSamples;

        [MenuItem("Tools/Tower Defense/Base/Rebuild River System")]
        public static void RebuildRiverSystem()
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
                Debug.LogError("[BaseRiver] MainMenu has no BaseWorld/Environment. Run Setup Base Scene first.");
                return;
            }

            // Legacy straight river (slabs + bridge): keep the bridge, drop the slabs.
            Transform legacy = environment.Find("River");
            if (legacy != null)
            {
                if (environment.Find("Bridge") == null)
                {
                    var bridgeRoot = new GameObject("Bridge").transform;
                    bridgeRoot.SetParent(environment, false);
                    PlaceBridge(bridgeRoot);
                    SetLayerRecursively(bridgeRoot.gameObject, BaseLayer);
                    MarkEnvironmentStatic(bridgeRoot.gameObject);
                    DisableDecorColliders(bridgeRoot.gameObject);
                }

                Object.DestroyImmediate(legacy.gameObject);
            }

            Transform old = world.transform.Find(RiverSystemName);
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            // The ground paint, shoreline props and the cliff cave cut all follow the river, so they rebuild too.
            EditorSceneManager.SaveScene(scene);
            RebuildEnvironmentDressing();

            GameObject river = BuildRiverSystem(world.transform);
            SetLayerRecursively(river, BaseLayer);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            ReportMissing();
            Debug.Log("[BaseRiver] River system rebuilt.");
        }

        // ------------------------------------------------------------------ Shape

        private static List<(Vector3 point, float halfWidth)> RiverSamples()
        {
            if (_riverSamples != null)
            {
                return _riverSamples;
            }

            _riverSamples = new List<(Vector3, float)>();
            int n = RiverControl.Length;
            for (int i = 0; i < n - 1; i++)
            {
                var p0 = Control(Mathf.Max(0, i - 1));
                var p1 = Control(i);
                var p2 = Control(i + 1);
                var p3 = Control(Mathf.Min(n - 1, i + 2));
                float length = Vector2.Distance(new Vector2(p1.x, p1.z), new Vector2(p2.x, p2.z));
                int steps = Mathf.Max(2, Mathf.CeilToInt(length / 1.6f));
                for (int s = 0; s < steps; s++)
                {
                    float t = s / (float)steps;
                    Vector3 point = CatmullRom(p0, p1, p2, p3, t);
                    float halfWidth = Mathf.Lerp(RiverControl[i].halfWidth, RiverControl[i + 1].halfWidth, t);
                    _riverSamples.Add((new Vector3(point.x, RiverWaterY, point.z), halfWidth));
                }
            }

            (float lx, float lz, float lw) = RiverControl[n - 1];
            _riverSamples.Add((new Vector3(lx, RiverWaterY, lz), lw));
            return _riverSamples;

            Vector3 Control(int index) => new Vector3(RiverControl[index].x, 0f, RiverControl[index].z);
        }

        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        /// <summary>Metres from <paramref name="q"/> (world XZ) to the nearest river bank: negative in the water,
        /// positive on land. Far from the river it returns a large value without scanning the curve.</summary>
        private static float RiverBankDistance(Vector2 q)
        {
            if (q.y > -12f || q.y < -62f)
            {
                return 100f;
            }

            List<(Vector3 point, float halfWidth)> samples = RiverSamples();
            float best = float.MaxValue;
            for (int i = 0; i < samples.Count - 1; i++)
            {
                var a = new Vector2(samples[i].point.x, samples[i].point.z);
                var b = new Vector2(samples[i + 1].point.x, samples[i + 1].point.z);
                Vector2 ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
                float d = Vector2.Distance(q, a + ab * t) - Mathf.Lerp(samples[i].halfWidth, samples[i + 1].halfWidth, t);
                if (d < best)
                {
                    best = d;
                }
            }

            return best;
        }

        private static Vector3 RiverSide(int index)
        {
            List<(Vector3 point, float halfWidth)> samples = RiverSamples();
            Vector3 a = samples[Mathf.Max(0, index - 1)].point;
            Vector3 b = samples[Mathf.Min(samples.Count - 1, index + 1)].point;
            Vector3 tangent = b - a;
            tangent.y = 0f;
            return Vector3.Cross(Vector3.up, tangent.normalized);
        }

        private static int NearestRiverSample(float x)
        {
            List<(Vector3 point, float halfWidth)> samples = RiverSamples();
            int best = 0;
            for (int i = 1; i < samples.Count; i++)
            {
                if (Mathf.Abs(samples[i].point.x - x) < Mathf.Abs(samples[best].point.x - x))
                {
                    best = i;
                }
            }

            return best;
        }

        /// <summary>True for east-cliff rim samples in front of the cave: their bottom tier is not built, leaving
        /// the opening that the cave arch frames.</summary>
        private static bool IsCaveRimSample(Vector3 rimPoint)
        {
            return rimPoint.x > CliffEast - 1f && Mathf.Abs(rimPoint.z - CaveZ) < CaveHalfSpan;
        }

        // ------------------------------------------------------------------ Shoreline props

        /// <summary>Small irregular clusters on the banks - a stone or two, grass, reeds, the odd flower - on roughly a
        /// third of the shoreline, the rest left open. Kept away from the bridge and the cave mouth. Static scenery,
        /// so it lives under Environment (static batched).</summary>
        private static void BuildRiverShoreProps(Transform environment, NaturePools pools, List<(Vector3, float)> placed,
            System.Random random)
        {
            var root = new GameObject("RiverShoreProps").transform;
            root.SetParent(environment, false);
            List<(Vector3 point, float halfWidth)> samples = RiverSamples();
            GameObject[] boulders = EnsureBoulderPrefabs();

            int i = 3;
            int cluster = 0;
            while (i < samples.Count - 3)
            {
                i += 4 + random.Next(6);
                if (i >= samples.Count)
                {
                    break;
                }

                Vector3 p = samples[i].point;
                bool nearBridge = Mathf.Abs(p.x) < 9f;
                bool nearCave = p.x > CliffEast - 8f;
                if (nearBridge || nearCave || Mathf.Abs(p.x) > GroundHalfSize - 6f || random.NextDouble() > 0.45)
                {
                    continue;
                }

                float sideSign = random.NextDouble() < 0.5 ? -1f : 1f;
                Vector3 side = RiverSide(i) * sideSign;
                Vector3 bank = new Vector3(p.x, 0f, p.z) + side * (samples[i].halfWidth + 0.5f);
                var group = new GameObject("Shore_" + cluster++).transform;
                group.SetParent(root, false);

                // A stone right at the waterline, and grass/reeds a little further up the bank.
                var stone = (GameObject)PrefabUtility.InstantiatePrefab(boulders[random.Next(boulders.Length)], group);
                stone.transform.position = bank + side * Range(random, -0.2f, 0.4f);
                stone.transform.rotation = Quaternion.Euler(0f, Range(random, 0f, 360f), 0f);
                stone.transform.localScale = Vector3.one * Range(random, 0.35f, 0.7f);
                placed.Add((stone.transform.position, 0.8f));

                int tufts = 2 + random.Next(3);
                for (int t = 0; t < tufts; t++)
                {
                    Vector3 along = new Vector3(side.z, 0f, -side.x) * Range(random, -2.2f, 2.2f);
                    Vector3 spot = bank + side * Range(random, 0.6f, 2f) + along;
                    var tuft = PlaceNature(group, t == 0 && random.NextDouble() < 0.6 ? pools.Flowers : pools.Grass, spot, random);
                    if (tuft != null)
                    {
                        placed.Add((spot, 0.4f));
                    }
                }
            }
        }

        // ------------------------------------------------------------------ Build

        private static GameObject BuildRiverSystem(Transform worldRoot)
        {
            EnsureFolder(RiverPrefabFolder);
            EnsureFolder(RiverMaterialFolder);
            EnsureFolder(RiverMeshFolder);

            var root = new GameObject(RiverSystemName);
            root.transform.SetParent(worldRoot, false);

            var waterLevelObject = new GameObject("RiverWaterLevel");
            waterLevelObject.transform.SetParent(root.transform, false);
            waterLevelObject.transform.position = new Vector3(0f, RiverWaterY, -40f);
            var waterLevel = waterLevelObject.AddComponent<RiverWaterLevel>();

            // Geometry.
            var geometry = new GameObject("RiverGeometry").transform;
            geometry.SetParent(root.transform, false);
            BuildRiverMesh(geometry);

            var channel = root.AddComponent<RiverChannel>();
            List<(Vector3 point, float halfWidth)> samples = RiverSamples();
            var centre = new List<Vector3>();
            var widths = new List<float>();
            for (int i = 0; i < samples.Count; i += 2)
            {
                centre.Add(samples[i].point);
                widths.Add(samples[i].halfWidth);
            }

            channel.SetShape(centre.ToArray(), widths.ToArray());

            // Cave.
            var cave = new GameObject("Cave").transform;
            cave.SetParent(root.transform, false);
            (RiverCaveTransition transition, Transform[] foamPoints) = BuildCave(cave);

            // FX pool.
            var fx = new GameObject("FX").transform;
            fx.SetParent(root.transform, false);
            WaterRipplePool ripples = BuildRipplePool(fx);

            // Boats.
            var boatsRoot = new GameObject("Boats").transform;
            boatsRoot.SetParent(root.transform, false);
            RiverBoatMover[] boats = BuildBoats(boatsRoot, waterLevel, transition);

            // Ducks.
            var ducksRoot = new GameObject("Ducks").transform;
            ducksRoot.SetParent(root.transform, false);
            RiverDuckSwimmer[] ducks = BuildDucks(ducksRoot, waterLevel, channel);

            var manager = root.AddComponent<RiverAmbientManager>();
            var so = new SerializedObject(manager);
            so.FindProperty("_channel").objectReferenceValue = channel;
            so.FindProperty("_water").objectReferenceValue = waterLevel;
            so.FindProperty("_ripples").objectReferenceValue = ripples;
            SetArray(so.FindProperty("_boats"), boats);
            SetArray(so.FindProperty("_ducks"), ducks);
            SetArray(so.FindProperty("_foamPoints"), foamPoints);
            so.ApplyModifiedPropertiesWithoutUndo();

            foreach (Collider c in root.GetComponentsInChildren<Collider>(true))
            {
                c.enabled = false;
            }

            return root;
        }

        private static void SetArray<T>(SerializedProperty property, T[] values) where T : Object
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }

        // ------------------------------------------------------------------ Water mesh

        private static void BuildRiverMesh(Transform parent)
        {
            List<(Vector3 point, float halfWidth)> samples = RiverSamples();
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            float along = 0f;

            for (int i = 0; i < samples.Count; i++)
            {
                if (i > 0)
                {
                    along += Vector3.Distance(samples[i - 1].point, samples[i].point);
                }

                Vector3 side = RiverSide(i);
                Vector3 p = samples[i].point;
                float w = samples[i].halfWidth;

                // Darkness ramps up from just outside the cave mouth to deep inside.
                float cave = Mathf.Clamp01((Vector3.Dot(p - CaveMouth, CaveDirection) + 2f) / 5.5f);
                cave = cave * cave * (3f - 2f * cave);
                var color = new Color(cave, 0f, 0f, 1f);

                vertices.Add(p - side * w);
                vertices.Add(p + side * w);
                uvs.Add(new Vector2(along / 8f, 0f));
                uvs.Add(new Vector2(along / 8f, 1f));
                colors.Add(color);
                colors.Add(color);

                if (i > 0)
                {
                    int a = (i - 1) * 2;
                    int b = i * 2;
                    AddUpTriangle(vertices, triangles, a, a + 1, b + 1);
                    AddUpTriangle(vertices, triangles, a, b + 1, b);
                }
            }

            var mesh = SaveMesh("SM_River_Main", vertices, triangles, uvs, colors, Vector3.up);

            var go = new GameObject("River_Main");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = EnsureWaterMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        /// <summary>Adds a triangle wound to face up (the camera is always above the water).</summary>
        private static void AddUpTriangle(List<Vector3> vertices, List<int> triangles, int a, int b, int c)
        {
            Vector3 normal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
            if (normal.y < 0f)
            {
                (b, c) = (c, b);
            }

            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
        }

        private static Mesh SaveMesh(string name, List<Vector3> vertices, List<int> triangles, List<Vector2> uvs,
            List<Color> colors, Vector3? flatNormal)
        {
            string path = RiverMeshFolder + "/" + name + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool isNew = mesh == null;
            mesh = mesh != null ? mesh : new Mesh();
            mesh.Clear();
            mesh.name = name;
            mesh.indexFormat = vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            if (uvs != null) mesh.SetUVs(0, uvs);
            if (colors != null) mesh.SetColors(colors);
            if (flatNormal.HasValue)
            {
                var normals = new Vector3[vertices.Count];
                for (int i = 0; i < normals.Length; i++) normals[i] = flatNormal.Value;
                mesh.normals = normals;
            }
            else
            {
                mesh.RecalculateNormals();
            }

            mesh.RecalculateBounds();
            if (isNew) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        // ------------------------------------------------------------------ Cave

        /// <summary>Direction the tunnel runs into the mountain. Skewed ~27 degrees off the cliff normal so the mouth
        /// turns toward the base camera (which looks north-east) and the dark interior is visible from above.</summary>
        private static readonly Vector3 CaveDirection = new Vector3(1f, 0f, 0.5f).normalized;

        private static Vector3 CaveMouth => new Vector3(CliffEast - 0.3f, 0f, CaveZ);

        private static (RiverCaveTransition transition, Transform[] foam) BuildCave(Transform parent)
        {
            var entrance = new GameObject("RiverCaveEntrance").transform;
            entrance.SetParent(parent, false);
            Vector3 mouth = CaveMouth;
            Vector3 d = CaveDirection;
            Vector3 side = Vector3.Cross(Vector3.up, d);

            // Rock arch framing the opening: outer faces in the cliff rock material, the soffit dark (it is already
            // inside the cave, so it must not catch the sun).
            var arch = new GameObject("CaveRockArch");
            arch.transform.SetParent(entrance, false);
            arch.AddComponent<MeshFilter>().sharedMesh = BuildArchMesh(mouth, d);
            var archRenderer = arch.AddComponent<MeshRenderer>();
            Material caveMaterial = EnsureShaderMaterial("MAT_River_CaveInterior", "AlienDefense/UnlitVertexColor");
            archRenderer.sharedMaterials = new[]
            {
                AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/MAT_Base_CliffRock.mat"), caveMaterial
            };
            archRenderer.shadowCastingMode = ShadowCastingMode.Off;

            // Interior: a tunnel whose colour fades from dark rock to deep teal to near-black (baked vertex colours).
            var interior = new GameObject("CaveInterior");
            interior.transform.SetParent(entrance, false);
            interior.AddComponent<MeshFilter>().sharedMesh = BuildTunnelMesh(mouth, d);
            var interiorRenderer = interior.AddComponent<MeshRenderer>();
            interiorRenderer.sharedMaterial = caveMaterial;
            interiorRenderer.shadowCastingMode = ShadowCastingMode.Off;
            interiorRenderer.receiveShadows = false;

            // Boulders at the arch feet.
            GameObject[] boulders = EnsureBoulderPrefabs();
            var feet = new[]
            {
                (mouth - d * 2.2f + side * (CaveArchOuter - 0.4f), 1.6f, 20f),
                (mouth - d * 3.2f + side * (CaveArchInner + 0.4f), 0.9f, 110f),
                (mouth - d * 2.2f - side * (CaveArchOuter - 0.2f), 1.4f, 250f),
                (mouth - d * 3.5f - side * (CaveArchInner + 0.2f), 0.8f, 60f)
            };
            for (int i = 0; i < feet.Length; i++)
            {
                var rock = (GameObject)PrefabUtility.InstantiatePrefab(boulders[i % boulders.Length], entrance);
                rock.name = "CaveRock_" + i;
                rock.transform.position = feet[i].Item1;
                rock.transform.rotation = Quaternion.Euler(0f, feet[i].Item3, 0f);
                rock.transform.localScale = Vector3.one * feet[i].Item2;
            }

            // A little vegetation on top of the rock hood so it grows out of the cliff rather than sitting on it.
            var hoodPlants = new[]
            {
                ("Env_Bush_02", mouth + d * 2.5f + side * 3.2f, 1.9f),
                ("Env_Bush_01", mouth + d * 4.5f - side * 2.6f, 1.6f),
                ("PT_High_Grass_02_v1", mouth + d * 1.5f - side * 5.5f, 2f),
                ("PT_Grass_02_v1", mouth + d * 5.5f + side * 6f, 2f)
            };
            foreach ((string prefabName, Vector3 position, float scale) in hoodPlants)
            {
                GameObject prefab = FindPrefab(prefabName);
                if (prefab == null)
                {
                    continue;
                }

                var plant = (GameObject)PrefabUtility.InstantiatePrefab(prefab, entrance);
                float lateral = Vector3.Dot(position - mouth, side);
                float height = Mathf.Sqrt(Mathf.Max(0f, CaveArchOuter * CaveArchOuter - lateral * lateral)) - 0.25f;
                plant.transform.position = new Vector3(position.x, height, position.z);
                plant.transform.localScale = Vector3.one * scale;
            }

            // Transition zone: local +Z runs from the mouth into the mountain.
            var zone = new GameObject("CaveTransitionZone");
            zone.transform.SetParent(entrance, false);
            zone.transform.position = mouth + d * 9f + Vector3.up * 2f;
            zone.transform.rotation = Quaternion.LookRotation(d, Vector3.up);
            var transition = zone.AddComponent<RiverCaveTransition>();
            var zso = new SerializedObject(transition);
            zso.FindProperty("_size").vector3Value = new Vector3(CaveArchInner * 2f, 7f, 16f);
            zso.ApplyModifiedPropertiesWithoutUndo();

            // Foam where the water breaks against the rocks at the mouth.
            int mouthSample = NearestRiverSample(mouth.x - 1.5f);
            float mouthHalfWidth = RiverSamples()[mouthSample].halfWidth;
            var foam = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                var point = new GameObject(i == 0 ? "CaveFoam_North" : "CaveFoam_South").transform;
                point.SetParent(entrance, false);
                point.position = mouth - d * 1.4f + side * ((i == 0 ? 1f : -1f) * (mouthHalfWidth - 0.4f)) + Vector3.up * RiverWaterY;
                foam[i] = point;
            }

            return (transition, foam);
        }

        /// <summary>Low-poly half ring standing across the tunnel direction, jittered and flat-shaded so it reads as
        /// carved rock. Submesh 0 = outer rock (lit), submesh 1 = dark soffit (vertex-coloured, unlit).</summary>
        private static Mesh BuildArchMesh(Vector3 mouth, Vector3 d)
        {
            const int segments = 9;
            const float front = -1.2f;
            // Deep enough to roof over the gap left in the bottom cliff tier (the arch doubles as a rock hood).
            const float back = 7f;
            Vector3 side = Vector3.Cross(Vector3.up, d);
            var random = new System.Random(77);
            var inner = new Vector3[segments + 1];
            var outer = new Vector3[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float angle = Mathf.PI * i / segments;
                float c = Mathf.Cos(angle);
                float s = Mathf.Sin(angle);
                float jitterIn = (float)(random.NextDouble() - 0.5) * 0.45f;
                float jitterOut = (float)(random.NextDouble() - 0.5) * 0.9f;
                inner[i] = side * (c * (CaveArchInner + jitterIn)) + Vector3.up * Mathf.Max(0f, s * (CaveArchInner + jitterIn));
                outer[i] = side * (c * (CaveArchOuter + jitterOut)) + Vector3.up * Mathf.Max(0f, s * (CaveArchOuter + jitterOut));
            }

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var colors = new List<Color>();
            var rock = new List<int>();
            var soffit = new List<int>();
            Vector3 frontCenter = mouth + d * front;
            Vector3 backCenter = mouth + d * back;

            void Quad(List<int> target, Vector3 a, Vector3 b, Vector3 c, Vector3 e, Vector3 facing, Color ca, Color cb)
            {
                Vector3 n = Vector3.Cross(b - a, c - a);
                if (Vector3.Dot(n, facing) < 0f)
                {
                    (b, e) = (e, b);
                    n = -n;
                }

                n.Normalize();
                int start = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(e);
                for (int k = 0; k < 4; k++) normals.Add(n);
                colors.Add(ca); colors.Add(ca); colors.Add(cb); colors.Add(cb);
                target.Add(start); target.Add(start + 1); target.Add(start + 2);
                target.Add(start); target.Add(start + 2); target.Add(start + 3);
            }

            Color lip = new Color(0.2f, 0.21f, 0.2f);
            Color inside = new Color(0.06f, 0.12f, 0.14f);
            for (int i = 0; i < segments; i++)
            {
                // Front face (toward the base).
                Quad(rock, frontCenter + inner[i], frontCenter + inner[i + 1], frontCenter + outer[i + 1], frontCenter + outer[i],
                    -d, Color.white, Color.white);
                // Outer rim.
                Vector3 outward = (outer[i] + outer[i + 1]).normalized;
                Quad(rock, frontCenter + outer[i], frontCenter + outer[i + 1], backCenter + outer[i + 1], backCenter + outer[i],
                    outward, Color.white, Color.white);
                // Soffit: already the inside of the cave - dark, darker toward the back.
                Vector3 inward = -(inner[i] + inner[i + 1]).normalized;
                Quad(soffit, frontCenter + inner[i], frontCenter + inner[i + 1], backCenter + inner[i + 1], backCenter + inner[i],
                    inward, lip, inside);
            }

            string path = RiverMeshFolder + "/SM_River_CaveArch.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool isNew = mesh == null;
            mesh = mesh != null ? mesh : new Mesh();
            mesh.Clear();
            mesh.name = "SM_River_CaveArch";
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(rock, 0);
            mesh.SetTriangles(soffit, 1);
            mesh.RecalculateBounds();
            if (isNew) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        /// <summary>Half-cylinder tunnel running from the mouth into the cliff along d, with a floor and a back cap.
        /// Vertex colours go dark rock -> deep teal -> near black, so the river visibly continues and fades into
        /// depth - without any light, fog or extra pass.</summary>
        private static Mesh BuildTunnelMesh(Vector3 mouth, Vector3 d)
        {
            const int arcSegments = 8;
            float[] depths = { 6.5f, 9f, 12f, 15f, 18f, CaveDepth };
            // The high base camera only sees a few metres into the mouth, so the darkening starts right there:
            // dark rock at the lip, deep teal within two metres, near black at the back (never pure black).
            Color[] shades =
            {
                new Color(0.06f, 0.12f, 0.14f), new Color(0.05f, 0.1f, 0.12f), new Color(0.04f, 0.08f, 0.1f),
                new Color(0.03f, 0.07f, 0.09f), new Color(0.02f, 0.05f, 0.07f), new Color(0.02f, 0.04f, 0.06f)
            };
            float radius = CaveArchInner - 0.15f;
            Vector3 side = Vector3.Cross(Vector3.up, d);

            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();

            Vector3 Ring(int ring, int k)
            {
                float angle = Mathf.PI * k / arcSegments;
                return mouth + d * depths[ring] + side * (Mathf.Cos(angle) * radius) + Vector3.up * (Mathf.Sin(angle) * radius);
            }

            void Tri(Vector3 a, Vector3 b, Vector3 c, Color ca, Color cb, Color cc, Vector3 facingPoint)
            {
                // Faces inward: toward the tunnel axis.
                Vector3 n = Vector3.Cross(b - a, c - a);
                Vector3 centroid = (a + b + c) / 3f;
                if (Vector3.Dot(n, facingPoint - centroid) < 0f)
                {
                    (b, c) = (c, b);
                    (cb, cc) = (cc, cb);
                }

                int s = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c);
                colors.Add(ca); colors.Add(cb); colors.Add(cc);
                triangles.Add(s); triangles.Add(s + 1); triangles.Add(s + 2);
            }

            for (int ring = 0; ring < depths.Length - 1; ring++)
            {
                Vector3 axis = mouth + d * ((depths[ring] + depths[ring + 1]) * 0.5f) + Vector3.up * 0.5f;
                for (int k = 0; k < arcSegments; k++)
                {
                    Vector3 a = Ring(ring, k), b = Ring(ring, k + 1), c = Ring(ring + 1, k + 1), e = Ring(ring + 1, k);
                    Tri(a, b, c, shades[ring], shades[ring], shades[ring + 1], axis);
                    Tri(a, c, e, shades[ring], shades[ring + 1], shades[ring + 1], axis);
                }

                // Floor beside the water.
                Vector3 f0 = mouth + d * depths[ring] - side * radius + Vector3.up * 0.02f;
                Vector3 f1 = mouth + d * depths[ring] + side * radius + Vector3.up * 0.02f;
                Vector3 f2 = mouth + d * depths[ring + 1] + side * radius + Vector3.up * 0.02f;
                Vector3 f3 = mouth + d * depths[ring + 1] - side * radius + Vector3.up * 0.02f;
                Vector3 above = axis + Vector3.up * 3f;
                Tri(f0, f1, f2, shades[ring] * 0.8f, shades[ring] * 0.8f, shades[ring + 1] * 0.8f, above);
                Tri(f0, f2, f3, shades[ring] * 0.8f, shades[ring + 1] * 0.8f, shades[ring + 1] * 0.8f, above);
            }

            // Back cap: the far darkness.
            int last = depths.Length - 1;
            Vector3 capCenter = mouth + d * depths[last];
            for (int k = 0; k < arcSegments; k++)
            {
                Tri(capCenter, Ring(last, k), Ring(last, k + 1), shades[last], shades[last], shades[last],
                    capCenter - d * 5f + Vector3.up);
            }

            string path = RiverMeshFolder + "/SM_River_CaveTunnel.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool isNew = mesh == null;
            mesh = mesh != null ? mesh : new Mesh();
            mesh.Clear();
            mesh.name = "SM_River_CaveTunnel";
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            if (isNew) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        // ------------------------------------------------------------------ FX

        private static WaterRipplePool BuildRipplePool(Transform parent)
        {
            var go = new GameObject("RipplePool");
            go.transform.SetParent(parent, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.playOnAwake = true;
            main.maxParticles = 120;
            main.startSpeed = 0f;
            main.startLifetime = 1f;
            main.startSize = 0.5f;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;

            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.45f, 1f, 1.7f));

            ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(0.92f, 1f, 1f), 0f), new GradientColorKey(new Color(0.8f, 0.97f, 1f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.85f, 0.12f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            renderer.sharedMaterial = EnsureRippleMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingFudge = -10f;

            return go.AddComponent<WaterRipplePool>();
        }

        // ------------------------------------------------------------------ Boats

        private static RiverBoatMover[] BuildBoats(Transform parent, RiverWaterLevel water, RiverCaveTransition cave)
        {
            GameObject rowBoat = EnsureBoatPrefab("BP_Boat_Row", "NormalBoat");
            GameObject longBoat = EnsureBoatPrefab("BP_Boat_Long", "LongNormalBoat");
            var movers = new List<RiverBoatMover>();

            // Route A: starts under the bridge (the deck hides the spawn), follows the river into the cave.
            var path = new GameObject("RiverBoatPath_A").transform;
            path.SetParent(parent, false);
            path.gameObject.AddComponent<RiverBoatPath>();
            List<(Vector3 point, float halfWidth)> samples = RiverSamples();
            int startIndex = NearestRiverSample(0f);
            int waypoint = 0;
            for (int i = startIndex; i < samples.Count; i += 4)
            {
                var wp = new GameObject("Waypoint_" + waypoint.ToString("00")).transform;
                wp.SetParent(path, false);
                wp.position = samples[i].point;
                waypoint++;
            }

            if (rowBoat != null)
            {
                var boat = (GameObject)PrefabUtility.InstantiatePrefab(rowBoat, parent);
                boat.name = "Boat_01";
                boat.transform.position = samples[startIndex].point;
                var mover = boat.AddComponent<RiverBoatMover>();
                var so = new SerializedObject(mover);
                so.FindProperty("_path").objectReferenceValue = path.GetComponent<RiverBoatPath>();
                so.FindProperty("_water").objectReferenceValue = water;
                so.FindProperty("_cave").objectReferenceValue = cave;
                so.FindProperty("_moveSpeed").floatValue = 1.1f;
                so.FindProperty("_waterlineOffset").floatValue = -0.15f;
                so.FindProperty("_randomStartPosition").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
                movers.Add(mover);
            }

            // Moored boat by the south bank, west of the bridge.
            if (longBoat != null)
            {
                int index = NearestRiverSample(-24f);
                Vector3 side = RiverSide(index);
                Vector3 p = samples[index].point - side * (samples[index].halfWidth - 1.6f);
                var boat = (GameObject)PrefabUtility.InstantiatePrefab(longBoat, parent);
                boat.name = "Boat_02_Moored";
                boat.transform.position = p;
                Vector3 along = samples[Mathf.Min(samples.Count - 1, index + 1)].point - samples[index].point;
                boat.transform.rotation = Quaternion.LookRotation(new Vector3(along.x, 0f, along.z).normalized, Vector3.up) *
                    Quaternion.Euler(0f, 12f, 0f);
                var mover = boat.AddComponent<RiverBoatMover>();
                var so = new SerializedObject(mover);
                so.FindProperty("_water").objectReferenceValue = water;
                so.FindProperty("_moveSpeed").floatValue = 0f;
                so.FindProperty("_waterlineOffset").floatValue = -0.18f;
                so.FindProperty("_bobHeight").floatValue = 0.035f;
                so.ApplyModifiedPropertiesWithoutUndo();
                movers.Add(mover);
            }

            return movers.ToArray();
        }

        /// <summary>Wrapper around a pack boat: URP copies of its Standard materials (which render pink in URP),
        /// colliders off, bow along +Z, hull bottom at y = 0.</summary>
        private static GameObject EnsureBoatPrefab(string name, string sourceName)
        {
            string path = RiverPrefabFolder + "/" + name + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                return existing;
            }

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Low Poly Boats & Raft/Low Poly Row Boats/Prefabs/" + sourceName + ".prefab");
            if (source == null)
            {
                Missing.Add("Boat prefab " + sourceName + " (Low Poly Boats & Raft) - boat skipped.");
                return null;
            }

            var root = new GameObject(name);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(source, root.transform);
            // The pack's hulls run along X; the movers steer along +Z.
            model.transform.localRotation = Quaternion.Euler(0f, -90f, 0f) * model.transform.localRotation;

            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    materials[i] = UrpCopy(materials[i]);
                }

                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }

            foreach (Collider c in model.GetComponentsInChildren<Collider>(true))
            {
                c.enabled = false;
            }

            TryGetBounds(model, out Bounds bounds);
            model.transform.position += new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved;
        }

        /// <summary>URP Lit copy of a Built-in Standard material (colour, albedo, normal map), cached by name.</summary>
        private static Material UrpCopy(Material source)
        {
            if (source == null || source.shader.name.StartsWith("Universal Render Pipeline"))
            {
                return source;
            }

            string path = RiverMaterialFolder + "/MAT_River_" + source.name.Replace(" ", "_") + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = source.name, enableInstancing = true };
            if (source.HasProperty("_MainTex") && source.GetTexture("_MainTex") != null)
            {
                material.SetTexture("_BaseMap", source.GetTexture("_MainTex"));
            }

            material.SetColor("_BaseColor", source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white);
            if (source.HasProperty("_BumpMap") && source.GetTexture("_BumpMap") != null)
            {
                material.SetTexture("_BumpMap", source.GetTexture("_BumpMap"));
                material.EnableKeyword("_NORMALMAP");
            }

            material.SetFloat("_Smoothness", 0.15f);
            material.SetFloat("_Metallic", 0f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // ------------------------------------------------------------------ Ducks

        private static RiverDuckSwimmer[] BuildDucks(Transform parent, RiverWaterLevel water, RiverChannel channel)
        {
            GameObject silly = EnsureDuckPrefab("BP_Duck_Silly", "Assets/Duck Model CC0/Duck Model CC0/duck.blend");
            GameObject call = EnsureDuckPrefab("BP_Duck_Call", "Assets/Call Duck Model CC0/Call Duck Model CC0/call_duck.blend");
            var ducks = new List<RiverDuckSwimmer>();

            // Three groups on stretches that are clear of the bridge and the cave, visible from the camera's range.
            var groups = new[]
            {
                ("DuckGroup_A", 28f, 3, new Vector2(18f, 10f)),
                ("DuckGroup_B", -30f, 2, new Vector2(20f, 9f)),
                ("DuckGroup_C", -58f, 2, new Vector2(16f, 9f))
            };

            var random = new System.Random(21);
            int count = 0;
            foreach ((string name, float x, int size, Vector2 area) in groups)
            {
                int index = NearestRiverSample(x);
                Vector3 centre = RiverSamples()[index].point;
                var group = new GameObject(name).transform;
                group.SetParent(parent, false);
                group.position = centre;

                for (int i = 0; i < size; i++)
                {
                    GameObject prefab = (count % 2 == 0 ? silly : call) ?? silly ?? call;
                    if (prefab == null)
                    {
                        continue;
                    }

                    var duck = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
                    duck.name = (prefab == silly ? "Duck_Silly_" : "Duck_Call_") + count.ToString("00");
                    duck.transform.position = centre + new Vector3((float)(random.NextDouble() - 0.5) * 6f, 0f,
                        (float)(random.NextDouble() - 0.5) * 2.5f);
                    // The models are real-duck sized (~1.1 m); at base-camera distance that is a speck.
                    duck.transform.localScale = Vector3.one * 1.7f;
                    var swimmer = duck.AddComponent<RiverDuckSwimmer>();
                    var so = new SerializedObject(swimmer);
                    so.FindProperty("_water").objectReferenceValue = water;
                    so.FindProperty("_channel").objectReferenceValue = channel;
                    so.FindProperty("_riverAreaCenter").objectReferenceValue = group;
                    so.FindProperty("_swimAreaSize").vector2Value = area;
                    so.FindProperty("_waterlineOffset").floatValue = -0.42f;
                    so.FindProperty("_separationDistance").floatValue = 2f;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    ducks.Add(swimmer);
                    count++;
                }
            }

            return ducks.ToArray();
        }

        /// <summary>Re-meshes a duck .blend into a light asset: every part combined into one mesh (a submesh per
        /// material), decimated to ~900 triangles, materials copied into the project. The prefab no longer needs the
        /// .blend - or Blender - at import time.</summary>
        private static GameObject EnsureDuckPrefab(string name, string blendPath)
        {
            string path = RiverPrefabFolder + "/" + name + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                return existing;
            }

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(blendPath);
            if (source == null)
            {
                Missing.Add("Duck model " + blendPath + " (needs Blender to import .blend) - duck skipped.");
                return null;
            }

            var instance = (GameObject)Object.Instantiate(source);
            instance.transform.position = Vector3.zero;
            var byMaterial = new Dictionary<Material, List<CombineInstance>>();
            var order = new List<Material>();
            Matrix4x4 toRoot = instance.transform.worldToLocalMatrix;
            foreach (MeshFilter filter in instance.GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null || filter.sharedMesh == null)
                {
                    continue;
                }

                for (int sub = 0; sub < filter.sharedMesh.subMeshCount; sub++)
                {
                    Material material = renderer.sharedMaterials[Mathf.Min(sub, renderer.sharedMaterials.Length - 1)];
                    if (!byMaterial.TryGetValue(material, out List<CombineInstance> list))
                    {
                        list = new List<CombineInstance>();
                        byMaterial[material] = list;
                        order.Add(material);
                    }

                    list.Add(new CombineInstance
                    {
                        mesh = filter.sharedMesh,
                        subMeshIndex = sub,
                        transform = toRoot * filter.transform.localToWorldMatrix
                    });
                }
            }

            var parts = new CombineInstance[order.Count];
            var temporary = new List<Mesh>();
            for (int i = 0; i < order.Count; i++)
            {
                var part = new Mesh { indexFormat = IndexFormat.UInt32 };
                part.CombineMeshes(byMaterial[order[i]].ToArray(), true, true);
                temporary.Add(part);
                parts[i] = new CombineInstance { mesh = part, transform = Matrix4x4.identity };
            }

            var combined = new Mesh { indexFormat = IndexFormat.UInt32 };
            combined.CombineMeshes(parts, false, false);
            Object.DestroyImmediate(instance);
            foreach (Mesh part in temporary) Object.DestroyImmediate(part);

            Mesh simplified = MeshDecimator.Decimate(combined, 900);
            Object.DestroyImmediate(combined);
            simplified.name = "SM_" + name.Replace("BP_", "");
            string meshPath = RiverMeshFolder + "/" + simplified.name + ".asset";
            var oldMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (oldMesh != null)
            {
                oldMesh.Clear();
                EditorUtility.CopySerialized(simplified, oldMesh);
                Object.DestroyImmediate(simplified);
                simplified = oldMesh;
            }
            else
            {
                AssetDatabase.CreateAsset(simplified, meshPath);
            }

            var materials = new Material[order.Count];
            for (int i = 0; i < order.Count; i++)
            {
                string matPath = RiverMaterialFolder + "/MAT_" + name.Replace("BP_", "") + "_" + order[i].name + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null)
                {
                    mat = new Material(order[i]) { enableInstancing = true };
                    AssetDatabase.CreateAsset(mat, matPath);
                }

                materials[i] = mat;
            }

            var root = new GameObject(name);
            var modelObject = new GameObject("Model");
            modelObject.transform.SetParent(root.transform, false);
            modelObject.AddComponent<MeshFilter>().sharedMesh = simplified;
            var meshRenderer = modelObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterials = materials;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            TryGetBounds(modelObject, out Bounds bounds);
            modelObject.transform.position += new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved;
        }

        // ------------------------------------------------------------------ Materials / textures

        private static Material EnsureWaterMaterial()
        {
            Material material = EnsureShaderMaterial("MAT_River_Water", "AlienDefense/StylizedRiver");
            if (material != null && material.GetTexture("_NoiseTex") == null)
            {
                material.SetTexture("_NoiseTex", EnsureNoiseTexture());
                EditorUtility.SetDirty(material);
            }

            return material;
        }

        private static Material EnsureShaderMaterial(string name, string shaderName)
        {
            string path = RiverMaterialFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                return material;
            }

            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Missing.Add("Shader " + shaderName + " not compiled yet - re-run the river tool.");
                return null;
            }

            material = new Material(shader) { name = name, enableInstancing = true };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Material EnsureRippleMaterial()
        {
            string path = RiverMaterialFolder + "/MAT_River_Ripple.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                return material;
            }

            var template = AssetDatabase.LoadAssetAtPath<Material>(
                "Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat");
            material = template != null ? new Material(template) : new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            material.name = "MAT_River_Ripple";
            material.SetTexture("_BaseMap", EnsureRingTexture());
            material.SetColor("_BaseColor", Color.white);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Texture2D EnsureRingTexture()
        {
            string path = TexturesFolder + "/T_River_Ring.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;

            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f)) / (size * 0.5f);
                    float ring = Smooth(0.55f, 0.74f, d) * (1f - Smooth(0.8f, 0.98f, d));
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, ring));
                }
            }

            return SaveTexture(texture, path, TextureWrapMode.Clamp, true);
        }

        /// <summary>Tileable value noise (four offset Perlin samples blended across the tile) for the water flow.</summary>
        private static Texture2D EnsureNoiseTexture()
        {
            string path = TexturesFolder + "/T_River_Noise.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;

            const int size = 256;
            const float frequency = 6f / size;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float fx = x / (float)size;
                    float fy = y / (float)size;
                    float a = Mathf.PerlinNoise(x * frequency, y * frequency);
                    float b = Mathf.PerlinNoise((x - size) * frequency, y * frequency);
                    float c = Mathf.PerlinNoise(x * frequency, (y - size) * frequency);
                    float d = Mathf.PerlinNoise((x - size) * frequency, (y - size) * frequency);
                    float v = Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
                    float detail = Mathf.Lerp(
                        Mathf.Lerp(Mathf.PerlinNoise(x * frequency * 3f + 50f, y * frequency * 3f + 50f),
                            Mathf.PerlinNoise((x - size) * frequency * 3f + 50f, y * frequency * 3f + 50f), fx),
                        Mathf.Lerp(Mathf.PerlinNoise(x * frequency * 3f + 50f, (y - size) * frequency * 3f + 50f),
                            Mathf.PerlinNoise((x - size) * frequency * 3f + 50f, (y - size) * frequency * 3f + 50f), fx), fy);
                    float n = Mathf.Clamp01(v * 0.7f + detail * 0.3f);
                    texture.SetPixel(x, y, new Color(n, n, n, 1f));
                }
            }

            return SaveTexture(texture, path, TextureWrapMode.Repeat, false);
        }

        private static Texture2D SaveTexture(Texture2D texture, string path, TextureWrapMode wrap, bool alpha)
        {
            texture.Apply();
            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = wrap;
            importer.alphaIsTransparency = alpha;
            importer.sRGBTexture = alpha;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
