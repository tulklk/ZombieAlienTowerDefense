using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.Minimap
{
    /// <summary>Thick rounded polyline UI graphic for the minimap's enemy-path line. Generates a plain colored
    /// triangle mesh (no texture) directly in local RectTransform space - one quad per segment plus a full
    /// triangle-fan circle at every point (endpoints and interior joints alike), which rounds both the path's
    /// end caps and its corner joints with the same simple draw call. Mesh is rebuilt only when SetPoints() or
    /// SetThickness() is called (level paths are fixed after load), never every frame.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MinimapPathGraphic : MaskableGraphic
    {
        [SerializeField, Min(0.5f)]
        private float _thickness = 10f;

        [SerializeField, Min(3)]
        [Tooltip("Triangle-fan segments per rounded joint/cap circle. Higher = smoother, more verts. Path point " +
            "counts here are tiny (a couple dozen at most) so this stays cheap regardless.")]
        private int _roundSegments = 8;

        private readonly List<Vector2> _points = new List<Vector2>();

        /// <summary>Replaces the polyline's points (already in this graphic's local RectTransform space, e.g.
        /// from MinimapController.WorldToMinimap) and rebuilds the mesh once.</summary>
        public void SetPoints(IReadOnlyList<Vector2> points)
        {
            _points.Clear();
            if (points != null)
            {
                _points.AddRange(points);
            }

            SetVerticesDirty();
        }

        public void SetThickness(float thickness)
        {
            float clamped = Mathf.Max(0.5f, thickness);
            if (Mathf.Approximately(clamped, _thickness))
            {
                return;
            }

            _thickness = clamped;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_points.Count < 2)
            {
                return;
            }

            float halfThickness = _thickness * 0.5f;
            Color32 color32 = color;

            for (int i = 0; i < _points.Count - 1; i++)
            {
                Vector2 a = _points[i];
                Vector2 b = _points[i + 1];
                Vector2 segment = b - a;
                if (segment.sqrMagnitude < 0.0001f)
                {
                    continue;
                }

                Vector2 dir = segment.normalized;
                Vector2 normal = new Vector2(-dir.y, dir.x) * halfThickness;

                int startIndex = vh.currentVertCount;
                vh.AddVert(a - normal, color32, Vector2.zero);
                vh.AddVert(a + normal, color32, Vector2.zero);
                vh.AddVert(b + normal, color32, Vector2.zero);
                vh.AddVert(b - normal, color32, Vector2.zero);
                vh.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
                vh.AddTriangle(startIndex, startIndex + 2, startIndex + 3);
            }

            // A full circle at every point (not just endpoints) covers both rounded caps and rounded joints with
            // one code path - a joint's corner gap is always a subset of its full circle, so this never leaves a
            // visible seam regardless of the turn angle there.
            for (int i = 0; i < _points.Count; i++)
            {
                AddCircle(vh, _points[i], halfThickness, color32);
            }
        }

        private void AddCircle(VertexHelper vh, Vector2 center, float radius, Color32 color32)
        {
            int startIndex = vh.currentVertCount;
            vh.AddVert(center, color32, Vector2.zero);
            for (int s = 0; s <= _roundSegments; s++)
            {
                float angle = (s / (float)_roundSegments) * Mathf.PI * 2f;
                Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                vh.AddVert(center + offset, color32, Vector2.zero);
            }

            for (int s = 1; s <= _roundSegments; s++)
            {
                vh.AddTriangle(startIndex, startIndex + s, startIndex + s + 1);
            }
        }
    }
}
