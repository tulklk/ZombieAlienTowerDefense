using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AlienDefense.EditorTools
{
    /// <summary>Quadric error metric (Garland-Heckbert) mesh decimation, used by <see cref="MobileMeshOptimizer"/> to
    /// re-mesh the few AI-generated props that carry half a million triangles each.
    ///
    /// Grid-based vertex clustering was tried first and was rejected: it melted the flat machined faces of the build
    /// pads and shattered the PlayerBase dome, because it snaps vertices to a grid regardless of how curved or flat
    /// the surface is. QEM instead collapses the edges that cost the least error, so flat panels lose almost all of
    /// their triangles while silhouettes and curved surfaces keep theirs - which is what a fixed top-down camera
    /// actually sees.
    ///
    /// UVs and normals are kept per triangle corner and re-interpolated at the collapsed position, so texture detail
    /// does not slide across seams. This is editor-only tooling: it runs once to bake an asset, never at runtime.</summary>
    internal static class MeshDecimator
    {
        private const double DoubleEpsilon = 1.0E-3;

        /// <summary>Vector3.normalized returns zero for anything shorter than 1e-5, and the cross product of two edges
        /// of a 2 mm triangle is about 4e-6 - which silently gave 62% of the build pad's faces a zero normal and made
        /// every collapse look like a fold. These meshes are normalised by hand instead.</summary>
        private static Vector3 Normalize(Vector3 value)
        {
            float length = Mathf.Sqrt(value.x * value.x + value.y * value.y + value.z * value.z);
            return length > 1e-20f ? value / length : Vector3.zero;
        }

        /// <summary>Why collapses were refused on the last run - printed by the caller, since a decimator that quietly
        /// stops short of its budget looks the same as one that finished.</summary>
        public static string LastReport { get; private set; } = string.Empty;

        /// <summary>Simplifies until the triangle budget is met (or no further collapse is safe).</summary>
        public static Mesh Decimate(Mesh source, int targetTriangles)
        {
            var state = new State(source);
            state.Run(targetTriangles);
            LastReport = state.Report;
            return state.ToMesh();
        }

        private struct SymmetricMatrix
        {
            public double M0, M1, M2, M3, M4, M5, M6, M7, M8, M9;

            /// <summary>Quadric of the plane (a, b, c, d).</summary>
            public SymmetricMatrix(double a, double b, double c, double d)
            {
                M0 = a * a; M1 = a * b; M2 = a * c; M3 = a * d;
                M4 = b * b; M5 = b * c; M6 = b * d;
                M7 = c * c; M8 = c * d;
                M9 = d * d;
            }

            public double this[int index] => index switch
            {
                0 => M0, 1 => M1, 2 => M2, 3 => M3, 4 => M4,
                5 => M5, 6 => M6, 7 => M7, 8 => M8, _ => M9,
            };

            public static SymmetricMatrix operator +(SymmetricMatrix a, SymmetricMatrix b)
            {
                return new SymmetricMatrix
                {
                    M0 = a.M0 + b.M0, M1 = a.M1 + b.M1, M2 = a.M2 + b.M2, M3 = a.M3 + b.M3, M4 = a.M4 + b.M4,
                    M5 = a.M5 + b.M5, M6 = a.M6 + b.M6, M7 = a.M7 + b.M7, M8 = a.M8 + b.M8, M9 = a.M9 + b.M9,
                };
            }

            public double Determinant(int a11, int a12, int a13, int a21, int a22, int a23, int a31, int a32, int a33)
            {
                return this[a11] * this[a22] * this[a33] + this[a13] * this[a21] * this[a32] + this[a12] * this[a23] * this[a31]
                    - this[a13] * this[a22] * this[a31] - this[a11] * this[a23] * this[a32] - this[a12] * this[a21] * this[a33];
            }
        }

        private struct Vert
        {
            public Vector3 Position;
            public int RefStart;
            public int RefCount;
            public SymmetricMatrix Q;
            public bool Border;
        }

        private struct Tri
        {
            public int V0, V1, V2;
            public double Err0, Err1, Err2, ErrMin;
            public bool Deleted;
            public bool Dirty;
            public Vector3 Normal;
            public Vector2 Uv0, Uv1, Uv2;
            public Vector3 N0, N1, N2;
            public int SubMesh;

            public int GetVertex(int index) => index == 0 ? V0 : index == 1 ? V1 : V2;

            public void SetVertex(int index, int value)
            {
                if (index == 0) { V0 = value; }
                else if (index == 1) { V1 = value; }
                else { V2 = value; }
            }

            public double GetError(int index) => index == 0 ? Err0 : index == 1 ? Err1 : Err2;
        }

        private struct RefTriangle
        {
            public int TriangleId;
            public int TriangleVertex;
        }

        private sealed class State
        {
            private readonly List<Vert> _vertices = new List<Vert>();
            private readonly List<Tri> _triangles = new List<Tri>();
            private readonly List<RefTriangle> _refs = new List<RefTriangle>();
            private readonly int _subMeshCount;
            private readonly bool _hasUv;
            private readonly bool _hasNormals;
            private int _collapses;
            private int _rejectedBorderMismatch;
            private int _rejectedFlipped;
            private int _rejectedThreshold;
            private int _rejectedParallel;
            private int _rejectedNormal;

            public string Report { get; private set; } = string.Empty;

            public State(Mesh source)
            {
                Vector3[] positions = source.vertices;
                Vector3[] normals = source.normals;
                Vector2[] uv = source.uv;
                _hasNormals = normals != null && normals.Length == positions.Length;
                _hasUv = uv != null && uv.Length == positions.Length;
                _subMeshCount = source.subMeshCount;

                // Positions are welded first. Exported meshes split a vertex wherever the UV or the normal breaks, and
                // on that split topology every seam edge looks like an open boundary, which would pin most of the mesh
                // and prevent any collapse. Attributes are carried on the triangle corners instead, so welding here
                // costs nothing.
                var welded = new Dictionary<(int, int, int), int>(positions.Length);
                var weldMap = new int[positions.Length];
                for (int i = 0; i < positions.Length; i++)
                {
                    Vector3 position = positions[i];
                    var key = (Mathf.RoundToInt(position.x * 100000f), Mathf.RoundToInt(position.y * 100000f),
                        Mathf.RoundToInt(position.z * 100000f));
                    if (!welded.TryGetValue(key, out int index))
                    {
                        index = _vertices.Count;
                        welded.Add(key, index);
                        _vertices.Add(new Vert { Position = position });
                    }

                    weldMap[i] = index;
                }

                for (int s = 0; s < _subMeshCount; s++)
                {
                    int[] indices = source.GetTriangles(s);
                    for (int i = 0; i < indices.Length; i += 3)
                    {
                        int a = indices[i];
                        int b = indices[i + 1];
                        int c = indices[i + 2];
                        if (weldMap[a] == weldMap[b] || weldMap[b] == weldMap[c] || weldMap[a] == weldMap[c])
                        {
                            continue;
                        }

                        _triangles.Add(new Tri
                        {
                            V0 = weldMap[a], V1 = weldMap[b], V2 = weldMap[c],
                            SubMesh = s,
                            Uv0 = _hasUv ? uv[a] : Vector2.zero,
                            Uv1 = _hasUv ? uv[b] : Vector2.zero,
                            Uv2 = _hasUv ? uv[c] : Vector2.zero,
                            N0 = _hasNormals ? normals[a] : Vector3.up,
                            N1 = _hasNormals ? normals[b] : Vector3.up,
                            N2 = _hasNormals ? normals[c] : Vector3.up,
                        });
                    }
                }
            }

            public void Run(int targetTriangles)
            {
                int deletedTriangles = 0;
                int triangleCount = _triangles.Count;
                var deleted0 = new List<bool>();
                var deleted1 = new List<bool>();

                for (int iteration = 0; iteration < 100; iteration++)
                {
                    if (triangleCount - deletedTriangles <= targetTriangles)
                    {
                        break;
                    }

                    if (iteration % 5 == 0)
                    {
                        UpdateMesh(iteration);
                        triangleCount = _triangles.Count;
                        deletedTriangles = 0;
                    }

                    for (int i = 0; i < _triangles.Count; i++)
                    {
                        Tri clean = _triangles[i];
                        clean.Dirty = false;
                        _triangles[i] = clean;
                    }

                    // Error threshold grows each pass, so the cheapest collapses happen first.
                    double threshold = 0.000000001 * System.Math.Pow(iteration + 3, 7.0);

                    for (int i = 0; i < _triangles.Count; i++)
                    {
                        Tri triangle = _triangles[i];
                        if (triangle.ErrMin > threshold || triangle.Deleted || triangle.Dirty)
                        {
                            continue;
                        }

                        for (int j = 0; j < 3; j++)
                        {
                            if (triangle.GetError(j) > threshold)
                            {
                                _rejectedThreshold++;
                                continue;
                            }

                            int i0 = triangle.GetVertex(j);
                            int i1 = triangle.GetVertex((j + 1) % 3);
                            Vert v0 = _vertices[i0];
                            Vert v1 = _vertices[i1];
                            if (v0.Border != v1.Border)
                            {
                                _rejectedBorderMismatch++;
                                continue;
                            }

                            CalculateError(i0, i1, out Vector3 target);

                            Resize(deleted0, v0.RefCount);
                            Resize(deleted1, v1.RefCount);
                            if (Flipped(target, i1, v0, deleted0) || Flipped(target, i0, v1, deleted1))
                            {
                                _rejectedFlipped++;
                                continue;
                            }

                            InterpolateAttributes(v0, target, deleted0);
                            InterpolateAttributes(v1, target, deleted1);

                            v0.Position = target;
                            v0.Q = v1.Q + v0.Q;
                            _vertices[i0] = v0;

                            int refStart = _refs.Count;
                            UpdateTriangles(i0, i0, deleted0, ref deletedTriangles);
                            UpdateTriangles(i0, i1, deleted1, ref deletedTriangles);
                            int refCount = _refs.Count - refStart;

                            v0 = _vertices[i0];
                            if (refCount <= v0.RefCount)
                            {
                                for (int k = 0; k < refCount; k++)
                                {
                                    _refs[v0.RefStart + k] = _refs[refStart + k];
                                }

                                _refs.RemoveRange(refStart, refCount);
                            }
                            else
                            {
                                v0.RefStart = refStart;
                            }

                            v0.RefCount = refCount;
                            _vertices[i0] = v0;
                            _collapses++;
                            break;
                        }

                        if (triangleCount - deletedTriangles <= targetTriangles)
                        {
                            break;
                        }
                    }
                }

                CompactMesh();

                int borders = 0;
                foreach (Vert vertex in _vertices)
                {
                    if (vertex.Border)
                    {
                        borders++;
                    }
                }

                Report = $"collapses={_collapses} borderVerts={borders}/{_vertices.Count} " +
                    $"rejected(border={_rejectedBorderMismatch}, flipped={_rejectedFlipped} " +
                    $"[parallel={_rejectedParallel}, normal={_rejectedNormal}], threshold={_rejectedThreshold})";
            }

            private static void Resize(List<bool> list, int count)
            {
                list.Clear();
                for (int i = 0; i < count; i++)
                {
                    list.Add(false);
                }
            }

            /// <summary>True when moving the vertex to <paramref name="target"/> would fold a triangle inside out or
            /// squash it to a sliver - those collapses are skipped so the surface never self-intersects.</summary>
            private bool Flipped(Vector3 target, int otherVertex, Vert vertex, List<bool> deleted)
            {
                for (int k = 0; k < vertex.RefCount; k++)
                {
                    RefTriangle reference = _refs[vertex.RefStart + k];
                    Tri triangle = _triangles[reference.TriangleId];
                    if (triangle.Deleted)
                    {
                        continue;
                    }

                    int s = reference.TriangleVertex;
                    int id1 = triangle.GetVertex((s + 1) % 3);
                    int id2 = triangle.GetVertex((s + 2) % 3);
                    if (id1 == otherVertex || id2 == otherVertex)
                    {
                        deleted[k] = true;
                        continue;
                    }

                    Vector3 d1 = Normalize(_vertices[id1].Position - target);
                    Vector3 d2 = Normalize(_vertices[id2].Position - target);
                    if (Mathf.Abs(Vector3.Dot(d1, d2)) > 0.999f)
                    {
                        _rejectedParallel++;
                        return true;
                    }

                    Vector3 normal = Normalize(Vector3.Cross(d1, d2));
                    deleted[k] = false;
                    if (Vector3.Dot(normal, triangle.Normal) < 0.2f)
                    {
                        _rejectedNormal++;
                        return true;
                    }
                }

                return false;
            }

            /// <summary>Re-projects the UV and normal of every surviving corner onto the collapsed position, so a
            /// texture keeps sitting where it sat on the original surface.</summary>
            private void InterpolateAttributes(Vert vertex, Vector3 target, List<bool> deleted)
            {
                for (int k = 0; k < vertex.RefCount; k++)
                {
                    RefTriangle reference = _refs[vertex.RefStart + k];
                    Tri triangle = _triangles[reference.TriangleId];
                    if (triangle.Deleted || deleted[k])
                    {
                        continue;
                    }

                    Vector3 p0 = _vertices[triangle.V0].Position;
                    Vector3 p1 = _vertices[triangle.V1].Position;
                    Vector3 p2 = _vertices[triangle.V2].Position;
                    Barycentric(target, p0, p1, p2, out float u, out float v, out float w);

                    Vector2 uv = triangle.Uv0 * u + triangle.Uv1 * v + triangle.Uv2 * w;
                    Vector3 normal = Normalize(triangle.N0 * u + triangle.N1 * v + triangle.N2 * w);
                    switch (reference.TriangleVertex)
                    {
                        case 0: triangle.Uv0 = uv; triangle.N0 = normal; break;
                        case 1: triangle.Uv1 = uv; triangle.N1 = normal; break;
                        default: triangle.Uv2 = uv; triangle.N2 = normal; break;
                    }

                    _triangles[reference.TriangleId] = triangle;
                }
            }

            private static void Barycentric(Vector3 point, Vector3 a, Vector3 b, Vector3 c, out float u, out float v, out float w)
            {
                Vector3 v0 = b - a;
                Vector3 v1 = c - a;
                Vector3 v2 = point - a;
                float d00 = Vector3.Dot(v0, v0);
                float d01 = Vector3.Dot(v0, v1);
                float d11 = Vector3.Dot(v1, v1);
                float d20 = Vector3.Dot(v2, v0);
                float d21 = Vector3.Dot(v2, v1);
                float denominator = d00 * d11 - d01 * d01;
                if (Mathf.Abs(denominator) < 1e-12f)
                {
                    u = 1f; v = 0f; w = 0f;
                    return;
                }

                v = (d11 * d20 - d01 * d21) / denominator;
                w = (d00 * d21 - d01 * d20) / denominator;
                u = 1f - v - w;
            }

            private void UpdateTriangles(int i0, int vertexIndex, List<bool> deleted, ref int deletedTriangles)
            {
                Vert vertex = _vertices[vertexIndex];
                for (int k = 0; k < vertex.RefCount; k++)
                {
                    RefTriangle reference = _refs[vertex.RefStart + k];
                    Tri triangle = _triangles[reference.TriangleId];
                    if (triangle.Deleted)
                    {
                        continue;
                    }

                    if (deleted[k])
                    {
                        triangle.Deleted = true;
                        _triangles[reference.TriangleId] = triangle;
                        deletedTriangles++;
                        continue;
                    }

                    triangle.SetVertex(reference.TriangleVertex, i0);
                    triangle.Dirty = true;
                    triangle.Err0 = CalculateError(triangle.V0, triangle.V1, out _);
                    triangle.Err1 = CalculateError(triangle.V1, triangle.V2, out _);
                    triangle.Err2 = CalculateError(triangle.V2, triangle.V0, out _);
                    triangle.ErrMin = System.Math.Min(triangle.Err0, System.Math.Min(triangle.Err1, triangle.Err2));
                    _triangles[reference.TriangleId] = triangle;
                    _refs.Add(reference);
                }
            }

            private void UpdateMesh(int iteration)
            {
                if (iteration > 0)
                {
                    int destination = 0;
                    for (int i = 0; i < _triangles.Count; i++)
                    {
                        if (!_triangles[i].Deleted)
                        {
                            _triangles[destination++] = _triangles[i];
                        }
                    }

                    _triangles.RemoveRange(destination, _triangles.Count - destination);
                }

                if (iteration == 0)
                {
                    InitializeQuadrics();
                }

                BuildReferences();

                if (iteration == 0)
                {
                    IdentifyBorders();
                }
            }

            private void InitializeQuadrics()
            {
                for (int i = 0; i < _vertices.Count; i++)
                {
                    Vert vertex = _vertices[i];
                    vertex.Q = default;
                    _vertices[i] = vertex;
                }

                for (int i = 0; i < _triangles.Count; i++)
                {
                    Tri triangle = _triangles[i];
                    Vector3 p0 = _vertices[triangle.V0].Position;
                    Vector3 p1 = _vertices[triangle.V1].Position;
                    Vector3 p2 = _vertices[triangle.V2].Position;
                    Vector3 normal = Normalize(Vector3.Cross(p1 - p0, p2 - p0));
                    triangle.Normal = normal;

                    var quadric = new SymmetricMatrix(normal.x, normal.y, normal.z, -Vector3.Dot(normal, p0));
                    for (int j = 0; j < 3; j++)
                    {
                        int index = triangle.GetVertex(j);
                        Vert vertex = _vertices[index];
                        vertex.Q += quadric;
                        _vertices[index] = vertex;
                    }

                    _triangles[i] = triangle;
                }

                for (int i = 0; i < _triangles.Count; i++)
                {
                    Tri triangle = _triangles[i];
                    triangle.Err0 = CalculateError(triangle.V0, triangle.V1, out _);
                    triangle.Err1 = CalculateError(triangle.V1, triangle.V2, out _);
                    triangle.Err2 = CalculateError(triangle.V2, triangle.V0, out _);
                    triangle.ErrMin = System.Math.Min(triangle.Err0, System.Math.Min(triangle.Err1, triangle.Err2));
                    _triangles[i] = triangle;
                }
            }

            private void BuildReferences()
            {
                for (int i = 0; i < _vertices.Count; i++)
                {
                    Vert vertex = _vertices[i];
                    vertex.RefStart = 0;
                    vertex.RefCount = 0;
                    _vertices[i] = vertex;
                }

                for (int i = 0; i < _triangles.Count; i++)
                {
                    Tri triangle = _triangles[i];
                    for (int j = 0; j < 3; j++)
                    {
                        int index = triangle.GetVertex(j);
                        Vert vertex = _vertices[index];
                        vertex.RefCount++;
                        _vertices[index] = vertex;
                    }
                }

                int start = 0;
                for (int i = 0; i < _vertices.Count; i++)
                {
                    Vert vertex = _vertices[i];
                    vertex.RefStart = start;
                    start += vertex.RefCount;
                    vertex.RefCount = 0;
                    _vertices[i] = vertex;
                }

                _refs.Clear();
                for (int i = 0; i < start; i++)
                {
                    _refs.Add(default);
                }

                for (int i = 0; i < _triangles.Count; i++)
                {
                    Tri triangle = _triangles[i];
                    for (int j = 0; j < 3; j++)
                    {
                        int index = triangle.GetVertex(j);
                        Vert vertex = _vertices[index];
                        _refs[vertex.RefStart + vertex.RefCount] = new RefTriangle { TriangleId = i, TriangleVertex = j };
                        vertex.RefCount++;
                        _vertices[index] = vertex;
                    }
                }
            }

            /// <summary>An edge used by exactly one triangle is an open boundary; its vertices are pinned so holes do
            /// not grow.</summary>
            private void IdentifyBorders()
            {
                var neighbourIds = new List<int>();
                var neighbourCounts = new List<int>();

                for (int i = 0; i < _vertices.Count; i++)
                {
                    Vert vertex = _vertices[i];
                    vertex.Border = false;
                    _vertices[i] = vertex;
                }

                for (int i = 0; i < _vertices.Count; i++)
                {
                    Vert vertex = _vertices[i];
                    neighbourIds.Clear();
                    neighbourCounts.Clear();

                    for (int j = 0; j < vertex.RefCount; j++)
                    {
                        Tri triangle = _triangles[_refs[vertex.RefStart + j].TriangleId];
                        for (int k = 0; k < 3; k++)
                        {
                            int id = triangle.GetVertex(k);
                            if (id == i)
                            {
                                continue;
                            }

                            int slot = neighbourIds.IndexOf(id);
                            if (slot < 0)
                            {
                                neighbourIds.Add(id);
                                neighbourCounts.Add(1);
                            }
                            else
                            {
                                neighbourCounts[slot]++;
                            }
                        }
                    }

                    for (int j = 0; j < neighbourIds.Count; j++)
                    {
                        if (neighbourCounts[j] != 1)
                        {
                            continue;
                        }

                        vertex.Border = true;
                        Vert neighbour = _vertices[neighbourIds[j]];
                        neighbour.Border = true;
                        _vertices[neighbourIds[j]] = neighbour;
                    }

                    _vertices[i] = vertex;
                }
            }

            private double CalculateError(int idV1, int idV2, out Vector3 result)
            {
                SymmetricMatrix q = _vertices[idV1].Q + _vertices[idV2].Q;
                bool border = _vertices[idV1].Border && _vertices[idV2].Border;
                double determinant = q.Determinant(0, 1, 2, 1, 4, 5, 2, 5, 7);

                Vector3 p1 = _vertices[idV1].Position;
                Vector3 p2 = _vertices[idV2].Position;

                if (System.Math.Abs(determinant) > DoubleEpsilon && !border)
                {
                    // The quadric has a unique minimum: put the merged vertex exactly there.
                    var optimal = new Vector3(
                        (float)(-1 / determinant * q.Determinant(1, 2, 3, 4, 5, 6, 5, 7, 8)),
                        (float)(1 / determinant * q.Determinant(0, 2, 3, 1, 5, 6, 2, 7, 8)),
                        (float)(-1 / determinant * q.Determinant(0, 1, 3, 1, 4, 6, 2, 5, 8)));

                    // On a near-flat neighbourhood the determinant is almost zero and that minimum lands far away from
                    // the edge; every triangle around it then folds and the collapse is refused. Keeping it near the
                    // edge is what lets these dense scanned meshes actually decimate.
                    Vector3 midpoint = (p1 + p2) * 0.5f;
                    float reach = (p2 - p1).magnitude;
                    if ((optimal - midpoint).sqrMagnitude <= reach * reach)
                    {
                        result = optimal;
                        return VertexError(q, optimal.x, optimal.y, optimal.z);
                    }
                }

                Vector3 p3 = (p1 + p2) * 0.5f;
                double error1 = VertexError(q, p1.x, p1.y, p1.z);
                double error2 = VertexError(q, p2.x, p2.y, p2.z);
                double error3 = VertexError(q, p3.x, p3.y, p3.z);
                double error = System.Math.Min(error1, System.Math.Min(error2, error3));
                result = error == error1 ? p1 : error == error2 ? p2 : p3;
                return error;
            }

            private static double VertexError(SymmetricMatrix q, double x, double y, double z)
            {
                return q.M0 * x * x + 2 * q.M1 * x * y + 2 * q.M2 * x * z + 2 * q.M3 * x
                    + q.M4 * y * y + 2 * q.M5 * y * z + 2 * q.M6 * y
                    + q.M7 * z * z + 2 * q.M8 * z + q.M9;
            }

            private void CompactMesh()
            {
                int destination = 0;
                for (int i = 0; i < _triangles.Count; i++)
                {
                    if (!_triangles[i].Deleted)
                    {
                        _triangles[destination++] = _triangles[i];
                    }
                }

                _triangles.RemoveRange(destination, _triangles.Count - destination);
            }

            /// <summary>Welds the surviving corners back into a vertex buffer: corners that share a position, UV and
            /// normal become one vertex, corners across a seam stay separate.</summary>
            public Mesh ToMesh()
            {
                var lookup = new Dictionary<(int, int, int, int), int>();
                var positions = new List<Vector3>();
                var normals = new List<Vector3>();
                var uvs = new List<Vector2>();
                var indicesPerSubMesh = new List<int>[_subMeshCount];
                for (int i = 0; i < _subMeshCount; i++)
                {
                    indicesPerSubMesh[i] = new List<int>();
                }

                foreach (Tri triangle in _triangles)
                {
                    List<int> indices = indicesPerSubMesh[Mathf.Clamp(triangle.SubMesh, 0, _subMeshCount - 1)];
                    indices.Add(AddCorner(lookup, positions, normals, uvs, _vertices[triangle.V0].Position, triangle.N0, triangle.Uv0));
                    indices.Add(AddCorner(lookup, positions, normals, uvs, _vertices[triangle.V1].Position, triangle.N1, triangle.Uv1));
                    indices.Add(AddCorner(lookup, positions, normals, uvs, _vertices[triangle.V2].Position, triangle.N2, triangle.Uv2));
                }

                var mesh = new Mesh
                {
                    indexFormat = positions.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16,
                };
                mesh.SetVertices(positions);
                if (_hasNormals)
                {
                    mesh.SetNormals(normals);
                }

                if (_hasUv)
                {
                    mesh.SetUVs(0, uvs);
                }

                mesh.subMeshCount = _subMeshCount;
                for (int i = 0; i < _subMeshCount; i++)
                {
                    mesh.SetTriangles(indicesPerSubMesh[i], i, false);
                }

                mesh.RecalculateBounds();
                if (!_hasNormals)
                {
                    mesh.RecalculateNormals();
                }

                mesh.RecalculateTangents();
                mesh.Optimize();
                return mesh;
            }

            private static int AddCorner(Dictionary<(int, int, int, int), int> lookup, List<Vector3> positions,
                List<Vector3> normals, List<Vector2> uvs, Vector3 position, Vector3 normal, Vector2 uv)
            {
                // Quantised so float noise from the interpolation does not split otherwise identical corners.
                var key = (
                    Mathf.RoundToInt(position.x * 4096f) * 73856093 ^ Mathf.RoundToInt(position.y * 4096f) * 19349663 ^ Mathf.RoundToInt(position.z * 4096f) * 83492791,
                    Mathf.RoundToInt(uv.x * 4096f),
                    Mathf.RoundToInt(uv.y * 4096f),
                    Mathf.RoundToInt(normal.x * 64f) * 73856093 ^ Mathf.RoundToInt(normal.y * 64f) * 19349663 ^ Mathf.RoundToInt(normal.z * 64f) * 83492791);

                if (lookup.TryGetValue(key, out int existing))
                {
                    return existing;
                }

                int index = positions.Count;
                positions.Add(position);
                normals.Add(normal);
                uvs.Add(uv);
                lookup.Add(key, index);
                return index;
            }
        }
    }
}
