using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AlienDefense.EditorTools
{
    public static partial class Level02Builder
    {
        /// <summary>Tiny flat-shaded mesh builder for the Level_02 kit: every face gets its own vertices, one colour
        /// (vertex colour) and its own normal, so the result reads as clean low-poly with a single shared material.
        /// Winding is fixed from an "outward" hint per face, so callers never have to think about clockwise order.</summary>
        private sealed class KitMesh
        {
            private readonly List<Vector3> _vertices = new List<Vector3>();
            private readonly List<Vector3> _normals = new List<Vector3>();
            private readonly List<Color> _colors = new List<Color>();
            private readonly List<int> _triangles = new List<int>();
            private readonly Stack<Matrix4x4> _stack = new Stack<Matrix4x4>();
            private Matrix4x4 _matrix = Matrix4x4.identity;

            public int TriangleCount => _triangles.Count / 3;

            public void Push(Vector3 position, Quaternion rotation, Vector3 scale)
            {
                _stack.Push(_matrix);
                _matrix = _matrix * Matrix4x4.TRS(position, rotation, scale);
            }

            public void Push(Vector3 position, Quaternion rotation) => Push(position, rotation, Vector3.one);

            public void Pop() => _matrix = _stack.Pop();

            /// <summary>One triangle; <paramref name="outward"/> (local space) decides which side is the front.</summary>
            public void Tri(Vector3 a, Vector3 b, Vector3 c, Color color, Vector3 outward)
            {
                Vector3 wa = _matrix.MultiplyPoint3x4(a);
                Vector3 wb = _matrix.MultiplyPoint3x4(b);
                Vector3 wc = _matrix.MultiplyPoint3x4(c);
                Vector3 wo = _matrix.MultiplyVector(outward);
                Vector3 n = Vector3.Cross(wb - wa, wc - wa);
                if (n.sqrMagnitude < 1e-10f)
                {
                    return;
                }

                if (Vector3.Dot(n, wo) < 0f)
                {
                    (wb, wc) = (wc, wb);
                    n = -n;
                }

                n.Normalize();
                int i = _vertices.Count;
                _vertices.Add(wa);
                _vertices.Add(wb);
                _vertices.Add(wc);
                for (int k = 0; k < 3; k++)
                {
                    _normals.Add(n);
                    _colors.Add(color);
                }

                _triangles.Add(i);
                _triangles.Add(i + 1);
                _triangles.Add(i + 2);
            }

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color, Vector3 outward)
            {
                Tri(a, b, c, color, outward);
                Tri(a, c, d, color, outward);
            }

            /// <summary>Axis-aligned box (in the current matrix) - optional separate top colour.</summary>
            public void Box(Vector3 center, Vector3 size, Color color, Color? top = null, bool bottom = false)
            {
                Vector3 h = size * 0.5f;
                Vector3 p(float x, float y, float z) => center + new Vector3(x * h.x, y * h.y, z * h.z);
                Quad(p(-1, -1, 1), p(1, -1, 1), p(1, 1, 1), p(-1, 1, 1), color, Vector3.forward);
                Quad(p(-1, -1, -1), p(1, -1, -1), p(1, 1, -1), p(-1, 1, -1), color, Vector3.back);
                Quad(p(1, -1, -1), p(1, -1, 1), p(1, 1, 1), p(1, 1, -1), color, Vector3.right);
                Quad(p(-1, -1, -1), p(-1, -1, 1), p(-1, 1, 1), p(-1, 1, -1), color, Vector3.left);
                Quad(p(-1, 1, -1), p(1, 1, -1), p(1, 1, 1), p(-1, 1, 1), top ?? color, Vector3.up);
                if (bottom)
                {
                    Quad(p(-1, -1, -1), p(1, -1, -1), p(1, -1, 1), p(-1, -1, 1), color, Vector3.down);
                }
            }

            /// <summary>Box rotated around its own centre.</summary>
            public void Box(Vector3 center, Vector3 size, Quaternion rotation, Color color, Color? top = null)
            {
                Push(center, rotation);
                Box(Vector3.zero, size, color, top);
                Pop();
            }

            /// <summary>A beam of the given thickness from <paramref name="from"/> to <paramref name="to"/>.</summary>
            public void Beam(Vector3 from, Vector3 to, float thickness, Color color)
            {
                Vector3 d = to - from;
                if (d.sqrMagnitude < 1e-6f)
                {
                    return;
                }

                Quaternion rotation = Quaternion.LookRotation(d.normalized, Mathf.Abs(d.normalized.y) > 0.95f ? Vector3.forward : Vector3.up);
                Box((from + to) * 0.5f, new Vector3(thickness, thickness, d.magnitude), rotation, color);
            }

            /// <summary>Prism/cylinder around +Y. <paramref name="sideColor"/> may vary per segment.</summary>
            public void Cylinder(Vector3 baseCenter, float radiusBottom, float radiusTop, float height, int segments,
                System.Func<int, Color> sideColor, Color? topColor, Color? bottomColor = null, float angleOffset = 0f)
            {
                for (int s = 0; s < segments; s++)
                {
                    float a0 = (s / (float)segments) * Mathf.PI * 2f + angleOffset;
                    float a1 = ((s + 1) / (float)segments) * Mathf.PI * 2f + angleOffset;
                    Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                    Vector3 d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                    Vector3 b0 = baseCenter + d0 * radiusBottom;
                    Vector3 b1 = baseCenter + d1 * radiusBottom;
                    Vector3 t0 = baseCenter + d0 * radiusTop + Vector3.up * height;
                    Vector3 t1 = baseCenter + d1 * radiusTop + Vector3.up * height;
                    Vector3 outward = (d0 + d1).normalized;
                    Color c = sideColor(s);
                    if (radiusTop <= 0.0001f)
                    {
                        Tri(b0, b1, t0, c, outward + Vector3.up * 0.2f);
                    }
                    else
                    {
                        Quad(b0, b1, t1, t0, c, outward);
                    }

                    if (topColor.HasValue && radiusTop > 0.0001f)
                    {
                        Tri(baseCenter + Vector3.up * height, t0, t1, topColor.Value, Vector3.up);
                    }

                    if (bottomColor.HasValue)
                    {
                        Tri(baseCenter, b0, b1, bottomColor.Value, Vector3.down);
                    }
                }
            }

            public void Cylinder(Vector3 baseCenter, float radius, float height, int segments, Color side, Color? top, Color? bottom = null) =>
                Cylinder(baseCenter, radius, radius, height, segments, _ => side, top, bottom);

            /// <summary>Flat polygon (fan) in the XY plane of the current matrix, facing <paramref name="outward"/>.</summary>
            public void Polygon(Vector3[] points, Color color, Vector3 outward)
            {
                for (int i = 1; i < points.Length - 1; i++)
                {
                    Tri(points[0], points[i], points[i + 1], color, outward);
                }
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.indexFormat = _vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
                mesh.SetVertices(_vertices);
                mesh.SetNormals(_normals);
                mesh.SetColors(_colors);
                mesh.SetTriangles(_triangles, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
