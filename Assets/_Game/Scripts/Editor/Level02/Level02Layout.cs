using System.Collections.Generic;
using UnityEngine;

namespace AlienDefense.EditorTools
{
    /// <summary>The art-directed, fixed layout of Level_02 - a farm village crossed by ONE snake-shaped main route,
    /// traced from the level's minimap reference (the minimap is the blueprint, not decoration).
    ///
    /// World frame (same as Level_01): the gameplay camera sits on the +X side and looks down -X, so screen-up is -X
    /// and screen-right is +Z. The level's minimap uses the same frame (MinimapController flipX+flipY+rotate 90:
    /// minimap right = +Z, minimap up = -X), so the route below reads on the minimap exactly as it is laid out here.
    ///
    /// Route, bottom (START, player base) to top (END, enemy barn) as seen on the minimap:
    ///   start stem going up -> bends right -> hairpin hook on the right, back to the middle -> long run to the left
    ///   -> U-turn on the left -> long run to the right -> bend at the upper right, curling back up-left -> END.
    /// Enemies walk it the other way (barn -> base); EnemyPath3D is resampled from the same spline.
    ///
    /// Scale is taken from the project, not invented: the UFO is 2.2 m across (measured on UFO_Player/ufo); the main
    /// road is 5.2 m (~2.4 UFO); hairpins have a 5-6.6 m centre-line radius so the road's inner edge never pinches;
    /// the two long runs are ~13 m apart so a BuildNode between them covers both. Playable area 74 x 60 m (X -36..38,
    /// Z -30..30); the route spans ~73% of its length and ~79% of its width, the rest is farm village.
    ///
    /// Zones around the route: base/start clearing (south, +X); central village with market, houses and windmill
    /// (bottom-left); farmhouse, storage barn, emergency corner (bottom-right); cow pasture between the two runs;
    /// farm village with church, red barn and houses (top-left); water tower (top-right); enemy barn (north end).</summary>
    public static partial class Level02Builder
    {
        // ------------------------------------------------------------------ playable area

        // Widened on 2026-10-11 (user: bigger map, enough absorbable props to max every skill): +16 m behind the base
        // (+X) and +14 m on both sides (Z). Was X -36..38, Z -30..30 with the rim at X 41.5 / Z 33. The route,
        // buildings and BuildNodes keep their positions; the new ring is countryside for the pickup fields.
        private const float MinX = -36f; // the enemy barn door: the UFO never flies into the barn
        private const float MaxX = 54f;
        private const float MinZ = -44f;
        private const float MaxZ = 44f;

        /// <summary>Height of the playable ground (terrain units). Level_01 plays around y = 3.</summary>
        private const float GroundY = 2f;

        /// <summary>Cliff rim (foot of the first cliff face) - a little outside the playable rectangle.</summary>
        private const float RimMinX = -50f;
        private const float RimMaxX = 57.5f;
        private const float RimMinZ = -47f;
        private const float RimMaxZ = 47f;

        private const float UfoDiameter = 2.2f;
        private const float MainRoadWidth = 5.2f;   // ~2.4 UFO
        private const float SideRoadWidth = 3.4f;   // ~1.5 UFO, decorative spurs only
        private const float EnemyLaneHalfWidth = 1.7f;

        // ------------------------------------------------------------------ roads

        /// <summary>Main route = enemy path (control points, smoothed with Catmull-Rom). Listed in enemy walking order:
        /// starts inside the enemy barn (minimap top), ends at the base target (minimap bottom). The arcs are sampled
        /// every 45 degrees on true circles so the spline stays round.</summary>
        private static readonly Vector2[] MainRoad =
        {
            new Vector2(-44f, 7f), new Vector2(-36.5f, 7f),                                          // barn -> door (END)
            new Vector2(-33.8f, 7.4f), new Vector2(-30.5f, 9f),                                      // leaves the courtyard
            new Vector2(-27f, 12f), new Vector2(-25.24f, 16.24f), new Vector2(-21f, 18f),             // upper-right bend (centre -21, 12; r 6)
            new Vector2(-16.76f, 16.24f), new Vector2(-15f, 12f),
            new Vector2(-14.7f, 7.5f), new Vector2(-14.6f, 3f), new Vector2(-14.7f, -1.5f),           // upper run, right -> left on screen
            new Vector2(-14.8f, -6f), new Vector2(-14.7f, -10.5f), new Vector2(-14.6f, -14.5f),
            new Vector2(-12.67f, -19.17f), new Vector2(-8f, -21.1f), new Vector2(-3.33f, -19.17f),    // left U-turn (centre -8, -14.5; r 6.6)
            new Vector2(-1.4f, -14.5f), new Vector2(-1.3f, -10f), new Vector2(-1.2f, -5.5f),          // lower run, left -> right on screen
            new Vector2(-1.3f, -1f), new Vector2(-1.5f, 3.5f), new Vector2(-1.6f, 8f), new Vector2(-1.5f, 12f),
            new Vector2(-1.4f, 16f), new Vector2(0.06f, 19.54f), new Vector2(3.6f, 21f),              // right hook (centre 3.6, 16; r 5)
            new Vector2(7.14f, 19.54f), new Vector2(8.6f, 16f),
            new Vector2(9.1f, 11.5f), new Vector2(11.2f, 7.3f), new Vector2(14.8f, 4.5f),             // first bend
            new Vector2(18f, 4f), new Vector2(21f, 4f),                                              // start stem -> base (START)
        };

        /// <summary>Short decorative spurs (not on the minimap, not walked by enemies). Index 0 is the scooter's lane.</summary>
        private static readonly Vector2[][] SideRoads =
        {
            // Stem -> market square (bottom-left village).
            new[] { new Vector2(19.5f, 2.2f), new Vector2(17.5f, -3f), new Vector2(13.5f, -7f), new Vector2(10f, -10.5f), new Vector2(9f, -13f) },
            // Stem -> blue farmhouse (bottom-right).
            new[] { new Vector2(20.5f, 6.2f), new Vector2(21.5f, 11f), new Vector2(22.5f, 16f), new Vector2(23f, 19.2f) },
            // U-turn -> red barn doors (top-left).
            new[] { new Vector2(-13.2f, -18f), new Vector2(-16.5f, -21f), new Vector2(-20.2f, -22.6f) },
            // Upper-right bend -> water tower (top-right).
            new[] { new Vector2(-24.2f, 17.5f), new Vector2(-26f, 20.5f), new Vector2(-27.2f, 22.2f) },
        };

        private enum GroundKind { LightDirt, DarkDirt, DryGrass }

        private struct Clearing
        {
            public Vector2 Center;
            public Vector2 Radii;
            public float Yaw;
            public GroundKind Kind;
            public bool Movement; // kept clear of decoration
            public bool Pad;      // a BuildNode pad: painted and flattened, never rim-dressed

            public Clearing(float x, float z, float rx, float rz, float yaw, GroundKind kind, bool movement, bool pad = false)
            {
                Center = new Vector2(x, z);
                Radii = new Vector2(rx, rz);
                Yaw = yaw;
                Kind = kind;
                Movement = movement;
                Pad = pad;
            }
        }

        /// <summary>Plazas, yards and large dirt clearings (noise-edged ellipses painted into the terrain). The
        /// BuildNode pads are appended at build time (see PrepareRoads), so <see cref="Clearings"/> is the full list.</summary>
        private static readonly Clearing[] AuthoredClearings =
        {
            new Clearing(26.5f, 4f, 8.5f, 7f, 8f, GroundKind.LightDirt, true),        // base / player start clearing
            new Clearing(-36f, 7f, 7.5f, 7f, 0f, GroundKind.DarkDirt, true),          // enemy barn courtyard (END)
            new Clearing(9f, -13f, 5.5f, 4.5f, -8f, GroundKind.LightDirt, true),      // market square
            new Clearing(29f, -22f, 5.2f, 3.6f, 0f, GroundKind.DarkDirt, false),      // windmill yard
            new Clearing(-28.5f, 24.5f, 4.2f, 3.6f, 15f, GroundKind.LightDirt, false), // water tower yard
            new Clearing(31f, 14.5f, 5.6f, 3.6f, 0f, GroundKind.DarkDirt, false),     // emergency corner
            new Clearing(-23f, -6.5f, 2.2f, 4.2f, 0f, GroundKind.LightDirt, false),   // church forecourt
            new Clearing(-19.6f, -22.8f, 2.4f, 4f, 0f, GroundKind.DarkDirt, false),   // red barn doors
            new Clearing(23f, 19f, 4.4f, 2.4f, 0f, GroundKind.LightDirt, false),      // farmhouse yard
            new Clearing(33.2f, 18.6f, 3.6f, 2.2f, 0f, GroundKind.DarkDirt, false),   // storage barn yard
            new Clearing(6f, -2.5f, 3f, 2.6f, 20f, GroundKind.LightDirt, false),      // stone well at the first bend
            // Irregular worn patches (traffic, livestock, work areas).
            new Clearing(35.5f, -26.5f, 3.6f, 2.4f, 30f, GroundKind.DryGrass, false),
            new Clearing(20f, -27.5f, 3.2f, 2f, -10f, GroundKind.DryGrass, false),
            new Clearing(-4.5f, 27.5f, 3.4f, 2.2f, 0f, GroundKind.DryGrass, false),
            new Clearing(-33f, -27.5f, 3.4f, 2.2f, 20f, GroundKind.DryGrass, false),
            new Clearing(12f, 28f, 3f, 1.8f, 0f, GroundKind.DryGrass, false),
            new Clearing(-40f, -12f, 4f, 3f, 20f, GroundKind.DarkDirt, false),
            new Clearing(-40.5f, 24f, 3.5f, 2.5f, -10f, GroundKind.DarkDirt, false),
            new Clearing(-24f, 28f, 3f, 1.8f, 0f, GroundKind.DryGrass, false),
        };

        private static Clearing[] Clearings;

        /// <summary>Fenced cow pasture between the two long runs (trampled ground). The gate faces the lower run
        /// (+X side, toward the camera).</summary>
        private static readonly Rect CowPasture = Rect.MinMaxRect(-11.4f, 1.6f, -5.1f, 10.6f);
        private const float CowPastureGateZ = 6.1f;

        // ------------------------------------------------------------------ gameplay anchors

        /// <summary>Route START (minimap bottom): the base target the enemies walk to.</summary>
        private static readonly Vector2 BaseTargetXZ = new Vector2(21f, 4f);

        /// <summary>Offsets from the base target copied from Level_01 (player base model, landing pad, player start).</summary>
        private static readonly Vector2 PlayerBaseOffset = new Vector2(4.8f, 0.35f);
        private static readonly Vector2 LandingOffset = new Vector2(13.5f, -1.9f);
        private static readonly Vector2 PlayerStartOffset = new Vector2(-1.3f, 0.8f);

        /// <summary>Route END (minimap top): the enemy barn. Enemies spawn inside it and leave through the door.</summary>
        private static readonly Vector2 EnemySpawnXZ = new Vector2(-44f, 7f);
        private static readonly Vector2 EndAreaXZ = new Vector2(-36f, 7f);
        private static readonly Vector2 BossAreaXZ = new Vector2(-33f, 7f);

        /// <summary>Eight BuildNodes = the eight dark dots of the minimap reference, in route order from START to END.
        /// Each sits 5.1-6.7 m from the road centre line (its 3.4 m ring clears the 5.2 m road by 0.8-2.4 m), on the
        /// inside of a bend or between the two long runs, and at least 10 m from any other node.</summary>
        private static readonly Vector2[] BuildNodeXZ =
        {
            new Vector2(17f, 9.4f),     // 01 beside the start stem, right of the base
            new Vector2(12.8f, -0.6f),  // 02 left of the first bend
            new Vector2(3.6f, 14.8f),   // 03 inside the right hook
            new Vector2(-6.6f, 15f),    // 04 between the runs, right
            new Vector2(-6.6f, -3f),    // 05 between the runs, centre
            new Vector2(-8f, -13.5f),   // 06 inside the left U-turn
            new Vector2(-19.8f, -7.5f), // 07 above the upper run, left
            new Vector2(-21f, 11f),     // 08 inside the upper-right bend, near the end
        };

        /// <summary>Radius kept free of props around a BuildNode (ring 1.7 m + tower + walking room).</summary>
        private const float BuildNodeClearRadius = 3.4f;

        // Zone anchors the dressing is built around.
        private static readonly Vector2 MarketSquareXZ = new Vector2(9f, -13f);
        private static readonly Vector2 WindmillXZ = new Vector2(29f, -22.5f);
        private static readonly Vector2 WaterTowerXZ = new Vector2(-28.5f, 24.5f);
        private static readonly Vector2 ChurchXZ = new Vector2(-32.5f, -6.5f);
        private static readonly Vector2 RedBarnXZ = new Vector2(-28.5f, -23f);
        private static readonly Vector2 RedBarnYardXZ = new Vector2(-19.6f, -22.8f);
        private static readonly Vector2 EmergencyCornerXZ = new Vector2(31f, 14.5f);
        private static readonly Vector2 StorageYardXZ = new Vector2(33.2f, 18.6f);
        private static readonly Vector2 CoopXZ = new Vector2(15.5f, 25.5f);
        private static readonly Vector2 EnemyBarnXZ = new Vector2(-42.5f, 7f);
        private static readonly Vector2 BendTreeXZ = new Vector2(6.4f, 4.6f);

        // ------------------------------------------------------------------ landmarks and buildings

        private enum Landmark
        {
            FarmHouseBlue, BarnWhite, SiloRed, ChickenCoop, StoreYellow,
            House, Church, WaterTower, Windmill, WindPump, MarketStall, RedBarn, EnemyBarn, OldShed, Well, WellStone,
        }

        /// <summary>Yaw value meaning "face the nearest road" - resolved once the road splines exist.</summary>
        private const float FaceRoad = float.NaN;

        private struct BuildingSpot
        {
            public Landmark Kind;
            public Vector2 Position;
            public float Yaw;
            public float Scale;
            public int Variant;
            public string Name;

            public BuildingSpot(Landmark kind, float x, float z, float yaw, float scale = 1f, int variant = 0, string name = null)
            {
                Kind = kind;
                Position = new Vector2(x, z);
                Yaw = yaw;
                Scale = scale;
                Variant = variant;
                Name = name;
            }
        }

        /// <summary>Every building in the level, grouped by the zone around the route it belongs to. Yaw 0 = front
        /// toward +Z; FaceRoad turns the front toward the nearest road.</summary>
        private static readonly BuildingSpot[] Buildings =
        {
            // Bottom-right: farmhouse, storage barn, coop, emergency tower (right of the start stem).
            new BuildingSpot(Landmark.FarmHouseBlue, 23f, 23.5f, 180f, 1f, 0, "FarmHouse_Fields"),
            new BuildingSpot(Landmark.ChickenCoop, CoopXZ.x, CoopXZ.y, 200f),
            new BuildingSpot(Landmark.BarnWhite, 33.5f, 24.8f, 180f, 0.95f, 0, "Barn_Storage"),
            new BuildingSpot(Landmark.SiloRed, 37.4f, 18.4f, 0f, 1f),
            new BuildingSpot(Landmark.House, 4f, 27f, 180f, 1f, 3, "House_Hook"),
            new BuildingSpot(Landmark.WindPump, 36.8f, 9.4f, 250f, 1f, 0, "EmergencyTower"),

            // Bottom-left: central village - market, houses, store, stone well, windmill.
            new BuildingSpot(Landmark.House, 21.5f, -9.8f, FaceRoad, 1f, 0, "House_Village_01"),
            new BuildingSpot(Landmark.StoreYellow, 31f, -8.8f, 0f, 0.85f, 0, "Store_Village"),
            new BuildingSpot(Landmark.House, 4f, -24f, FaceRoad, 1f, 1, "House_Village_02"),
            new BuildingSpot(Landmark.House, 16.5f, -25f, FaceRoad, 1f, 2, "House_Village_03"),
            new BuildingSpot(Landmark.MarketStall, 4.5f, -17f, 48f, 1f, 0, "MarketStall_01"),
            new BuildingSpot(Landmark.MarketStall, 10.5f, -19.2f, -14f, 1f, 1, "MarketStall_02"),
            new BuildingSpot(Landmark.MarketStall, 15.2f, -15.2f, -70f, 1f, 2, "MarketStall_03"),
            new BuildingSpot(Landmark.WellStone, 6f, -2.5f, 30f, 1f),
            new BuildingSpot(Landmark.Windmill, WindmillXZ.x, WindmillXZ.y, 200f, 1.3f, 0, "Windmill"),
            new BuildingSpot(Landmark.Well, 3.6f, -10.8f, 0f, 1f),

            // Top-left: farm village - church (front to the camera), red barn, houses, silo, well.
            new BuildingSpot(Landmark.Church, ChurchXZ.x, ChurchXZ.y, 90f, 1f, 0, "Church"),
            new BuildingSpot(Landmark.RedBarn, RedBarnXZ.x, RedBarnXZ.y, 90f, 1f, 0, "Barn_Red"),
            new BuildingSpot(Landmark.SiloRed, -34f, -15.8f, 0f, 1.1f),
            new BuildingSpot(Landmark.House, -22f, 1.8f, FaceRoad, 1f, 5, "House_Farm_01"),
            new BuildingSpot(Landmark.House, -22.5f, -15.2f, FaceRoad, 0.8f, 4, "House_Farm_Garden"),
            new BuildingSpot(Landmark.Well, -27f, 0.6f, 0f, 1f),

            // Top-right: water tower, its house, another farm house by the barn courtyard.
            new BuildingSpot(Landmark.WaterTower, WaterTowerXZ.x, WaterTowerXZ.y, 15f, 1f, 0, "WaterTower"),
            new BuildingSpot(Landmark.House, -21.5f, 26.2f, FaceRoad, 0.9f, 1, "House_WaterTower"),
            new BuildingSpot(Landmark.House, -34.5f, 19.8f, FaceRoad, 0.95f, 2, "House_Farm_02"),
            new BuildingSpot(Landmark.OldShed, -9f, 26.5f, 20f, 1.1f),

            // END: the enemy barn, door toward +X (toward the route).
            new BuildingSpot(Landmark.EnemyBarn, EnemyBarnXZ.x, EnemyBarnXZ.y, 90f, 1f, 0, "EnemyBarn"),
        };

        // ------------------------------------------------------------------ helpers

        private static List<Vector2> _mainRoadSamples;
        private static List<List<Vector2>> _sideRoadSamples;

        /// <summary>Catmull-Rom through the control points, sampled every ~<paramref name="spacing"/> m.</summary>
        private static List<Vector2> Smooth(Vector2[] points, float spacing)
        {
            var result = new List<Vector2>();
            for (int i = 0; i < points.Length - 1; i++)
            {
                Vector2 p0 = points[Mathf.Max(0, i - 1)];
                Vector2 p1 = points[i];
                Vector2 p2 = points[i + 1];
                Vector2 p3 = points[Mathf.Min(points.Length - 1, i + 2)];
                int steps = Mathf.Max(2, Mathf.CeilToInt(Vector2.Distance(p1, p2) / spacing));
                for (int s = 0; s < steps; s++)
                {
                    float t = s / (float)steps;
                    result.Add(CatmullRom(p0, p1, p2, p3, t));
                }
            }

            result.Add(points[points.Length - 1]);
            return result;
        }

        private static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        private static void PrepareRoads()
        {
            _mainRoadSamples = Smooth(MainRoad, 0.75f);
            _sideRoadSamples = new List<List<Vector2>>();
            foreach (Vector2[] road in SideRoads)
            {
                _sideRoadSamples.Add(Smooth(road, 0.75f));
            }

            // Every BuildNode stands on a small soft dirt pad (grass -> dirt transition, ~1.5 ring radii).
            var clearings = new List<Clearing>(AuthoredClearings);
            foreach (Vector2 node in BuildNodeXZ)
            {
                clearings.Add(new Clearing(node.x, node.y, 2.7f, 2.5f, 0f, GroundKind.LightDirt, false, true));
            }

            Clearings = clearings.ToArray();

            // FaceRoad buildings turn their front (+Z at yaw 0) toward the nearest road point.
            for (int i = 0; i < Buildings.Length; i++)
            {
                if (float.IsNaN(Buildings[i].Yaw))
                {
                    Vector2 target = NearestRoadPoint(Buildings[i].Position);
                    Vector2 d = target - Buildings[i].Position;
                    Buildings[i].Yaw = Mathf.Round(Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg);
                }
            }
        }

        private static Vector2 NearestRoadPoint(Vector2 p)
        {
            Vector2 best = p;
            float bestDistance = float.MaxValue;
            void Scan(List<Vector2> line)
            {
                foreach (Vector2 q in line)
                {
                    float d = (q - p).sqrMagnitude;
                    if (d < bestDistance)
                    {
                        bestDistance = d;
                        best = q;
                    }
                }
            }

            Scan(_mainRoadSamples);
            foreach (List<Vector2> road in _sideRoadSamples)
            {
                Scan(road);
            }

            return best;
        }

        private static float DistanceToPolyline(Vector2 p, List<Vector2> line)
        {
            float best = float.MaxValue;
            for (int i = 0; i < line.Count - 1; i++)
            {
                float d = DistanceToSegment(p, line[i], line[i + 1]);
                if (d < best)
                {
                    best = d;
                }
            }

            return best;
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            return Vector2.Distance(p, a + ab * t);
        }

        private static float DistanceToMainRoad(Vector2 p) => DistanceToPolyline(p, _mainRoadSamples);

        /// <summary>Distance to the nearest road edge (negative inside a road).</summary>
        private static float RoadEdgeDistance(Vector2 p)
        {
            float best = DistanceToMainRoad(p) - MainRoadWidth * 0.5f;
            foreach (List<Vector2> road in _sideRoadSamples)
            {
                float d = DistanceToPolyline(p, road) - SideRoadWidth * 0.5f;
                if (d < best)
                {
                    best = d;
                }
            }

            return best;
        }

        /// <summary>Normalised ellipse distance (&lt; 1 inside) of a clearing.</summary>
        private static float ClearingDistance(Clearing c, Vector2 p)
        {
            Vector2 local = Rotate(p - c.Center, -c.Yaw);
            return new Vector2(local.x / c.Radii.x, local.y / c.Radii.y).magnitude;
        }

        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(r);
            float s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        private static bool InsidePlayable(Vector2 p, float margin = 0f) =>
            p.x >= MinX + margin && p.x <= MaxX - margin && p.y >= MinZ + margin && p.y <= MaxZ - margin;
    }
}
