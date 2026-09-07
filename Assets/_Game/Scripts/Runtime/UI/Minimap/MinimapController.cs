using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.Minimap
{
    /// <summary>Drives the 2D schematic minimap: converts world-space waypoint positions into minimap UI space,
    /// feeds the path polyline graphic, places the Spawn/Goal/corner-waypoint markers, and formats the wave-prep
    /// timer text. Pure "view" class - never touches gameplay, never reads path/wave data itself (see
    /// MinimapPresenter for the subscribing side, matching this project's existing View/Presenter split e.g.
    /// WaveHUDView/WaveHUDPresenter). Everything here is schematic 2D UI, never a world Camera/RenderTexture.</summary>
    public sealed class MinimapController : MonoBehaviour
    {
        [Header("Layout")]
        [SerializeField]
        [Tooltip("MapContent - defines the drawing area (its rect.width/height are 'MinimapWidth/Height'). Must " +
            "be centered: anchorMin=anchorMax=pivot=(0.5,0.5), so anchoredPosition (0,0) is the map's visual center.")]
        private RectTransform _mapContentRect;

        [SerializeField]
        private MinimapPathGraphic _pathGraphic;

        [SerializeField]
        private RectTransform _spawnMarker;

        [SerializeField]
        private RectTransform _goalMarker;

        [SerializeField]
        [Tooltip("Small corner-waypoint dots are created under this at InitializePath time (built once, not " +
            "pooled per-enemy - this project's path never changes after level load).")]
        private Transform _waypointMarkersParent;

        [SerializeField]
        [Tooltip("Optional. Live player-UFO position marker - see SetUFOPosition. Leave unassigned if not needed.")]
        private RectTransform _ufoMarker;

        [SerializeField]
        [Tooltip("Optional. Enemy dot markers are pooled here (built once, on first SetEnemyPositions call) - see SetEnemyPositions.")]
        private Transform _enemyMarkersParent;

        [SerializeField]
        [Tooltip("Optional. Static BuildNode/base-slot dots are created under this at InitializeBuildNodes time " +
            "(built once, never pooled - BuildNodes don't move after level load). See InitializeBuildNodes.")]
        private Transform _buildNodeMarkersParent;

        [Header("Waypoint dot sprite/style")]
        [SerializeField]
        private Sprite _dotSprite;

        [SerializeField]
        private Color _waypointMarkerColor = new Color(0.09f, 0.31f, 0.45f); // #174F72

        [SerializeField, Min(1f)]
        private float _waypointMarkerSize = 8f;

        [Header("Enemy markers (optional - pooled, see MinimapPresenter for the polling tick)")]
        [SerializeField]
        private Color _enemyMarkerColor = new Color(0.91f, 0.2f, 0.17f); // #E8332B

        [SerializeField, Min(1f)]
        private float _enemyMarkerSize = 9f;

        [SerializeField, Min(1)]
        [Tooltip("Marker pool size cap - never grows past this regardless of how many enemies are actually " +
            "alive, so a huge wave can't spawn UI objects unbounded.")]
        private int _maxEnemyMarkers = 30;

        [Header("Base/BuildNode markers (optional - static, built once, see InitializeBuildNodes)")]
        [SerializeField]
        private Color _buildNodeMarkerColor = new Color(0.31f, 0.66f, 0.91f); // #4FA8E8

        [SerializeField, Min(1f)]
        private float _buildNodeMarkerSize = 11f;

        [Header("Orientation / Bounds (verify visually, don't assume world axes - see spec)")]
        [SerializeField]
        private bool _flipX;

        [SerializeField]
        private bool _flipY;

        [SerializeField]
        private float _rotationDegrees;

        [SerializeField, Min(1f)]
        [Tooltip("Bounds padding around the path's own min/max extents so the line never touches the panel edge. " +
            "1.15 = 15% padding.")]
        private float _boundsPaddingFactor = 1.15f;

        [Header("Path style")]
        [SerializeField, Min(0.5f)]
        private float _pathWidthPx = 10f;

        [SerializeField, Min(1)]
        [Tooltip("Interpolated segments drawn between each pair of consecutive waypoints (Catmull-Rom spline - " +
            "see BuildSmoothedPathPoints). The real EnemyPath3D waypoints are AI navigation nodes, not the " +
            "visual road's own centerline, so several small direction changes between neighbouring waypoints are " +
            "normal - connecting them with straight segments turns those into a visibly faceted zigzag that " +
            "doesn't match the smooth, continuous dirt road rendered in the actual level (confirmed by comparing " +
            "directly against a Scene view screenshot of the same level). A spline through the exact same " +
            "waypoint positions removes the faceting without changing the path's real shape/proportions - the " +
            "curve still passes through every real waypoint, it just arrives smoothly instead of via sharp " +
            "corners. 1 = smoothing disabled (draws the raw straight segments).")]
        private int _pathSmoothingSegments = 8;

        [Header("Corner-waypoint selection")]
        [SerializeField, Range(1f, 90f)]
        [Tooltip("An interior waypoint becomes a visible dot only if the path turns by at least this many degrees " +
            "there - avoids cluttering the minimap with a dot at every one of a long path's many near-straight points.")]
        private float _cornerAngleThresholdDegrees = 20f;

        [SerializeField, Min(0)]
        private int _maxWaypointMarkers = 10;

        [Header("Timer")]
        [SerializeField]
        private TMP_Text _timerText;

        [SerializeField]
        private Color _timerNormalColor = Color.white;

        [SerializeField]
        private Color _timerWarningColor = new Color(1f, 0.85f, 0.2f);

        [SerializeField]
        private Color _timerDangerColor = new Color(1f, 0.3f, 0.25f);

        [SerializeField, Min(0f)]
        private float _timerWarningThreshold = 30f;

        [SerializeField, Min(0f)]
        private float _timerDangerThreshold = 10f;

        [Header("Pulse (goal marker only, very light)")]
        [SerializeField]
        private bool _pulseGoalMarker = true;

        [SerializeField, Range(0f, 0.3f)]
        private float _pulseScaleAmount = 0.08f;

        [SerializeField, Min(0.1f)]
        private float _pulseDurationSeconds = 1.5f;

        private Vector2 _worldCenter; // .y here holds world Z, not world Y - this map is a top-down XZ schematic
        private float _paddedRangeX;
        private float _paddedRangeZ;
        private bool _boundsValid;
        private float _pulseTimer;
        private readonly List<Transform> _spawnedWaypointDots = new List<Transform>();
        private readonly List<Transform> _spawnedBuildNodeDots = new List<Transform>();
        private readonly List<RectTransform> _enemyMarkerPool = new List<RectTransform>();

        /// <summary>Call once (level load). Computes bounds from the waypoints themselves, converts every point to
        /// minimap-local space, and builds the path graphic + Spawn/Goal/corner markers. Safe to call again (e.g.
        /// a level reload) - previous corner dots are cleared first.</summary>
        public void InitializePath(IReadOnlyList<Transform> waypoints)
        {
            if (waypoints == null || waypoints.Count == 0)
            {
                Debug.LogWarning("[MinimapController] InitializePath called with no waypoints.", this);
                return;
            }

            var worldPoints = new List<Vector3>(waypoints.Count);
            for (int i = 0; i < waypoints.Count; i++)
            {
                if (waypoints[i] != null)
                {
                    worldPoints.Add(waypoints[i].position);
                }
            }

            if (worldPoints.Count == 0)
            {
                Debug.LogWarning("[MinimapController] InitializePath: every waypoint Transform was null.", this);
                return;
            }

            ComputeBounds(worldPoints);

            var uiPoints = new List<Vector2>(worldPoints.Count);
            for (int i = 0; i < worldPoints.Count; i++)
            {
                uiPoints.Add(WorldToMinimap(worldPoints[i]));
            }

            if (_pathGraphic != null)
            {
                _pathGraphic.SetThickness(_pathWidthPx);
                _pathGraphic.SetPoints(BuildSmoothedPathPoints(uiPoints));
            }

            if (_spawnMarker != null)
            {
                _spawnMarker.anchoredPosition = uiPoints[0];
                _spawnMarker.gameObject.SetActive(true);
            }

            if (_goalMarker != null)
            {
                _goalMarker.anchoredPosition = uiPoints[uiPoints.Count - 1];
                _goalMarker.gameObject.SetActive(true);
            }

            PlaceWaypointMarkers(worldPoints, uiPoints);
        }

        /// <summary>Inserts _pathSmoothingSegments interpolated points between every consecutive pair of rawPoints
        /// using a Catmull-Rom spline, so the drawn line arrives at each real waypoint smoothly instead of via a
        /// sharp corner - see _pathSmoothingSegments' own doc comment for why. The curve passes exactly through
        /// every point in rawPoints (including index 0 and the last index - Spawn/Goal placement, which reads
        /// straight from rawPoints separately, is unaffected). Endpoint segments reuse the nearest real point as
        /// their missing outer control point (a "clamped" spline) rather than extrapolating past it, so the path
        /// never overshoots beyond the real Spawn/Goal positions.</summary>
        private List<Vector2> BuildSmoothedPathPoints(List<Vector2> rawPoints)
        {
            if (rawPoints.Count < 3 || _pathSmoothingSegments <= 1)
            {
                return rawPoints;
            }

            var smoothed = new List<Vector2>((rawPoints.Count - 1) * _pathSmoothingSegments + 1);
            for (int i = 0; i < rawPoints.Count - 1; i++)
            {
                Vector2 p0 = rawPoints[Mathf.Max(i - 1, 0)];
                Vector2 p1 = rawPoints[i];
                Vector2 p2 = rawPoints[i + 1];
                Vector2 p3 = rawPoints[Mathf.Min(i + 2, rawPoints.Count - 1)];

                int startStep = i == 0 ? 0 : 1; // step 0 of segment i == the last step of segment i-1 (both p1)
                for (int step = startStep; step <= _pathSmoothingSegments; step++)
                {
                    float t = step / (float)_pathSmoothingSegments;
                    smoothed.Add(CatmullRom(p0, p1, p2, p3, t));
                }
            }

            return smoothed;
        }

        /// <summary>Standard uniform Catmull-Rom spline: passes exactly through p1 (t=0) and p2 (t=1), curving
        /// toward the p0->p2 and p1->p3 tangent directions in between.</summary>
        private static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * (
                (2f * p1) +
                (p2 - p0) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (3f * p1 - p0 - 3f * p2 + p3) * t3);
        }

        /// <summary>Call once (level load, after InitializePath - relies on the same bounds/scale it computes).
        /// Places one static dot per BuildNode/base-slot world position. BuildNodes never move after level load,
        /// so - unlike SetEnemyPositions - these are built once here and never pooled or repositioned again.
        /// Safe to call again (e.g. a level reload); previous dots are cleared first. No-op if no
        /// _buildNodeMarkersParent was assigned (feature left off).</summary>
        public void InitializeBuildNodes(IReadOnlyList<Vector3> worldPositions)
        {
            for (int i = 0; i < _spawnedBuildNodeDots.Count; i++)
            {
                if (_spawnedBuildNodeDots[i] != null)
                {
                    Destroy(_spawnedBuildNodeDots[i].gameObject);
                }
            }

            _spawnedBuildNodeDots.Clear();

            if (_buildNodeMarkersParent == null || _dotSprite == null || worldPositions == null || !_boundsValid)
            {
                return;
            }

            for (int i = 0; i < worldPositions.Count; i++)
            {
                var markerObject = new GameObject("BuildNodeDot", typeof(RectTransform), typeof(Image));
                markerObject.transform.SetParent(_buildNodeMarkersParent, false);
                var rect = (RectTransform)markerObject.transform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(_buildNodeMarkerSize, _buildNodeMarkerSize);
                rect.anchoredPosition = WorldToMinimap(worldPositions[i]);

                var image = markerObject.GetComponent<Image>();
                image.sprite = _dotSprite;
                image.color = _buildNodeMarkerColor;

                _spawnedBuildNodeDots.Add(markerObject.transform);
            }
        }

        /// <summary>World position (XZ plane) -> minimap-local point, centered on MapContent's pivot. Public so
        /// callers (e.g. enemy/UFO markers) can convert a single point without re-deriving the formula.
        ///
        /// Uses ONE uniform scale for both axes (the smaller of width-fit/height-fit "Mathf.Min"), never two
        /// independent X/Y scale factors - a path's real X range and Z range are almost never equal (this
        /// project's levels run mostly north-south, e.g. a small X spread over a much larger Z spread), so
        /// normalizing each axis to the same square panel size independently stretched the path into a
        /// different shape than the real level - confirmed by comparing the rendered minimap directly against a
        /// top-down Scene view screenshot of the same level, not assumed. Fit-to-box keeps the true proportions:
        /// a tall/narrow path renders tall/narrow, centered, with a bit of empty margin on the short axis rather
        /// than being stretched to fill it.</summary>
        public Vector2 WorldToMinimap(Vector3 worldPosition)
        {
            if (!_boundsValid || _mapContentRect == null)
            {
                return Vector2.zero;
            }

            float mapWidth = _mapContentRect.rect.width;
            float mapHeight = _mapContentRect.rect.height;
            float scale = Mathf.Min(mapWidth / _paddedRangeX, mapHeight / _paddedRangeZ);

            float dx = worldPosition.x - _worldCenter.x;
            float dz = worldPosition.z - _worldCenter.y;

            if (_flipX)
            {
                dx = -dx;
            }

            if (_flipY)
            {
                dz = -dz;
            }

            Vector2 uiPoint = new Vector2(dx, dz) * scale;

            if (!Mathf.Approximately(_rotationDegrees, 0f))
            {
                uiPoint = Quaternion.Euler(0f, 0f, _rotationDegrees) * uiPoint;
            }

            return uiPoint;
        }

        /// <summary>mm:ss countdown text, white -> yellow -> red as it runs low. Bind this to whatever the
        /// project's existing timer source is (see MinimapPresenter) - never call this from a timer created here.</summary>
        public void SetTime(float seconds)
        {
            if (_timerText == null)
            {
                return;
            }

            int totalSeconds = Mathf.Max(0, Mathf.CeilToInt(seconds));
            int minutes = totalSeconds / 60;
            int secs = totalSeconds % 60;
            _timerText.text = minutes + ":" + secs.ToString("00");

            Color targetColor = _timerNormalColor;
            if (seconds <= _timerDangerThreshold)
            {
                targetColor = _timerDangerColor;
            }
            else if (seconds <= _timerWarningThreshold)
            {
                targetColor = _timerWarningColor;
            }

            _timerText.color = targetColor;
        }

        /// <summary>Below this separation (in minimap UI pixels), the UFO marker is nudged away from the
        /// Spawn/Goal marker it's crowding - see SetUFOPosition. Some levels legitimately start the player right
        /// next to the path's endpoint (e.g. "defend the base the path leads to"), which without this would
        /// render the UFO marker's icon almost exactly on top of the Goal marker's own dot+glow - reading as one
        /// merged blob pinned at the edge instead of two distinct, readable icons.</summary>
        private const float MinUfoMarkerSeparationPx = 20f;

        /// <summary>Optional: moves (and activates on first call) a live marker for the player-controlled UFO.
        /// Never call this for the fixed Goal marker - that one is set once in InitializePath from the path's
        /// last waypoint. No-op if no UFO marker was assigned.</summary>
        public void SetUFOPosition(Vector3 worldPosition)
        {
            if (_ufoMarker == null)
            {
                return;
            }

            if (!_ufoMarker.gameObject.activeSelf)
            {
                _ufoMarker.gameObject.SetActive(true);
            }

            Vector2 point = WorldToMinimap(worldPosition);
            point = SeparateFromMarker(point, _goalMarker);
            point = SeparateFromMarker(point, _spawnMarker);
            _ufoMarker.anchoredPosition = point;
        }

        /// <summary>If point is within MinUfoMarkerSeparationPx of anchor's own anchoredPosition, pushes it
        /// straight out to exactly that distance (in the same direction, or an arbitrary direction if the two
        /// are exactly coincident) - see MinUfoMarkerSeparationPx's own doc comment for why.</summary>
        private static Vector2 SeparateFromMarker(Vector2 point, RectTransform anchor)
        {
            if (anchor == null || !anchor.gameObject.activeSelf)
            {
                return point;
            }

            Vector2 anchorPoint = anchor.anchoredPosition;
            Vector2 offset = point - anchorPoint;
            float distance = offset.magnitude;
            if (distance >= MinUfoMarkerSeparationPx)
            {
                return point;
            }

            Vector2 direction = distance > 0.01f ? offset / distance : Vector2.down;
            return anchorPoint + direction * MinUfoMarkerSeparationPx;
        }

        /// <summary>Optional: repositions a pooled set of enemy dot markers to the given world positions (already
        /// capped to _maxEnemyMarkers by the caller or here, whichever is smaller). The pool is built once, on
        /// the first call, then only ever SetActive+repositioned - never Instantiate/Destroy per enemy, never
        /// per-frame (see MinimapPresenter's own throttled tick, this method itself does no throttling). No-op
        /// if no _enemyMarkersParent was assigned (feature left off).</summary>
        public void SetEnemyPositions(IReadOnlyList<Vector3> worldPositions)
        {
            if (_enemyMarkersParent == null)
            {
                return;
            }

            EnsureEnemyMarkerPool();

            int count = worldPositions != null ? Mathf.Min(worldPositions.Count, _enemyMarkerPool.Count) : 0;
            for (int i = 0; i < _enemyMarkerPool.Count; i++)
            {
                RectTransform marker = _enemyMarkerPool[i];
                if (i < count)
                {
                    marker.anchoredPosition = WorldToMinimap(worldPositions[i]);
                    if (!marker.gameObject.activeSelf)
                    {
                        marker.gameObject.SetActive(true);
                    }
                }
                else if (marker.gameObject.activeSelf)
                {
                    marker.gameObject.SetActive(false);
                }
            }
        }

        private void EnsureEnemyMarkerPool()
        {
            if (_enemyMarkerPool.Count > 0 || _dotSprite == null)
            {
                return;
            }

            for (int i = 0; i < _maxEnemyMarkers; i++)
            {
                var markerObject = new GameObject("EnemyDot", typeof(RectTransform), typeof(Image));
                markerObject.transform.SetParent(_enemyMarkersParent, false);
                var rect = (RectTransform)markerObject.transform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(_enemyMarkerSize, _enemyMarkerSize);

                var image = markerObject.GetComponent<Image>();
                image.sprite = _dotSprite;
                image.color = _enemyMarkerColor;

                markerObject.SetActive(false);
                _enemyMarkerPool.Add(rect);
            }
        }

        private void ComputeBounds(List<Vector3> worldPoints)
        {
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            for (int i = 0; i < worldPoints.Count; i++)
            {
                Vector3 p = worldPoints[i];
                if (p.x < minX) minX = p.x;
                if (p.x > maxX) maxX = p.x;
                if (p.z < minZ) minZ = p.z;
                if (p.z > maxZ) maxZ = p.z;
            }

            _worldCenter = new Vector2((minX + maxX) * 0.5f, (minZ + maxZ) * 0.5f);
            _paddedRangeX = Mathf.Max((maxX - minX) * _boundsPaddingFactor, 1f);
            _paddedRangeZ = Mathf.Max((maxZ - minZ) * _boundsPaddingFactor, 1f);
            _boundsValid = true;
        }

        private void PlaceWaypointMarkers(List<Vector3> worldPoints, List<Vector2> uiPoints)
        {
            for (int i = 0; i < _spawnedWaypointDots.Count; i++)
            {
                if (_spawnedWaypointDots[i] != null)
                {
                    Destroy(_spawnedWaypointDots[i].gameObject);
                }
            }

            _spawnedWaypointDots.Clear();

            if (_waypointMarkersParent == null || _dotSprite == null)
            {
                return;
            }

            List<int> indices = SelectCornerIndices(worldPoints);
            for (int n = 0; n < indices.Count; n++)
            {
                int index = indices[n];

                var markerObject = new GameObject("WaypointDot", typeof(RectTransform), typeof(Image));
                markerObject.transform.SetParent(_waypointMarkersParent, false);
                var rect = (RectTransform)markerObject.transform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(_waypointMarkerSize, _waypointMarkerSize);
                rect.anchoredPosition = uiPoints[index];

                var image = markerObject.GetComponent<Image>();
                image.sprite = _dotSprite;
                image.color = _waypointMarkerColor;

                _spawnedWaypointDots.Add(markerObject.transform);
            }
        }

        /// <summary>First + last are excluded here (Spawn/Goal already mark them); only INTERIOR points where the
        /// path bends by at least _cornerAngleThresholdDegrees qualify - "the waypoint góc cua chính" the spec
        /// asks for, not all 20+ path points. If more corners qualify than _maxWaypointMarkers, keeps an evenly
        /// spaced subset so the strongest bends across the whole path stay represented rather than only the first
        /// few.</summary>
        private List<int> SelectCornerIndices(List<Vector3> worldPoints)
        {
            var corners = new List<int>();
            for (int i = 1; i < worldPoints.Count - 1; i++)
            {
                Vector2 prev = new Vector2(worldPoints[i - 1].x, worldPoints[i - 1].z);
                Vector2 curr = new Vector2(worldPoints[i].x, worldPoints[i].z);
                Vector2 next = new Vector2(worldPoints[i + 1].x, worldPoints[i + 1].z);

                Vector2 dirIn = curr - prev;
                Vector2 dirOut = next - curr;
                if (dirIn.sqrMagnitude < 0.0001f || dirOut.sqrMagnitude < 0.0001f)
                {
                    continue;
                }

                float angle = Vector2.Angle(dirIn.normalized, dirOut.normalized);
                if (angle >= _cornerAngleThresholdDegrees)
                {
                    corners.Add(i);
                }
            }

            if (corners.Count <= _maxWaypointMarkers || _maxWaypointMarkers <= 0)
            {
                return corners;
            }

            var trimmed = new List<int>(_maxWaypointMarkers);
            for (int n = 0; n < _maxWaypointMarkers; n++)
            {
                int sourceIndex = Mathf.RoundToInt(n * (corners.Count - 1) / (float)Mathf.Max(1, _maxWaypointMarkers - 1));
                trimmed.Add(corners[sourceIndex]);
            }

            return trimmed;
        }

        private void Update()
        {
            if (!_pulseGoalMarker || _goalMarker == null)
            {
                return;
            }

            _pulseTimer += Time.deltaTime;
            float phase = (_pulseTimer / _pulseDurationSeconds) * (Mathf.PI * 2f);
            float t = (Mathf.Sin(phase) + 1f) * 0.5f;
            float scale = 1f + t * _pulseScaleAmount;
            _goalMarker.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
