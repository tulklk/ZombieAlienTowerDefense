using System.Collections.Generic;
using UnityEngine;

namespace AlienDefense.Vfx
{
    /// <summary>Procedural low-poly ice meshes shared by every freeze/stun visual: tall faceted crystals, a shallow
    /// frost patch and tiny shards. All flat-shaded (no shared vertices) with white vertex colour, built once and
    /// cached for the whole session; every mesh has its pivot on its base so it can grow out of the ground.</summary>
    public static class IceCrystalMeshes
    {
        public const int CrystalVariants = 4;

        private static Mesh[] s_crystals;
        private static Mesh s_frostPatch;
        private static Mesh s_shard;

        /// <summary>Unit crystal, base on y = 0 (radius ~1), sharp tip at y = 1.</summary>
        public static Mesh Crystal(int variant)
        {
            if (s_crystals == null || s_crystals.Length != CrystalVariants || s_crystals[0] == null)
            {
                s_crystals = new Mesh[CrystalVariants];
                for (int i = 0; i < CrystalVariants; i++)
                {
                    s_crystals[i] = BuildCrystal(9001 + i * 131);
                }
            }

            return s_crystals[Mathf.Abs(variant) % CrystalVariants];
        }

        /// <summary>Unit frost patch: an irregular, very shallow faceted mound (radius ~1, 0.07 high).</summary>
        public static Mesh FrostPatch()
        {
            if (s_frostPatch == null)
            {
                s_frostPatch = BuildFrostPatch(4051);
            }

            return s_frostPatch;
        }

        /// <summary>Tiny elongated bipyramid, ~1.2 units tall - scale it down in the particle system.</summary>
        public static Mesh Shard()
        {
            if (s_shard == null)
            {
                s_shard = BuildShard();
            }

            return s_shard;
        }

        // -------------------------------------------------------------------------------------------------------------

        /// <summary>A six-sided prism emerging from the ground: slightly pinched where it meets the ground, fullest just
        /// above it, near-straight faceted sides, then a taper into an off-centre point. Alternate rings are turned half a
        /// facet so the sides break into triangular crystal faces.</summary>
        private static Mesh BuildCrystal(int seed)
        {
            var random = new System.Random(seed);
            const int sides = 6;
            float[] heights = { 0f, 0.1f, 0.58f, 0.78f };
            float[] radii = { 0.85f, 1f, 0.9f, 0.52f };

            var rings = new Vector3[heights.Length][];
            var angles = new float[heights.Length][];
            for (int k = 0; k < heights.Length; k++)
            {
                BuildRing(random, sides, radii[k], heights[k], (k % 2) * 0.5f, k == 0 ? 0f : 0.025f, 0.1f,
                    out rings[k], out angles[k]);
            }

            var builder = new FacetBuilder();
            for (int k = 0; k < rings.Length - 1; k++)
            {
                builder.Stitch(rings[k], angles[k], rings[k + 1], angles[k + 1], FacetBuilder.Outward.Radial);
            }

            // The tip leans a little off the axis, like a real crystal point.
            var apex = new Vector3(Range(random, -0.25f, 0.25f), 1f, Range(random, -0.25f, 0.25f));
            Vector3[] top = rings[rings.Length - 1];
            for (int j = 0; j < sides; j++)
            {
                builder.AddFacet(top[j], top[j + 1], apex, FacetBuilder.Outward.Radial);
            }

            return builder.ToMesh("IceCrystal_" + seed);
        }

        private static Mesh BuildFrostPatch(int seed)
        {
            var random = new System.Random(seed);
            BuildRing(random, 10, 1f, 0.005f, 0f, 0f, 0.1f, out Vector3[] rim, out float[] rimAngles);
            BuildRing(random, 10, 0.6f, 0.045f, 0.5f, 0.005f, 0.1f, out Vector3[] inner, out float[] innerAngles);

            var builder = new FacetBuilder();
            builder.Stitch(rim, rimAngles, inner, innerAngles, FacetBuilder.Outward.Up);
            var centre = new Vector3(0f, 0.07f, 0f);
            for (int j = 0; j < 10; j++)
            {
                builder.AddFacet(inner[j], inner[j + 1], centre, FacetBuilder.Outward.Up);
            }

            return builder.ToMesh("IceFrostPatch");
        }

        private static Mesh BuildShard()
        {
            var top = new Vector3(0f, 0.7f, 0f);
            var bottom = new Vector3(0f, -0.5f, 0f);
            var middle = new Vector3[4];
            for (int i = 0; i < 3; i++)
            {
                float angle = i * Mathf.PI * 2f / 3f;
                middle[i] = new Vector3(Mathf.Cos(angle) * 0.22f, 0f, Mathf.Sin(angle) * 0.22f);
            }

            middle[3] = middle[0];
            var builder = new FacetBuilder();
            for (int i = 0; i < 3; i++)
            {
                builder.AddFacet(middle[i], middle[i + 1], top, FacetBuilder.Outward.FromOrigin);
                builder.AddFacet(middle[i], middle[i + 1], bottom, FacetBuilder.Outward.FromOrigin);
            }

            return builder.ToMesh("IceShard");
        }

        /// <summary>count points round a ring, each jittered in angle, radius and height; the arrays get one extra
        /// closing element (the first point again, its angle + 2 PI) so bands can be stitched without wrapping.</summary>
        private static void BuildRing(System.Random random, int count, float radius, float height, float turnSteps,
            float heightJitter, float radiusJitter, out Vector3[] points, out float[] angles)
        {
            float step = Mathf.PI * 2f / count;
            points = new Vector3[count + 1];
            angles = new float[count + 1];
            for (int j = 0; j < count; j++)
            {
                float angle = (j + turnSteps + Range(random, -0.18f, 0.18f)) * step;
                float r = radius * (1f + Range(random, -radiusJitter, radiusJitter));
                float y = height + Range(random, -heightJitter, heightJitter);
                angles[j] = angle;
                points[j] = new Vector3(Mathf.Cos(angle) * r, y, Mathf.Sin(angle) * r);
            }

            angles[count] = angles[0] + Mathf.PI * 2f;
            points[count] = points[0];
        }

        private static float Range(System.Random random, float min, float max)
        {
            return min + (float)random.NextDouble() * (max - min);
        }

        /// <summary>Collects flat-shaded triangles, each wound so its face points away from the inside.</summary>
        private sealed class FacetBuilder
        {
            /// <summary>Which way "outside" is: away from the vertical axis, straight up, or away from the origin.</summary>
            public enum Outward { Radial, Up, FromOrigin }

            private readonly List<Vector3> _vertices = new List<Vector3>();
            private readonly List<Vector3> _normals = new List<Vector3>();

            /// <summary>Zig-zags triangles between two rings by walking both round in angle order.</summary>
            public void Stitch(Vector3[] lower, float[] lowerAngles, Vector3[] upper, float[] upperAngles, Outward outward)
            {
                int count = lower.Length - 1;
                int i = 0, j = 0;
                while (i < count || j < count)
                {
                    bool advanceLower = j >= count || (i < count && lowerAngles[i + 1] <= upperAngles[j + 1]);
                    if (advanceLower)
                    {
                        AddFacet(lower[i], lower[i + 1], upper[j], outward);
                        i++;
                    }
                    else
                    {
                        AddFacet(lower[i], upper[j + 1], upper[j], outward);
                        j++;
                    }
                }
            }

            public void AddFacet(Vector3 a, Vector3 b, Vector3 c, Outward outward)
            {
                Vector3 normal = Vector3.Cross(b - a, c - a);
                Vector3 centroid = (a + b + c) / 3f;
                Vector3 away = outward == Outward.Up ? Vector3.up
                    : outward == Outward.FromOrigin ? centroid
                    : new Vector3(centroid.x, 0f, centroid.z);

                // Unity shows the side a clockwise triangle's cross product points to.
                if (Vector3.Dot(normal, away) < 0f)
                {
                    Vector3 swap = b;
                    b = c;
                    c = swap;
                    normal = -normal;
                }

                normal.Normalize();
                _vertices.Add(a);
                _vertices.Add(b);
                _vertices.Add(c);
                _normals.Add(normal);
                _normals.Add(normal);
                _normals.Add(normal);
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
                mesh.SetVertices(_vertices);
                mesh.SetNormals(_normals);

                var colors = new Color32[_vertices.Count];
                var triangles = new int[_vertices.Count];
                for (int i = 0; i < colors.Length; i++)
                {
                    colors[i] = new Color32(255, 255, 255, 255);
                    triangles[i] = i;
                }

                mesh.colors32 = colors;
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
