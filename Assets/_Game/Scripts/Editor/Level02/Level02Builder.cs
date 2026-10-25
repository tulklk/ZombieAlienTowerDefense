using System.Collections.Generic;
using AlienDefense.Common;
using AlienDefense.Core;
using AlienDefense.Enemies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace AlienDefense.EditorTools
{
    /// <summary>Builds Level_02 - a farm village map - as its own scene.
    ///
    /// Safety: Level_01 is never opened for writing. The first run copies Level_01.unity to Level_02.unity (so every
    /// gameplay system, UI and serialized reference comes along untouched), then replaces only the copy's map: the
    /// old "Maps" root is removed after its gameplay anchors (LevelBounds, EnemyPath, BaseTarget, PlayerBase, UFO
    /// landing pad, light, volume) are moved into the new "Level_02" hierarchy and re-positioned. Re-running rebuilds
    /// the generated environment in place and keeps the gameplay objects. Prefab and material sources are never
    /// modified - Level_02 variants live under Assets/_Game/Prefabs/Level02 and Assets/_Game/Art/Level02.
    ///
    /// The layout is fixed (Level02Layout.cs); randomness is seeded and limited to small rotation/scale/offset
    /// variations, so a rebuild reproduces the same level.</summary>
    public static partial class Level02Builder
    {
        private const string Level01Path = "Assets/_Game/Scenes/Levels/Level_01.unity";
        private const string Level02Path = "Assets/_Game/Scenes/Levels/Level_02.unity";
        private const string Level02SceneName = "Level_02";
        private const string LevelCatalogPath = "Assets/_Game/Data/Levels/LevelCatalog.asset";
        private const string Level02DefinitionPath = "Assets/_Game/Data/Levels/Level_02_Definition.asset";
        private const string Level01VolumePath = "Assets/_Game/Settings/Volumes/PP_Level01.asset";
        private const string Level02VolumePath = "Assets/_Game/Settings/Volumes/PP_Level02.asset";
        private const string RootName = "Level_02";
        private const int Seed = 20260930;

        private static System.Random _rng;
        private static readonly List<string> Report = new List<string>();

        static partial void BuildKit();
        static partial void DressLevel(Transform root, Terrain terrain, List<LedgeSpot> ledges);

        [MenuItem("Tools/Tower Defense/Level 02/Build Level 02 (full)")]
        public static void BuildFromMenu()
        {
            Build(true);
        }

        [MenuItem("Tools/Tower Defense/Level 02/Build Level 02 (ground + gameplay only)")]
        public static void BuildGroundOnlyFromMenu()
        {
            Build(false);
        }

        public static string Build(bool dress, bool regenerateTextures = false)
        {
            Report.Clear();
            _rng = new System.Random(Seed);
            _regenerateTextures = regenerateTextures;
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[Level02Builder] Stop Play mode first.");
                return "playing";
            }

            Scene scene = OpenOrCreateScene();
            PrepareRoads();
            BuildDistanceFields();
            BuildYards();

            Transform root = PrepareHierarchy(scene, out Dictionary<string, Anchor> anchors);
            Transform environment = Group(root, "Environment");
            Terrain terrain = BuildTerrain(Group(environment, "Terrain"));
            Material vertexLit = EnsureVertexLitMaterial();
            List<LedgeSpot> ledges = BuildCliffs(environment, vertexLit);

            PlaceGameplay(root, anchors, terrain);
            SetupLighting(root);

            if (dress)
            {
                BuildKit();
                DressLevel(root, terrain, ledges);
                ValidateLayout(root);
            }

            Group(root, "Optimization");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            RegisterScene();
            AssetDatabase.SaveAssets();

            string summary = "[Level02Builder] Done.\n" + string.Join("\n", Report);
            Debug.Log(summary);
            return summary;
        }

        // ------------------------------------------------------------------ scene

        private static Scene OpenOrCreateScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Level02Path) == null)
            {
                Scene active = SceneManager.GetActiveScene();
                if (active.path == Level01Path && active.isDirty)
                {
                    EditorSceneManager.SaveScene(active);
                }

                AssetDatabase.CopyAsset(Level01Path, Level02Path);
                Report.Add("Created " + Level02Path + " as a copy of Level_01.");
            }

            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != Level02Path)
            {
                if (scene.isDirty)
                {
                    EditorSceneManager.SaveScene(scene);
                }

                scene = EditorSceneManager.OpenScene(Level02Path, OpenSceneMode.Single);
            }

            return scene;
        }

        /// <summary>A gameplay object carried over from the Level_01 copy, with its height above the old ground so
        /// it can be re-seated on the new terrain at the same relative height.</summary>
        private struct Anchor
        {
            public Transform Transform;
            public float HeightAboveGround;
        }

        private static Transform PrepareHierarchy(Scene scene, out Dictionary<string, Anchor> anchorsOut)
        {
            var anchors = new Dictionary<string, Anchor>();
            anchorsOut = anchors;
            GameObject maps = FindRoot(scene, "Maps");
            GameObject root = FindRoot(scene, RootName);

            if (root == null)
            {
                root = new GameObject(RootName);
                SceneManager.MoveGameObjectToScene(root, scene);
            }

            Transform gameplay = Group(root.transform, "Gameplay");
            Transform lighting = Group(root.transform, "Lighting");

            if (maps != null)
            {
                // First build: rescue the gameplay anchors out of the copied Level_01 map, then drop the map.
                Terrain[] oldTerrains = maps.GetComponentsInChildren<Terrain>(true);
                float OldGround(Vector3 p)
                {
                    foreach (Terrain t in oldTerrains)
                    {
                        Vector3 o = t.transform.position;
                        Vector3 s = t.terrainData.size;
                        if (p.x >= o.x && p.x <= o.x + s.x && p.z >= o.z && p.z <= o.z + s.z)
                        {
                            return o.y + t.SampleHeight(p);
                        }
                    }

                    return p.y;
                }

                void Rescue(string path, Transform newParent, string key)
                {
                    Transform t = maps.transform.Find(path);
                    if (t == null)
                    {
                        Report.Add("WARNING: Level_01 copy has no " + path);
                        return;
                    }

                    anchors[key] = new Anchor { Transform = t, HeightAboveGround = t.position.y - OldGround(t.position) };
                    t.SetParent(newParent, true);
                }

                Transform objectives = Group(gameplay, "Objectives");
                Transform spawners = Group(gameplay, "EnemySpawners");
                Rescue("ZombieRoad/LevelBounds", gameplay, "LevelBounds");
                Rescue("ZombieRoad/EnemyPath", spawners, "EnemyPath");
                Rescue("ZombieRoad/BaseTarget", objectives, "BaseTarget");
                Rescue("ZombieRoad/PlayerBase", objectives, "PlayerBase");
                Rescue("UFOLanding", objectives, "UFOLanding");
                Rescue("ZombieRoad/TowerSpawnPoint_Debug", objectives, "TowerSpawnPoint_Debug");
                Rescue("ZombieRoad/Directional Light", lighting, "Light");
                Rescue("Environment/Lighting/GlobalVolume_Level01", lighting, "Volume");
                Rescue("Environment/Lighting/LevelLightingIntro", lighting, "LightingIntro");

                GameObject player = FindRoot(scene, "Player");
                if (player != null)
                {
                    anchors["Player"] = new Anchor { Transform = player.transform, HeightAboveGround = player.transform.position.y - OldGround(player.transform.position) };
                }

                GameObject buildNodes = FindRoot(scene, "BuildNodes");
                if (buildNodes != null)
                {
                    Transform first = buildNodes.transform.childCount > 0 ? buildNodes.transform.GetChild(0) : null;
                    anchors["BuildNodes"] = new Anchor
                    {
                        Transform = buildNodes.transform,
                        HeightAboveGround = first != null ? first.position.y - OldGround(first.position) : 0.3f,
                    };
                }

                GameObject levelAnchors = FindRoot(scene, "LevelAnchors");
                if (levelAnchors != null)
                {
                    Transform sky = levelAnchors.transform.Find("UFO_SkyStartPoint");
                    anchors["SkyStart"] = new Anchor { Transform = sky, HeightAboveGround = sky != null ? sky.position.y - OldGround(sky.position) : 1.5f };
                }

                Object.DestroyImmediate(maps);
                Report.Add("Removed the copied Level_01 map (Maps root, " + oldTerrains.Length + " terrains).");
            }
            else
            {
                // Rebuild: anchors already live in the Level_02 hierarchy; heights are kept as authored.
                AddExisting(anchors, gameplay, "LevelBounds", "LevelBounds");
                AddExisting(anchors, gameplay, "EnemySpawners/EnemyPath", "EnemyPath");
                AddExisting(anchors, gameplay, "Objectives/BaseTarget", "BaseTarget");
                AddExisting(anchors, gameplay, "Objectives/PlayerBase", "PlayerBase");
                AddExisting(anchors, gameplay, "Objectives/UFOLanding", "UFOLanding");
                AddExisting(anchors, gameplay, "Objectives/TowerSpawnPoint_Debug", "TowerSpawnPoint_Debug");
                AddExisting(anchors, lighting, "Directional Light", "Light");
                Transform existingNodes = gameplay.Find("BuildNodes");
                if (existingNodes == null)
                {
                    existingNodes = gameplay.Find("BuildPoints/BuildNodes"); // layout before the route rework
                }

                if (existingNodes != null && existingNodes.childCount > 0)
                {
                    Transform first = existingNodes.GetChild(0);
                    float ground = Level02PreviewTools.GroundHeight(new Vector2(first.position.x, first.position.z));
                    anchors["BuildNodes"] = new Anchor { Transform = existingNodes, HeightAboveGround = first.position.y - ground };
                }

                AddExisting(anchors, gameplay, "PlayerSpawn_Level02/LevelAnchors/UFO_SkyStartPoint", "SkyStart");
                if (!anchors.ContainsKey("SkyStart"))
                {
                    AddExisting(anchors, gameplay, "PlayerSpawn/LevelAnchors/UFO_SkyStartPoint", "SkyStart");
                }
                GameObject player = FindRoot(scene, "Player");
                if (player != null)
                {
                    anchors["Player"] = new Anchor { Transform = player.transform, HeightAboveGround = _playerHover };
                }
            }

            foreach (string generated in new[] { "Environment", "Buildings", "Vegetation", "FarmProps", "Animals", "Vehicles", "Optimization" })
            {
                Transform old = root.transform.Find(generated);
                if (old != null)
                {
                    Object.DestroyImmediate(old.gameObject);
                }
            }

            foreach (string generated in new[] { "PlayerSpawn/Markers", "PlayerSpawn_Level02/Markers", "EnemySpawners/Markers", "BossArea", "EndArea_Level02", "Objectives/Markers" })
            {
                Transform old = gameplay.Find(generated);
                if (old != null)
                {
                    Object.DestroyImmediate(old.gameObject);
                }
            }

            // Hierarchy order: Environment, Buildings, Vegetation, FarmProps, Animals, Vehicles, Gameplay, Lighting.
            Group(root.transform, "Environment").SetSiblingIndex(0);
            return root.transform;
        }

        private static float _playerHover = 1.5f;

        private static void AddExisting(Dictionary<string, Anchor> anchors, Transform parent, string path, string key)
        {
            Transform t = parent.Find(path);
            if (t == null)
            {
                return;
            }

            float ground = Level02PreviewTools.GroundHeight(new Vector2(t.position.x, t.position.z));
            anchors[key] = new Anchor { Transform = t, HeightAboveGround = t.position.y - ground };
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                if (go.name == name)
                {
                    return go;
                }
            }

            return null;
        }

        private static Transform Group(Transform parent, string name)
        {
            Transform t = parent.Find(name);
            if (t == null)
            {
                t = new GameObject(name).transform;
                t.SetParent(parent, false);
            }

            return t;
        }

        // ------------------------------------------------------------------ gameplay

        private static float Ground(Terrain terrain, Vector2 xz) => terrain.transform.position.y + terrain.SampleHeight(new Vector3(xz.x, 0f, xz.y));

        private static void PlaceGameplay(Transform root, Dictionary<string, Anchor> anchors, Terrain terrain)
        {
            Transform gameplay = Group(root, "Gameplay");
            Transform objectives = Group(gameplay, "Objectives");
            Transform spawners = Group(gameplay, "EnemySpawners");

            void Seat(string key, Vector2 xz, float? yaw = null)
            {
                if (!anchors.TryGetValue(key, out Anchor a) || a.Transform == null)
                {
                    Report.Add("WARNING: missing anchor " + key);
                    return;
                }

                a.Transform.position = new Vector3(xz.x, Ground(terrain, xz) + a.HeightAboveGround, xz.y);
                if (yaw.HasValue)
                {
                    Vector3 e = a.Transform.eulerAngles;
                    a.Transform.rotation = Quaternion.Euler(e.x, yaw.Value, e.z);
                }
            }

            // Level bounds.
            if (anchors.TryGetValue("LevelBounds", out Anchor boundsAnchor))
            {
                var so = new SerializedObject(boundsAnchor.Transform.GetComponent<LevelBounds>());
                so.FindProperty("_center").vector2Value = new Vector2((MinX + MaxX) * 0.5f, (MinZ + MaxZ) * 0.5f);
                so.FindProperty("_extents").vector2Value = new Vector2((MaxX - MinX) * 0.5f, (MaxZ - MinZ) * 0.5f);
                so.ApplyModifiedPropertiesWithoutUndo();
                boundsAnchor.Transform.position = new Vector3(0f, GroundY, 0f);
            }

            // Base, landing pad, player start - Level_01's relative arrangement around the base target.
            Seat("BaseTarget", BaseTargetXZ);
            Seat("PlayerBase", BaseTargetXZ + PlayerBaseOffset);
            Seat("UFOLanding", BaseTargetXZ + LandingOffset);
            Seat("TowerSpawnPoint_Debug", BaseTargetXZ + new Vector2(-4f, -6f));
            Vector2 playerStart = BaseTargetXZ + PlayerStartOffset;
            if (anchors.TryGetValue("Player", out Anchor playerAnchor))
            {
                _playerHover = playerAnchor.HeightAboveGround;
                Seat("Player", playerStart);
            }

            // PlayerSpawn_Level02 sits on the player start (route START, minimap bottom) and holds Level_01's
            // LevelAnchors (sky start point for the intro) at their world positions.
            Transform playerSpawn = gameplay.Find("PlayerSpawn_Level02");
            if (playerSpawn == null)
            {
                playerSpawn = gameplay.Find("PlayerSpawn");
                if (playerSpawn == null)
                {
                    playerSpawn = Group(gameplay, "PlayerSpawn_Level02");
                }

                playerSpawn.name = "PlayerSpawn_Level02";
            }

            var childrenOfSpawn = new List<Transform>();
            foreach (Transform child in playerSpawn)
            {
                childrenOfSpawn.Add(child);
                child.SetParent(null, true);
            }

            playerSpawn.position = new Vector3(playerStart.x, Ground(terrain, playerStart), playerStart.y);
            foreach (Transform child in childrenOfSpawn)
            {
                child.SetParent(playerSpawn, true);
            }

            if (anchors.TryGetValue("SkyStart", out Anchor sky) && sky.Transform != null)
            {
                Transform levelAnchors = sky.Transform.parent;
                if (levelAnchors != null && levelAnchors.parent != playerSpawn)
                {
                    levelAnchors.SetParent(playerSpawn, true);
                }

                Seat("SkyStart", playerStart);
            }

            // Enemy path: rebuilt from the smoothed main route - a waypoint every 4 m on straights and whenever the
            // heading has turned 20 degrees, so enemies (who walk straight between waypoints) follow the hairpins.
            if (anchors.TryGetValue("EnemyPath", out Anchor pathAnchor))
            {
                Transform path = pathAnchor.Transform;
                path.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                for (int i = path.childCount - 1; i >= 0; i--)
                {
                    Object.DestroyImmediate(path.GetChild(i).gameObject);
                }

                List<Vector2> dense = _mainRoadSamples;
                var points = new List<Vector2> { dense[0] };
                float accumulated = 0f;
                Vector2 lastHeading = (dense[1] - dense[0]).normalized;
                for (int i = 1; i < dense.Count; i++)
                {
                    accumulated += Vector2.Distance(dense[i - 1], dense[i]);
                    Vector2 heading = i + 1 < dense.Count ? (dense[i + 1] - dense[i]).normalized : lastHeading;
                    bool turned = Vector2.Angle(lastHeading, heading) >= 20f && accumulated >= 1.5f;
                    if (accumulated >= 4f || turned || i == dense.Count - 1)
                    {
                        points.Add(dense[i]);
                        accumulated = 0f;
                        lastHeading = heading;
                    }
                }

                var waypoints = new Transform[points.Count];
                float length = 0f;
                for (int i = 0; i < points.Count; i++)
                {
                    string label = i == 0 ? "_EnemySpawn" : i == points.Count - 1 ? "_Base" : "";
                    var w = new GameObject("WP_" + i.ToString("00") + label).transform;
                    w.SetParent(path, false);
                    w.position = new Vector3(points[i].x, Ground(terrain, points[i]), points[i].y);
                    waypoints[i] = w;
                    if (i > 0)
                    {
                        length += Vector2.Distance(points[i - 1], points[i]);
                    }
                }

                var so = new SerializedObject(path.GetComponent<EnemyPath3D>());
                SerializedProperty array = so.FindProperty("_waypoints");
                array.arraySize = waypoints.Length;
                for (int i = 0; i < waypoints.Length; i++)
                {
                    array.GetArrayElementAtIndex(i).objectReferenceValue = waypoints[i];
                }

                so.FindProperty("_laneHalfWidth").floatValue = EnemyLaneHalfWidth;
                so.ApplyModifiedPropertiesWithoutUndo();
                Report.Add("Enemy path (main route): " + waypoints.Length + " waypoints from " + MainRoad.Length + " spline control points, "
                    + length.ToString("F0") + " m, spawn inside the enemy barn at " + EnemySpawnXZ + ", ends at the base target " + BaseTargetXZ + ".");
            }

            // Build nodes: the eight dots of the minimap reference, beside the route.
            if (anchors.TryGetValue("BuildNodes", out Anchor nodesAnchor))
            {
                PlaceBuildNodes(gameplay, nodesAnchor, terrain);
            }

            // END area (minimap top): the enemy spawn inside the barn and the boss spawn in the barn courtyard.
            // Plain transforms for designers and the report; the real spawn is EnemyPath's first waypoint.
            var endArea = new GameObject("EndArea_Level02").transform;
            endArea.SetParent(gameplay, false);
            endArea.position = new Vector3(EndAreaXZ.x, Ground(terrain, EndAreaXZ), EndAreaXZ.y);
            Marker(endArea, "EnemySpawn_End", EnemySpawnXZ, terrain);
            Marker(endArea, "BossSpawn_Level02", BossAreaXZ, terrain);
            Transform objectiveMarkers = Group(objectives, "Markers");
            Marker(objectiveMarkers, "DefendBase", BaseTargetXZ, terrain);

            if (anchors.TryGetValue("LevelBounds", out Anchor levelBounds))
            {
                ConfigureMinimap(levelBounds.Transform.GetComponent<LevelBounds>());
            }
        }

        private static void Marker(Transform parent, string name, Vector2 xz, Terrain terrain)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(xz.x, Ground(terrain, xz), xz.y);
        }

        // ------------------------------------------------------------------ lighting

        private static void SetupLighting(Transform root)
        {
            Transform lighting = Group(root, "Lighting");
            Transform lightTransform = lighting.Find("Directional Light");
            if (lightTransform != null)
            {
                var light = lightTransform.GetComponent<Light>();
                light.color = new Color(1f, 0.955f, 0.87f);
                light.intensity = 1.12f;
                light.shadows = LightShadows.Soft;
                light.shadowStrength = 0.5f;
                lightTransform.rotation = Quaternion.Euler(52f, 318f, 0f);
                RenderSettings.sun = light;
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.7f, 0.8f, 0.92f);
            RenderSettings.ambientEquatorColor = new Color(0.56f, 0.63f, 0.6f);
            RenderSettings.ambientGroundColor = new Color(0.36f, 0.36f, 0.3f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.82f, 0.89f, 0.95f);
            RenderSettings.fogStartDistance = 55f;
            RenderSettings.fogEndDistance = 160f;

            // Post-processing: Level_01's profile duplicated, then only this copy is edited.
            Transform volumeTransform = lighting.Find("GlobalVolume_Level01") ?? lighting.Find("GlobalVolume_Level02");
            if (volumeTransform != null)
            {
                volumeTransform.name = "GlobalVolume_Level02";
                var volume = volumeTransform.GetComponent<Volume>();
                if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(Level02VolumePath) == null)
                {
                    AssetDatabase.CopyAsset(Level01VolumePath, Level02VolumePath);
                }

                var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(Level02VolumePath);
                volume.sharedProfile = profile;
                TuneVolume(profile);
            }
        }

        private static void TuneVolume(VolumeProfile profile)
        {
            if (profile.TryGet(out UnityEngine.Rendering.Universal.Bloom bloom))
            {
                // Only genuinely bright things (VFX, the beam) bloom - never grass or walls.
                bloom.threshold.Override(1.15f);
                bloom.intensity.Override(0.35f);
            }

            if (profile.TryGet(out UnityEngine.Rendering.Universal.ColorAdjustments color))
            {
                color.saturation.Override(14f);
                color.contrast.Override(8f);
            }

            EditorUtility.SetDirty(profile);
        }

        // ------------------------------------------------------------------ registration

        private static void RegisterScene()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == Level02Path))
            {
                int index = scenes.FindIndex(s => s.path == Level01Path);
                scenes.Insert(index >= 0 ? index + 1 : scenes.Count, new EditorBuildSettingsScene(Level02Path, true));
                EditorBuildSettings.scenes = scenes.ToArray();
                Report.Add("Added Level_02 to Build Settings.");
            }

            // Point the catalog entry that uses Level_02_Definition at the new scene (it used to reuse Level_01).
            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(LevelCatalogPath);
            var definition = AssetDatabase.LoadAssetAtPath<ScriptableObject>(Level02DefinitionPath);
            if (catalog != null && definition != null)
            {
                var so = new SerializedObject(catalog);
                SerializedProperty entries = so.FindProperty("_entries");
                for (int i = 0; i < entries.arraySize; i++)
                {
                    SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                    if (entry.FindPropertyRelative("_levelDefinition").objectReferenceValue == definition)
                    {
                        SerializedProperty sceneName = entry.FindPropertyRelative("_sceneName");
                        if (sceneName.stringValue != Level02SceneName)
                        {
                            sceneName.stringValue = Level02SceneName;
                            Report.Add("LevelCatalog entry " + i + " (Level_02_Definition) now loads scene Level_02.");
                        }
                    }
                }

                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(catalog);
            }
        }

        // ------------------------------------------------------------------ materials

        private static Material EnsureVertexLitMaterial()
        {
            EnsureFolder(MaterialsFolder);
            string path = MaterialsFolder + "/MAT_Level02_VertexLit.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("AlienDefense/Level02/VertexColorLit");
                material = new Material(shader) { name = "MAT_Level02_VertexLit", enableInstancing = true };
                AssetDatabase.CreateAsset(material, path);
            }

            return material;
        }
    }
}
