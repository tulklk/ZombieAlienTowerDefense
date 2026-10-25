using System.Collections.Generic;
using UnityEngine;

namespace AlienDefense.EditorTools
{
    /// <summary>Lays absorbable props out as tidy themed plots instead of a random scatter (user, 2026-10-11: "the
    /// objects lie everywhere - make it look like a real farm / coast"). Free ground is packed greedily with
    /// axis-aligned rectangles (a crop field, a hay yard, a row of sun loungers...) separated by clear walkways; each
    /// plot is then filled from a theme as rows and grids in plot-local coordinates. Rectangles only ever run along
    /// the world axes, so neighbouring plots line up like real fields. Deterministic: same input, same layout.</summary>
    public sealed class PlotPlanner
    {
        public delegate bool FreeTest(Vector2 point);

        public struct Plot
        {
            public Vector2 Center;
            public Vector2 Size;   // x along the plot's long axis
            public float Yaw;      // 0 = long axis on world X, 90 = on world Z
            public int Tier;       // which size pass produced it (0 = the biggest)
            public int Index;
        }

        /// <summary>One prop in plot-local space: x along the plot's long axis, y across it.</summary>
        public struct Item
        {
            public string Key;
            public Vector2 Local;
            public float Yaw;
            public float Scale;

            public Item(string key, float x, float y, float yaw = 0f, float scale = 1f)
            {
                Key = key;
                Local = new Vector2(x, y);
                Yaw = yaw;
                Scale = scale;
            }
        }

        private readonly List<Rect> _taken = new List<Rect>();
        private readonly List<Plot> _plots = new List<Plot>();
        private readonly float _gap;
        private readonly float _sampleStep;

        public IReadOnlyList<Plot> Plots => _plots;

        public PlotPlanner(float gap, float sampleStep = 0.7f)
        {
            _gap = gap;
            _sampleStep = sampleStep;
        }

        /// <summary>Rectangles already standing (a building yard, a plaza...) that plots must keep a walkway from.</summary>
        public void Reserve(Rect rect)
        {
            _taken.Add(rect);
        }

        /// <summary>Packs as many <paramref name="size"/> plots as fit into <paramref name="area"/>, scanning a
        /// <paramref name="step"/> grid and trying both orientations. Returns how many were added.</summary>
        public int Pack(Rect area, Vector2 size, float step, FreeTest free, int tier, int maxCount = int.MaxValue)
        {
            int added = 0;
            for (float z = area.yMin + size.y * 0.5f; z <= area.yMax - size.y * 0.5f; z += step)
            {
                for (float x = area.xMin + size.x * 0.5f; x <= area.xMax - size.x * 0.5f; x += step)
                {
                    if (added >= maxCount)
                    {
                        return added;
                    }

                    var centre = new Vector2(x, z);
                    for (int o = 0; o < 2; o++)
                    {
                        Vector2 extent = o == 0 ? size : new Vector2(size.y, size.x);
                        var rect = new Rect(centre - extent * 0.5f, extent);
                        if (!area.Contains(rect.min) || !area.Contains(rect.max) || Overlaps(rect) || !AllFree(rect, free))
                        {
                            continue;
                        }

                        _taken.Add(rect);
                        _plots.Add(new Plot { Center = centre, Size = size, Yaw = o == 0 ? 0f : 90f, Tier = tier, Index = _plots.Count });
                        added++;
                        break;
                    }
                }
            }

            return added;
        }

        /// <summary>True when the point lies in (or within <paramref name="margin"/> of) a plot or reserved rectangle.</summary>
        public bool IsTaken(Vector2 point, float margin)
        {
            for (int i = 0; i < _taken.Count; i++)
            {
                Rect r = _taken[i];
                if (point.x >= r.xMin - margin && point.x <= r.xMax + margin && point.y >= r.yMin - margin && point.y <= r.yMax + margin)
                {
                    return true;
                }
            }

            return false;
        }

        public delegate void RowVisit(Vector2 point, Vector2 tangent, int index);

        /// <summary>Walks a polyline and calls <paramref name="visit"/> every <paramref name="spacing"/> metres of arc
        /// length - evenly spaced planting along a road, whatever the sample density of the line.</summary>
        public static void AlongPolyline(IList<Vector2> line, float spacing, RowVisit visit)
        {
            float carried = 0f;
            int index = 0;
            for (int s = 0; s < line.Count - 1; s++)
            {
                Vector2 a = line[s];
                Vector2 b = line[s + 1];
                float length = Vector2.Distance(a, b);
                if (length < 0.0001f)
                {
                    continue;
                }

                Vector2 tangent = (b - a) / length;
                float at = spacing - carried;
                while (at <= length)
                {
                    visit(a + tangent * at, tangent, index++);
                    at += spacing;
                }

                carried = length - (at - spacing);
            }
        }

        private bool Overlaps(Rect rect)
        {
            var grown = new Rect(rect.xMin - _gap, rect.yMin - _gap, rect.width + _gap * 2f, rect.height + _gap * 2f);
            for (int i = 0; i < _taken.Count; i++)
            {
                if (_taken[i].Overlaps(grown))
                {
                    return true;
                }
            }

            return false;
        }

        private bool AllFree(Rect rect, FreeTest free)
        {
            float margin = _gap * 0.5f;
            for (float z = rect.yMin - margin; z <= rect.yMax + margin + 0.001f; z += _sampleStep)
            {
                for (float x = rect.xMin - margin; x <= rect.xMax + margin + 0.001f; x += _sampleStep)
                {
                    if (!free(new Vector2(x, z)))
                    {
                        return false;
                    }
                }
            }

            return free(rect.center);
        }

        /// <summary>World position of a plot-local point.</summary>
        public static Vector2 ToWorld(Plot plot, Vector2 local)
        {
            if (Mathf.Abs(plot.Yaw) < 1f)
            {
                return plot.Center + local;
            }

            // Turned plot: the long axis (local x) runs along world +Z, local y along world -X.
            return plot.Center + new Vector2(-local.y, local.x);
        }

        /// <summary>World yaw (Unity, degrees about Y) of an item turned with its plot: mapping local +x onto world +Z
        /// is a -90 degree turn in Unity's clockwise-from-above convention.</summary>
        public static float ToWorldYaw(Plot plot, float localYaw)
        {
            return localYaw + (Mathf.Abs(plot.Yaw) < 1f ? 0f : -90f);
        }

        /// <summary>Evenly spaced positions along one axis, centred: count items step apart.</summary>
        public static float RowOffset(int index, int count, float step)
        {
            return (index - (count - 1) * 0.5f) * step;
        }

        /// <summary>A grid of one key filling the plot (minus a border), rows along the long axis.</summary>
        public static void Grid(List<Item> items, Plot plot, string key, float along, float across, float border, float yaw, float scale)
        {
            int nx = Mathf.Max(1, Mathf.FloorToInt((plot.Size.x - border * 2f) / along) + 1);
            int ny = Mathf.Max(1, Mathf.FloorToInt((plot.Size.y - border * 2f) / across) + 1);
            for (int j = 0; j < ny; j++)
            {
                for (int i = 0; i < nx; i++)
                {
                    items.Add(new Item(key, RowOffset(i, nx, along), RowOffset(j, ny, across), yaw, scale));
                }
            }
        }

        public static float Hash(int a, int b)
        {
            unchecked
            {
                uint h = (uint)(a * 374761393 + b * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
            }
        }
    }
}
