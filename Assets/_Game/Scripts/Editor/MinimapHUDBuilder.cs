using AlienDefense.Building;
using AlienDefense.Enemies;
using AlienDefense.Player;
using AlienDefense.UI.Minimap;
using AlienDefense.Waves;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.EditorTools
{
    /// <summary>Builds the schematic 2D minimap HUD (Timer + path/marker panel) and nests it under an existing
    /// TopHUD-style parent. No world Camera, no RenderTexture - see MinimapController's own doc comment. Reuses
    /// the project's existing EnemyPath3D/WaveController for data (see MinimapPresenter) instead of creating a
    /// second waypoint or timer system.</summary>
    public static class MinimapHUDBuilder
    {
        private const string SpriteDir = "Assets/_Game/Art/Sprite/Minimap";
        private const string UfoIconPath = "Assets/_Game/Art/Sprite/Play/ufoicon.png";

        // Green theme (was cyan/blue) - see MinimapHUDBuilder's colour knobs in the report; change any of these
        // one place to re-theme the whole minimap.
        private static readonly Color PanelBorderColor = HexColor("#0B6E2B");
        private static readonly Color PanelBackgroundColor = HexColor("#22B14C");
        private static readonly Color PanelInnerColor = HexColor("#5EE88A");
        private static readonly Color GridColor = new Color(0.918f, 1f, 0.941f, 0.15f); // #EAFFF0 @ 0.15
        private static readonly Color PathColor = HexColor("#146B2E");
        private static readonly Color WaypointColor = HexColor("#0F4D22");
        private static readonly Color SpawnColor = HexColor("#FF7B29");
        private static readonly Color GoalGlowColor = HexColor("#B9FFCB");
        private static readonly Color GoalIconColor = HexColor("#FFFFFF");
        private static readonly Color BuildNodeColor = HexColor("#4FA8E8");
        private static readonly Color TimerBackgroundColor = HexColor("#22B14C");

        // Bigger overall footprint (was 190x190 panel / 190 wide root).
        private const float RootWidth = 260f;
        private const float TimerHeight = 46f;
        private const float TimerSpacing = 6f;
        private const float PanelSize = 260f;
        private const float PanelBorderThickness = 5f;
        private const float ContentPadding = 12f;

        /// <summary>MinimapController.WorldToMinimap's Z-axis rotation applied after centering/scaling, before
        /// building minimap-space points. This project's Main Camera looks down the world at a fixed yaw
        /// (eulerAngles.y = 270), so on screen world +X reads as roughly vertical and world +Z reads as roughly
        /// horizontal - the OPPOSITE of the naive "world X -> UI right, world Z -> UI up" mapping. Confirmed
        /// empirically for Level_01 (WorldToScreenPoint deltas: world +Z(10) -> pure screen +X; world +X(10) ->
        /// mostly screen -Y) and by comparing the rendered path against a Scene-view screenshot of the level -
        /// without this 90 degrees, a level whose path actually runs "tall" on screen (like Level_01, world-X
        /// span 101 vs world-Z span ~20) instead rendered "wide/diagonal" on the minimap. This is a property of
        /// the shared camera rig, not any one level's path shape, so it's applied as a fixed default here rather
        /// than hand-tuned per level - every level's EnemyPath3D should orient correctly with no extra tuning.</summary>
        private const float PathMapRotationDegrees = 90f;

        /// <summary>MinimapController.WorldToMinimap's _flipX, applied before PathMapRotationDegrees's rotation.
        /// Unlike the rotation above (a geometric fact about the camera rig), this is a chosen display
        /// convention - "enemy spawn draws near the top, the path's far/goal end draws near the bottom" - kept
        /// as a fixed default so it stays consistent across every level rather than being re-picked per level.
        /// At a 90 degree rotation, flipping X (not Y) is what swaps top<->bottom in the final minimap output -
        /// verified against this project's real waypoint data, not assumed (see report).</summary>
        private const bool PathMapFlipX = true;

        /// <summary>MinimapController.WorldToMinimap's _flipY, applied before PathMapRotationDegrees's rotation.
        /// At a 90 degree rotation, flipping Y (not X) is what mirrors left<->right in the final minimap output.
        /// Confirmed against a direct user comparison to the real Level_01 road (Scene view) - without this, the
        /// path's left/right curve direction came out mirrored versus the real level.</summary>
        private const bool PathMapFlipY = true;

        /// <param name="topHUDHeight">Existing TopHUD's own declared height (its sizeDelta.y) - the minimap is
        /// placed just below it so it never overlaps HP/resource/wave UI (see spec "không đè TopHUD").</param>
        /// <param name="buildNodes">Optional. Every BuildNode to show as a static dot - pass null/empty to leave
        /// the feature off.</param>
        /// <param name="player">Optional. The scene's PlayerController - its live position feeds the minimap's
        /// UFO marker every frame (see MinimapPresenter.Update). Pass null to leave the feature off (the UFO
        /// marker GameObject is still built either way, just never activated).</param>
        public static (MinimapController controller, MinimapPresenter presenter) Build(
            Transform safeArea, float topHUDHeight, WaveController waveController, EnemyPath3D enemyPath,
            BuildNode[] buildNodes = null, PlayerController player = null)
        {
            Sprite panelSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + "/T_MinimapPanel.png");
            Sprite dotSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + "/T_MinimapDot.png");
            Sprite gridSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + "/T_MinimapGrid.png");
            Sprite ufoSprite = AssetDatabase.LoadAssetAtPath<Sprite>(UfoIconPath);

            float rootHeight = TimerHeight + TimerSpacing + PanelSize;

            var root = new GameObject("MinimapRoot", typeof(RectTransform));
            root.transform.SetParent(safeArea, false);
            var rootRect = (RectTransform)root.transform;
            rootRect.anchorMin = new Vector2(1f, 1f);
            rootRect.anchorMax = new Vector2(1f, 1f);
            rootRect.pivot = new Vector2(1f, 1f);
            rootRect.anchoredPosition = new Vector2(-20f, -(topHUDHeight + 15f));
            rootRect.sizeDelta = new Vector2(RootWidth, rootHeight);

            // ---------------- Timer panel ----------------
            var timerPanel = new GameObject("TimerPanel", typeof(RectTransform), typeof(Image));
            timerPanel.transform.SetParent(root.transform, false);
            var timerRect = (RectTransform)timerPanel.transform;
            timerRect.anchorMin = new Vector2(0f, 1f);
            timerRect.anchorMax = new Vector2(1f, 1f);
            timerRect.pivot = new Vector2(0.5f, 1f);
            timerRect.anchoredPosition = Vector2.zero;
            timerRect.sizeDelta = new Vector2(0f, TimerHeight);
            var timerBgImage = timerPanel.GetComponent<Image>();
            timerBgImage.sprite = panelSprite;
            timerBgImage.type = Image.Type.Sliced;
            timerBgImage.color = TimerBackgroundColor;

            var timerIcon = new GameObject("TimerIcon", typeof(RectTransform), typeof(Image));
            timerIcon.transform.SetParent(timerPanel.transform, false);
            var timerIconRect = (RectTransform)timerIcon.transform;
            timerIconRect.anchorMin = new Vector2(0f, 0.5f);
            timerIconRect.anchorMax = new Vector2(0f, 0.5f);
            timerIconRect.pivot = new Vector2(0.5f, 0.5f);
            timerIconRect.anchoredPosition = new Vector2(20f, 0f);
            timerIconRect.sizeDelta = new Vector2(20f, 20f);
            var timerIconImage = timerIcon.GetComponent<Image>();
            timerIconImage.sprite = dotSprite;
            timerIconImage.color = Color.white;

            var timerTextObject = new GameObject("TimerText", typeof(RectTransform));
            timerTextObject.transform.SetParent(timerPanel.transform, false);
            var timerTextRect = (RectTransform)timerTextObject.transform;
            timerTextRect.anchorMin = Vector2.zero;
            timerTextRect.anchorMax = Vector2.one;
            timerTextRect.offsetMin = new Vector2(38f, 0f);
            timerTextRect.offsetMax = new Vector2(-8f, 0f);
            var timerText = timerTextObject.AddComponent<TextMeshProUGUI>();
            timerText.text = "0:00";
            timerText.fontSize = 26f;
            timerText.fontStyle = FontStyles.Bold;
            timerText.alignment = TextAlignmentOptions.Center;
            timerText.color = Color.white;
            timerText.outlineWidth = 0.15f;
            timerText.outlineColor = new Color32(6, 40, 56, 255);

            // ---------------- Minimap panel (border -> background -> grid -> content mask -> content) ----------------
            var minimapPanel = new GameObject("MinimapPanel", typeof(RectTransform), typeof(Image));
            minimapPanel.transform.SetParent(root.transform, false);
            var panelRect = (RectTransform)minimapPanel.transform;
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(1f, 1f);
            panelRect.pivot = new Vector2(0.5f, 1f);
            panelRect.anchoredPosition = new Vector2(0f, -(TimerHeight + TimerSpacing));
            panelRect.sizeDelta = new Vector2(0f, PanelSize);
            var panelBorderImage = minimapPanel.GetComponent<Image>();
            panelBorderImage.sprite = panelSprite;
            panelBorderImage.type = Image.Type.Sliced;
            panelBorderImage.color = PanelBorderColor;

            var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(minimapPanel.transform, false);
            var backgroundRect = (RectTransform)background.transform;
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = new Vector2(PanelBorderThickness, PanelBorderThickness);
            backgroundRect.offsetMax = new Vector2(-PanelBorderThickness, -PanelBorderThickness);
            var backgroundImage = background.GetComponent<Image>();
            backgroundImage.sprite = panelSprite;
            backgroundImage.type = Image.Type.Sliced;
            backgroundImage.color = PanelBackgroundColor;

            var innerHighlight = new GameObject("InnerHighlight", typeof(RectTransform), typeof(Image));
            innerHighlight.transform.SetParent(background.transform, false);
            var innerRect = (RectTransform)innerHighlight.transform;
            innerRect.anchorMin = new Vector2(0f, 0.6f);
            innerRect.anchorMax = new Vector2(1f, 1f);
            innerRect.offsetMin = Vector2.zero;
            innerRect.offsetMax = Vector2.zero;
            var innerImage = innerHighlight.GetComponent<Image>();
            innerImage.sprite = panelSprite;
            innerImage.type = Image.Type.Sliced;
            innerImage.color = new Color(PanelInnerColor.r, PanelInnerColor.g, PanelInnerColor.b, 0.35f);

            var grid = new GameObject("Grid", typeof(RectTransform), typeof(Image));
            grid.transform.SetParent(background.transform, false);
            var gridRect = (RectTransform)grid.transform;
            gridRect.anchorMin = Vector2.zero;
            gridRect.anchorMax = Vector2.one;
            gridRect.offsetMin = Vector2.zero;
            gridRect.offsetMax = Vector2.zero;
            var gridImage = grid.GetComponent<Image>();
            gridImage.sprite = gridSprite;
            gridImage.type = Image.Type.Tiled;
            gridImage.pixelsPerUnitMultiplier = 4f; // 32px source tile -> 8px on-screen grid cells
            gridImage.color = GridColor;

            var contentMaskObject = new GameObject("ContentMask", typeof(RectTransform), typeof(RectMask2D));
            contentMaskObject.transform.SetParent(background.transform, false);
            var contentMaskRect = (RectTransform)contentMaskObject.transform;
            contentMaskRect.anchorMin = Vector2.zero;
            contentMaskRect.anchorMax = Vector2.one;
            contentMaskRect.offsetMin = new Vector2(ContentPadding, ContentPadding);
            contentMaskRect.offsetMax = new Vector2(-ContentPadding, -ContentPadding);

            var mapContentObject = new GameObject("MapContent", typeof(RectTransform));
            mapContentObject.transform.SetParent(contentMaskObject.transform, false);
            var mapContentRect = (RectTransform)mapContentObject.transform;
            mapContentRect.anchorMin = new Vector2(0.5f, 0.5f);
            mapContentRect.anchorMax = new Vector2(0.5f, 0.5f);
            mapContentRect.pivot = new Vector2(0.5f, 0.5f);
            mapContentRect.anchoredPosition = Vector2.zero;
            mapContentRect.sizeDelta = Vector2.zero; // filled in below to match ContentMask's actual rect
            // ContentMask stretches to fill Background minus padding, but MapContent uses a centered pivot (not
            // stretch) so WorldToMinimap's centered UI-space formula lines up with anchoredPosition (0,0) =
            // visual center. Size it explicitly to the same padded rect instead of stretching.
            float contentSize = PanelSize - PanelBorderThickness * 2f - ContentPadding * 2f;
            mapContentRect.sizeDelta = new Vector2(contentSize, contentSize);

            var pathGraphicObject = new GameObject("PathGraphic", typeof(RectTransform));
            pathGraphicObject.transform.SetParent(mapContentObject.transform, false);
            var pathGraphicRect = (RectTransform)pathGraphicObject.transform;
            pathGraphicRect.anchorMin = Vector2.zero;
            pathGraphicRect.anchorMax = Vector2.one;
            pathGraphicRect.offsetMin = Vector2.zero;
            pathGraphicRect.offsetMax = Vector2.zero;
            var pathGraphic = pathGraphicObject.AddComponent<MinimapPathGraphic>();
            pathGraphic.color = PathColor;

            var waypointMarkersObject = new GameObject("WaypointMarkers", typeof(RectTransform));
            waypointMarkersObject.transform.SetParent(mapContentObject.transform, false);
            var waypointMarkersRect = (RectTransform)waypointMarkersObject.transform;
            waypointMarkersRect.anchorMin = new Vector2(0.5f, 0.5f);
            waypointMarkersRect.anchorMax = new Vector2(0.5f, 0.5f);
            waypointMarkersRect.pivot = new Vector2(0.5f, 0.5f);
            waypointMarkersRect.anchoredPosition = Vector2.zero;
            waypointMarkersRect.sizeDelta = Vector2.zero;

            // Enemy dot markers (optional feature) - pooled lazily by MinimapController.SetEnemyPositions the
            // first time MinimapPresenter's throttled tick calls it; this is just the empty parent for the pool.
            var enemyMarkersObject = new GameObject("EnemyMarkers", typeof(RectTransform));
            enemyMarkersObject.transform.SetParent(mapContentObject.transform, false);
            var enemyMarkersRect = (RectTransform)enemyMarkersObject.transform;
            enemyMarkersRect.anchorMin = new Vector2(0.5f, 0.5f);
            enemyMarkersRect.anchorMax = new Vector2(0.5f, 0.5f);
            enemyMarkersRect.pivot = new Vector2(0.5f, 0.5f);
            enemyMarkersRect.anchoredPosition = Vector2.zero;
            enemyMarkersRect.sizeDelta = Vector2.zero;

            // Static BuildNode/base-slot dots (optional feature) - built once, directly by
            // MinimapController.InitializeBuildNodes since BuildNodes never move; this is just the empty parent.
            var buildNodeMarkersObject = new GameObject("BuildNodeMarkers", typeof(RectTransform));
            buildNodeMarkersObject.transform.SetParent(mapContentObject.transform, false);
            var buildNodeMarkersRect = (RectTransform)buildNodeMarkersObject.transform;
            buildNodeMarkersRect.anchorMin = new Vector2(0.5f, 0.5f);
            buildNodeMarkersRect.anchorMax = new Vector2(0.5f, 0.5f);
            buildNodeMarkersRect.pivot = new Vector2(0.5f, 0.5f);
            buildNodeMarkersRect.anchoredPosition = Vector2.zero;
            buildNodeMarkersRect.sizeDelta = Vector2.zero;

            RectTransform spawnMarker = BuildDotMarker(mapContentObject.transform, "SpawnMarker", dotSprite, SpawnColor, 22f, addOutline: true);
            RectTransform goalMarker = BuildGoalMarker(mapContentObject.transform, dotSprite, GoalIconColor);
            RectTransform ufoMarker = BuildUfoMarker(mapContentObject.transform, dotSprite, ufoSprite);

            // ---------------- Wire MinimapController ----------------
            var controllerObject = new GameObject("MinimapController");
            controllerObject.transform.SetParent(root.transform, false);
            var controller = controllerObject.AddComponent<MinimapController>();

            var controllerSerialized = new SerializedObject(controller);
            controllerSerialized.FindProperty("_mapContentRect").objectReferenceValue = mapContentRect;
            controllerSerialized.FindProperty("_pathGraphic").objectReferenceValue = pathGraphic;
            controllerSerialized.FindProperty("_spawnMarker").objectReferenceValue = spawnMarker;
            controllerSerialized.FindProperty("_goalMarker").objectReferenceValue = goalMarker;
            controllerSerialized.FindProperty("_waypointMarkersParent").objectReferenceValue = waypointMarkersObject.transform;
            controllerSerialized.FindProperty("_enemyMarkersParent").objectReferenceValue = enemyMarkersObject.transform;
            controllerSerialized.FindProperty("_buildNodeMarkersParent").objectReferenceValue = buildNodeMarkersObject.transform;
            controllerSerialized.FindProperty("_buildNodeMarkerColor").colorValue = BuildNodeColor;
            controllerSerialized.FindProperty("_ufoMarker").objectReferenceValue = ufoMarker;
            controllerSerialized.FindProperty("_dotSprite").objectReferenceValue = dotSprite;
            controllerSerialized.FindProperty("_waypointMarkerColor").colorValue = WaypointColor;
            controllerSerialized.FindProperty("_waypointMarkerSize").floatValue = 10f;
            controllerSerialized.FindProperty("_pathWidthPx").floatValue = 13f;
            controllerSerialized.FindProperty("_rotationDegrees").floatValue = PathMapRotationDegrees;
            controllerSerialized.FindProperty("_flipX").boolValue = PathMapFlipX;
            controllerSerialized.FindProperty("_flipY").boolValue = PathMapFlipY;
            controllerSerialized.FindProperty("_timerText").objectReferenceValue = timerText;
            controllerSerialized.ApplyModifiedPropertiesWithoutUndo();

            var presenterObject = new GameObject("MinimapPresenter");
            presenterObject.transform.SetParent(root.transform, false);
            var presenter = presenterObject.AddComponent<MinimapPresenter>();
            var presenterSerialized = new SerializedObject(presenter);
            presenterSerialized.FindProperty("_waveController").objectReferenceValue = waveController;
            presenterSerialized.FindProperty("_enemyPath").objectReferenceValue = enemyPath;
            presenterSerialized.FindProperty("_controller").objectReferenceValue = controller;
            presenterSerialized.FindProperty("_player").objectReferenceValue = player;
            if (buildNodes != null)
            {
                SerializedProperty buildNodesProperty = presenterSerialized.FindProperty("_buildNodes");
                buildNodesProperty.arraySize = buildNodes.Length;
                for (int i = 0; i < buildNodes.Length; i++)
                {
                    buildNodesProperty.GetArrayElementAtIndex(i).objectReferenceValue = buildNodes[i];
                }
            }

            presenterSerialized.ApplyModifiedPropertiesWithoutUndo();

            return (controller, presenter);
        }

        private static RectTransform BuildDotMarker(Transform parent, string name, Sprite dotSprite, Color color, float size, bool addOutline)
        {
            // The main fill MUST be its own child Image (not a component on markerObject itself) so sibling
            // order alone controls draw order against the outline child below - a Graphic on the parent
            // GameObject always draws before any of its children regardless of sibling index, which previously
            // made the "outline" child silently paint over and hide the fill.
            var markerObject = new GameObject(name, typeof(RectTransform));
            markerObject.transform.SetParent(parent, false);
            var rect = (RectTransform)markerObject.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);

            if (addOutline)
            {
                var outline = new GameObject(name + "Outline", typeof(RectTransform), typeof(Image));
                outline.transform.SetParent(markerObject.transform, false);
                var outlineRect = (RectTransform)outline.transform;
                outlineRect.anchorMin = Vector2.zero;
                outlineRect.anchorMax = Vector2.one;
                outlineRect.offsetMin = new Vector2(-3f, -3f);
                outlineRect.offsetMax = new Vector2(3f, 3f);
                var outlineImage = outline.GetComponent<Image>();
                outlineImage.sprite = dotSprite;
                outlineImage.color = new Color(0.03f, 0.2f, 0.28f, 0.9f);
            }

            var fill = new GameObject(name + "Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(markerObject.transform, false);
            var fillRect = (RectTransform)fill.transform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fillImage = fill.GetComponent<Image>();
            fillImage.sprite = dotSprite;
            fillImage.color = color;

            markerObject.SetActive(false); // shown by MinimapController.InitializePath once real path points exist
            return rect;
        }

        /// <summary>The path's fixed end point (not the live player!) - a soft glow behind a plain round icon.
        /// Deliberately NOT the UFO sprite (that used to make this static endpoint look exactly like the live
        /// player marker below it, e.g. "why is the UFO icon parked up at the goal" - see BuildUfoMarker, which
        /// now owns that sprite instead).</summary>
        private static RectTransform BuildGoalMarker(Transform parent, Sprite dotSprite, Color iconColor)
        {
            var markerObject = new GameObject("GoalMarker", typeof(RectTransform));
            markerObject.transform.SetParent(parent, false);
            var rect = (RectTransform)markerObject.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(38f, 38f);

            var glow = new GameObject("Glow", typeof(RectTransform), typeof(Image));
            glow.transform.SetParent(markerObject.transform, false);
            var glowRect = (RectTransform)glow.transform;
            glowRect.anchorMin = Vector2.zero;
            glowRect.anchorMax = Vector2.one;
            glowRect.offsetMin = new Vector2(-6f, -6f);
            glowRect.offsetMax = new Vector2(6f, 6f);
            var glowImage = glow.GetComponent<Image>();
            glowImage.sprite = dotSprite;
            glowImage.color = new Color(GoalGlowColor.r, GoalGlowColor.g, GoalGlowColor.b, 0.45f);

            var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            icon.transform.SetParent(markerObject.transform, false);
            var iconRect = (RectTransform)icon.transform;
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = new Vector2(9f, 9f);
            iconRect.offsetMax = new Vector2(-9f, -9f);
            var iconImage = icon.GetComponent<Image>();
            iconImage.sprite = dotSprite;
            iconImage.color = iconColor;

            markerObject.SetActive(false); // shown by MinimapController.InitializePath once real path points exist
            return rect;
        }

        /// <summary>The live player UFO's marker (see MinimapPresenter.Update -> MinimapController.SetUFOPosition,
        /// called every frame). Starts inactive - SetUFOPosition itself activates it on first call - and starts
        /// at the marker's parent's origin; its real anchoredPosition is set every frame, so its build-time
        /// position here doesn't matter. Public (like this whole class - see its own file for why) so an
        /// existing already-built minimap can also get just this one new marker added without a full rebuild.</summary>
        public static RectTransform BuildUfoMarker(Transform parent, Sprite dotSprite, Sprite ufoSprite)
        {
            var markerObject = new GameObject("UfoMarker", typeof(RectTransform));
            markerObject.transform.SetParent(parent, false);
            var rect = (RectTransform)markerObject.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(30f, 30f);

            var glow = new GameObject("Glow", typeof(RectTransform), typeof(Image));
            glow.transform.SetParent(markerObject.transform, false);
            var glowRect = (RectTransform)glow.transform;
            glowRect.anchorMin = Vector2.zero;
            glowRect.anchorMax = Vector2.one;
            glowRect.offsetMin = new Vector2(-5f, -5f);
            glowRect.offsetMax = new Vector2(5f, 5f);
            var glowImage = glow.GetComponent<Image>();
            glowImage.sprite = dotSprite;
            glowImage.color = new Color(1f, 1f, 1f, 0.4f);

            var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            icon.transform.SetParent(markerObject.transform, false);
            var iconRect = (RectTransform)icon.transform;
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
            var iconImage = icon.GetComponent<Image>();
            iconImage.sprite = ufoSprite;
            iconImage.color = Color.white;
            iconImage.preserveAspect = true;

            markerObject.SetActive(false); // activated by MinimapController.SetUFOPosition's first call
            return rect;
        }

        private static Color HexColor(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out Color c) ? c : Color.white;
        }
    }
}
