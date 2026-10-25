using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AlienDefense.EditorTools
{
    /// <summary>The map border: layered low-poly cliffs all around the playable area - lower face, grass ledge,
    /// middle rock face, second ledge, upper face, wooded plateau - with the heights, ledge depths and the rim line
    /// itself varying along the border, so it never reads as one straight wall. Generated as flat-shaded,
    /// vertex-coloured meshes (one shared material), split into chunks so the camera only draws what it sees.
    /// The ledges report spots for trees, bushes and boulders. No wall colliders: the UFO's CharacterController
    /// collides with no level layer in this project, so - as in Level_01 - the LevelBounds rectangle set just inside
    /// the rim is what keeps it in, and a Default-layer wall would only catch the enemies' ground raycast.</summary>
    public static partial class Level02Builder
    {
        private const float RimSpacing = 1.35f;
        private const float RimCornerRadius = 9f;
        private const int CliffChunks = 10;

        private struct LedgeSpot
        {
            public Vector3 Position;
            public Vector2 Outward;
            public int Tier; // 1 = first ledge, 2 = second ledge, 3 = plateau
        }

        private static readonly Color[] RockTones = { Hex("#B6A88C"), Hex("#A59779"), Hex("#C4B698"), Hex("#9C917B"), Hex("#B0A184") };
        private static readonly Color[] LedgeGrassTones = { Hex("#8EBB4E"), Hex("#84B347"), Hex("#98C458"), Hex("#7FAB44") };
        private static readonly Color LedgeDirt = Hex("#B79C6C");

        /// <summary>Closed rim polyline (rounded rectangle pushed in/out by smooth noise), counter-clockwise seen from
        /// above, with outward normals.</summary>
        private static void BuildRim(out List<Vector2> points, out List<Vector2> normals)
        {
            var raw = new List<Vector2>();
            float r = RimCornerRadius;
            void Arc(Vector2 centre, float from, float to)
            {
                int steps = Mathf.CeilToInt(Mathf.Abs(to - from) * Mathf.Deg2Rad * r / RimSpacing);
                for (int i = 0; i < steps; i++)
                {
                    float a = Mathf.Lerp(from, to, i / (float)steps) * Mathf.Deg2Rad;
                    raw.Add(centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
                }
            }

            void Line(Vector2 a, Vector2 b)
            {
                int steps = Mathf.CeilToInt(Vector2.Distance(a, b) / RimSpacing);
                for (int i = 0; i < steps; i++)
                {
                    raw.Add(Vector2.Lerp(a, b, i / (float)steps));
                }
            }

            // Counter-clockwise in (x, z): south edge (+x side) ... walk min z -> max z along x = RimMaxX etc.
            Line(new Vector2(RimMinX + r, RimMinZ), new Vector2(RimMaxX - r, RimMinZ));
            Arc(new Vector2(RimMaxX - r, RimMinZ + r), -90f, 0f);
            Line(new Vector2(RimMaxX, RimMinZ + r), new Vector2(RimMaxX, RimMaxZ - r));
            Arc(new Vector2(RimMaxX - r, RimMaxZ - r), 0f, 90f);
            Line(new Vector2(RimMaxX - r, RimMaxZ), new Vector2(RimMinX + r, RimMaxZ));
            Arc(new Vector2(RimMinX + r, RimMaxZ - r), 90f, 180f);
            Line(new Vector2(RimMinX, RimMaxZ - r), new Vector2(RimMinX, RimMinZ + r));
            Arc(new Vector2(RimMinX + r, RimMinZ + r), 180f, 270f);

            int n = raw.Count;
            normals = new List<Vector2>(n);
            for (int i = 0; i < n; i++)
            {
                Vector2 tangent = raw[(i + 1) % n] - raw[(i - 1 + n) % n];
                normals.Add(new Vector2(tangent.y, -tangent.x).normalized); // right of a CCW walk = outward
            }

            points = new List<Vector2>(n);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float push = (Fbm(t * 22f, 0.5f, 60, 3, 22) - 0.5f) * 3.2f; // periodic along the loop
                points.Add(raw[i] + normals[i] * push);
            }
        }

        private static List<LedgeSpot> BuildCliffs(Transform parent, Material material)
        {
            BuildRim(out List<Vector2> rim, out List<Vector2> normals);
            int n = rim.Count;
            var ledges = new List<LedgeSpot>();

            // Per-sample profile: (outward distance, height above GroundY). Tier heights and ledge depths vary with
            // smooth periodic noise along the loop; face points get per-sample jitter for the faceted rock look.
            var profiles = new Vector2[n][];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float nA = Fbm(t * 9f, 1.5f, 61, 2, 9);
                float nB = Fbm(t * 13f, 2.5f, 62, 2, 13);
                float nC = Fbm(t * 7f, 3.5f, 63, 2, 7);
                float nD = Fbm(t * 17f, 4.5f, 64, 2, 17);
                float nE = Fbm(t * 11f, 5.5f, 65, 2, 11);

                float h1 = 2.6f + nA * 2.6f;
                float h2 = h1 + 3.4f + nB * 3f;
                float h3 = h2 + 4f + nC * 4.5f;
                float l1 = 2f + nD * 4.2f;
                float l2 = 2f + nE * 3.6f;
                float f1 = 0.9f + Hash1(i, 70) * 0.5f;
                float f2 = 1f + Hash1(i, 71) * 0.7f;
                float f3 = 1.1f + Hash1(i, 72) * 0.9f;

                float j(int k) => (Hash(i, k, 73) - 0.5f) * 0.55f;
                float jh(int k) => (Hash(i, k, 74) - 0.5f) * 0.5f;

                float d = 0f;
                var p = new List<Vector2>
                {
                    new Vector2(-1.2f, -1.4f),                                   // below ground, inside
                    new Vector2(-0.15f + j(0) * 0.4f, -0.2f),                    // foot
                    new Vector2(f1 * 0.45f + j(1), h1 * 0.5f + jh(1)),           // face 1
                    new Vector2(d = f1, h1),                                     // ledge 1 lip
                    new Vector2(d + l1 * 0.5f + j(2) * 0.5f, h1 + 0.18f + jh(2) * 0.3f),
                    new Vector2(d += l1, h1 + 0.1f),                             // back of ledge 1
                    new Vector2(d + f2 * 0.5f + j(3), (h1 + h2) * 0.5f + jh(3)), // face 2
                    new Vector2(d += f2, h2),                                    // ledge 2 lip
                    new Vector2(d + l2 * 0.5f + j(4) * 0.5f, h2 + 0.2f + jh(4) * 0.3f),
                    new Vector2(d += l2, h2 + 0.12f),
                    new Vector2(d + f3 * 0.5f + j(5), (h2 + h3) * 0.5f + jh(5)), // face 3
                    new Vector2(d += f3, h3),                                    // plateau lip
                    new Vector2(d + 6f + j(6), h3 + 0.5f + jh(6)),
                    new Vector2(d + 16f, h3 + 1.4f + nA * 1.5f),
                    new Vector2(d + 34f, h3 + 2.5f + nC * 3f),
                };
                profiles[i] = p.ToArray();

                Vector2 o = normals[i];
                void Spot(float dist, float height, int tier)
                {
                    Vector2 xz = rim[i] + o * dist;
                    ledges.Add(new LedgeSpot { Position = new Vector3(xz.x, GroundY + height, xz.y), Outward = o, Tier = tier });
                }

                if (i % 2 == 0)
                {
                    Spot(f1 + l1 * 0.55f, h1 + 0.15f, 1);
                    Spot(f1 + l1 + f2 + l2 * 0.55f, h2 + 0.15f, 2);
                    Spot(d + 3f + Hash1(i, 75) * 6f, h3 + 0.5f, 3);
                    Spot(d + 10f + Hash1(i, 76) * 8f, h3 + 1.2f, 3);
                }
            }

            var root = new GameObject("Cliffs").transform;
            root.SetParent(parent, false);
            int perChunk = Mathf.CeilToInt(n / (float)CliffChunks);
            for (int c = 0; c < CliffChunks; c++)
            {
                int start = c * perChunk;
                int end = Mathf.Min(n, start + perChunk); // exclusive; the last quad joins sample end (wrapping)
                if (start >= end)
                {
                    break;
                }

                Mesh mesh = BuildCliffChunk(rim, normals, profiles, start, end, n);
                string path = MeshesFolder + "/SM_Level02_Cliff_" + c.ToString("00") + ".asset";
                mesh = SaveMesh(mesh, path);
                var go = new GameObject("Cliff_" + c.ToString("00"));
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
            }

            return ledges;
        }

        private static Mesh BuildCliffChunk(List<Vector2> rim, List<Vector2> normals, Vector2[][] profiles, int start, int end, int n)
        {
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();

            Vector3 Point(int i, int k)
            {
                Vector2 p = profiles[i][k];
                Vector2 xz = rim[i] + normals[i] * p.x;
                return new Vector3(xz.x, GroundY + p.y, xz.y);
            }

            for (int i = start; i < end; i++)
            {
                int next = (i + 1) % n;
                int count = profiles[i].Length;
                for (int k = 0; k < count - 1; k++)
                {
                    Vector3 a = Point(i, k);
                    Vector3 b = Point(next, k);
                    Vector3 c = Point(next, k + 1);
                    Vector3 d = Point(i, k + 1);
                    Vector3 facing = new Vector3(-normals[i].x, 1f, -normals[i].y); // toward the playable area / sky
                    AddCliffTriangle(vertices, colors, triangles, a, c, b, facing, i, k, 0);
                    AddCliffTriangle(vertices, colors, triangles, a, d, c, facing, i, k, 1);
                }
            }

            var mesh = new Mesh { name = "SM_Level02_Cliff" };
            mesh.indexFormat = vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddCliffTriangle(List<Vector3> vertices, List<Color> colors, List<int> triangles, Vector3 a, Vector3 b, Vector3 c, Vector3 facing, int i, int k, int half)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(normal, facing) < 0f)
            {
                // keep every face turned toward the playable area / sky so the camera never meets a back face
                Vector3 t = b;
                b = c;
                c = t;
                normal = -normal;
            }

            normal.Normalize();
            Color color;
            float h = Hash(i * 2 + half, k, 77);
            if (normal.y > 0.72f)
            {
                color = LedgeGrassTones[(int)(h * LedgeGrassTones.Length) % LedgeGrassTones.Length];
                if (Hash(i, k, 78) < 0.12f)
                {
                    color = Color.Lerp(color, LedgeDirt, 0.6f); // worn patches on the ledges
                }
            }
            else if (normal.y > 0.5f)
            {
                color = Color.Lerp(RockTones[(int)(h * RockTones.Length) % RockTones.Length], LedgeGrassTones[0], 0.35f);
            }
            else
            {
                color = RockTones[(int)(h * RockTones.Length) % RockTones.Length];
            }

            int baseIndex = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            colors.Add(color);
            colors.Add(color);
            colors.Add(color);
            triangles.Add(baseIndex);
            triangles.Add(baseIndex + 1);
            triangles.Add(baseIndex + 2);
        }

        private static Mesh SaveMesh(Mesh mesh, string path)
        {
            EnsureFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                existing.Clear();
                EditorUtility.CopySerialized(mesh, existing);
                existing.name = System.IO.Path.GetFileNameWithoutExtension(path);
                Object.DestroyImmediate(mesh);
                EditorUtility.SetDirty(existing);
                return existing;
            }

            mesh.name = System.IO.Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
    }
}
