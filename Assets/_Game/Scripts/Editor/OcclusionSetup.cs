using System.Collections.Generic;
using AlienDefense.Building;
using AlienDefense.CameraSystem;
using AlienDefense.Enemies;
using AlienDefense.Occlusion;
using AlienDefense.Player;
using AlienDefense.Towers;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AlienDefense.EditorTools
{
    /// <summary>Editor setup for the "UFO hidden behind scenery" fade. The scenery is never modified - only the UFO
    /// fades. Idempotent.
    ///
    /// - UFO_Player prefab: UFOOcclusionFade (visual root = "ufo") and an OcclusionTarget child at the middle of the
    ///   UFO's visible body.
    /// - Level 02 kit prefabs (Assets/_Game/Prefabs/Level02): the OcclusionObject MARKER goes on the PREFAB root of
    ///   every building, tree and rock at least MinHeight tall (Level02Builder.BuildKit re-applies it).
    /// - Open level scene: the controller on the gameplay camera (TopDownCameraController._cameraTransform) wired to the
    ///   shared OcclusionSettings, the OcclusionTarget and the UFO's UFOOcclusionFade, plus the marker on the root of
    ///   every other tall scenery object (third-party pack instances in Level_01 - the pack prefabs themselves are never
    ///   edited). Markers never touch their object's renderers. Small props, ground, roads, water, VFX and anything
    ///   belonging to the player, enemies, towers, build nodes or the base get no marker.</summary>
    public static class OcclusionSetup
    {
        private const string SettingsFolder = "Assets/_Game/Settings/Occlusion";
        private const string SettingsPath = SettingsFolder + "/OcclusionSettings.asset";
        private const string ShaderName = "AlienDefense/UFOOcclusionFade";
        private const string UfoPrefabPath = "Assets/_Game/Prefabs/Player/UFO_Player.prefab";
        private const string KitRoot = "Assets/_Game/Prefabs/Level02";
        private const float MinHeight = 2.2f;     // a UFO hovers ~2.5-3.5 m up: anything lower cannot hide it
        private const float MaxFootprint = 30f;   // terrain chunks, cliffs and whole-map meshes are not "objects"

        private static readonly string[] ExcludedLayers =
        {
            "Ignore Raycast", "TransparentFX", "Water", "UI", "Ground", "Player", "Enemy", "Tower", "BuildNode",
            "Projectile", "MainMenuPreview", "BaseWorld",
        };

        private static readonly string[] ExcludedNames = { "PlayerBase", "BaseTarget", "UFOLanding", "LevelBounds", "Gameplay" };

        [MenuItem("Tools/Tower Defense/Occlusion/Setup Open Level Scene")]
        public static void SetupOpenSceneFromMenu()
        {
            Debug.Log(SetupUfoPrefab());
            Debug.Log(SetupScene(SceneManager.GetActiveScene()));
        }

        [MenuItem("Tools/Tower Defense/Occlusion/Apply To Level 02 Kit Prefabs")]
        public static void ApplyToKitPrefabsFromMenu()
        {
            Debug.Log(ApplyToKitPrefabs());
        }

        // ------------------------------------------------------------------ settings

        public static OcclusionSettings EnsureSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<OcclusionSettings>(SettingsPath);
            if (settings == null)
            {
                if (!AssetDatabase.IsValidFolder(SettingsFolder))
                {
                    AssetDatabase.CreateFolder("Assets/_Game/Settings", "Occlusion");
                }

                settings = ScriptableObject.CreateInstance<OcclusionSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }

            var so = new SerializedObject(settings);
            if (so.FindProperty("_fadeShader").objectReferenceValue == null)
            {
                so.FindProperty("_fadeShader").objectReferenceValue = Shader.Find(ShaderName);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }

            return settings;
        }

        // ------------------------------------------------------------------ UFO prefab

        /// <summary>UFOOcclusionFade + OcclusionTarget on the UFO_Player prefab (every level's player is an instance).</summary>
        public static string SetupUfoPrefab()
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(UfoPrefabPath);
            try
            {
                Transform visualRoot = contents.transform.Find("ufo");
                if (visualRoot == null)
                {
                    return "[OcclusionSetup] UFO_Player has no 'ufo' visual child - nothing done.";
                }

                // The middle of the visible body (not the pivot near the ground).
                Bounds body = default;
                bool any = false;
                foreach (Renderer r in visualRoot.GetComponentsInChildren<Renderer>(false))
                {
                    if (r is ParticleSystemRenderer || !r.enabled || r.name.Contains("Beam"))
                    {
                        continue;
                    }

                    if (!any)
                    {
                        body = r.bounds;
                        any = true;
                    }
                    else
                    {
                        body.Encapsulate(r.bounds);
                    }
                }

                Transform target = contents.transform.Find("OcclusionTarget");
                if (target == null)
                {
                    target = new GameObject("OcclusionTarget").transform;
                    target.SetParent(contents.transform, false);
                }

                target.position = any ? body.center : contents.transform.position + Vector3.up * 2f;

                var fade = contents.GetComponent<UFOOcclusionFade>();
                if (fade == null)
                {
                    fade = contents.AddComponent<UFOOcclusionFade>();
                }

                var so = new SerializedObject(fade);
                so.FindProperty("_visualRoot").objectReferenceValue = visualRoot;
                so.FindProperty("_settings").objectReferenceValue = EnsureSettings();
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(contents, UfoPrefabPath);
                return "[OcclusionSetup] UFO_Player prefab: UFOOcclusionFade (visual root 'ufo'), OcclusionTarget at local " + target.localPosition.ToString("F2") + ".";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        // ------------------------------------------------------------------ Level 02 kit prefabs

        public static string ApplyToKitPrefabs()
        {
            int added = 0, kept = 0, skipped = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { KitRoot + "/Buildings", KitRoot + "/Vegetation", KitRoot + "/Props" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    continue;
                }

                bool isProp = path.Contains("/Props/");
                string name = prefab.name;
                if (isProp && !name.Contains("Rock") && !name.Contains("Boulder") && !name.Contains("Stone"))
                {
                    skipped++;
                    continue; // only rocks among the props
                }

                if (!IsTallEnough(prefab))
                {
                    skipped++;
                    continue;
                }

                GameObject contents = PrefabUtility.LoadPrefabContents(path);
                OcclusionObject marker = contents.GetComponent<OcclusionObject>();
                if (marker == null)
                {
                    marker = contents.AddComponent<OcclusionObject>();
                    added++;
                }
                else
                {
                    kept++;
                }

                marker.BakeLocalBounds(); // the object's own box, measured before any static batching
                PrefabUtility.SaveAsPrefabAsset(contents, path);
                PrefabUtility.UnloadPrefabContents(contents);
            }

            return "[OcclusionSetup] Level 02 kit prefabs: OcclusionObject added to " + added + ", re-baked on " + kept + ", skipped " + skipped + " (small or not a building/tree/rock).";
        }

        private static bool IsTallEnough(GameObject prefab)
        {
            Bounds bounds = default;
            bool any = false;
            foreach (Renderer r in prefab.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer)
                {
                    continue;
                }

                if (!any)
                {
                    bounds = r.bounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }

            return any && bounds.size.y >= MinHeight && Mathf.Max(bounds.size.x, bounds.size.z) <= MaxFootprint;
        }

        // ------------------------------------------------------------------ scene

        public static string SetupScene(Scene scene)
        {
            OcclusionSettings settings = EnsureSettings();
            var log = new System.Text.StringBuilder("[OcclusionSetup] " + scene.name + ":");

            TopDownCameraController cameraController = null;
            PlayerController player = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (cameraController == null)
                {
                    cameraController = root.GetComponentInChildren<TopDownCameraController>(true);
                }

                if (player == null)
                {
                    player = root.GetComponentInChildren<PlayerController>(true);
                }
            }

            if (cameraController == null || player == null)
            {
                return log + " no TopDownCameraController / PlayerController - nothing done.";
            }

            // The UFO's fade + OcclusionTarget come from the UFO_Player prefab (SetupUfoPrefab). Drop the scene-only
            // target an earlier version of this tool put under the visual.
            foreach (Transform t in player.GetComponentsInChildren<Transform>(true))
            {
                if (t != null && t.name == "OcclusionTarget" && t.parent != player.transform && PrefabUtility.IsAddedGameObjectOverride(t.gameObject))
                {
                    Object.DestroyImmediate(t.gameObject);
                    log.Append(" removed old scene OcclusionTarget;");
                }
            }

            Transform target = player.transform.Find("OcclusionTarget");
            var fade = player.GetComponent<UFOOcclusionFade>();
            if (target == null || fade == null)
            {
                return log + " the UFO_Player instance has no OcclusionTarget / UFOOcclusionFade - run SetupUfoPrefab first.";
            }

            log.Append(" target " + target.name + " at local " + target.localPosition.ToString("F2") + ";");

            var cameraTransform = (Transform)new SerializedObject(cameraController).FindProperty("_cameraTransform").objectReferenceValue;
            if (cameraTransform == null)
            {
                cameraTransform = cameraController.transform;
            }

            var controller = cameraTransform.GetComponent<PlayerOcclusionController>();
            if (controller == null)
            {
                controller = cameraTransform.gameObject.AddComponent<PlayerOcclusionController>();
            }

            var controllerSo = new SerializedObject(controller);
            controllerSo.FindProperty("_settings").objectReferenceValue = settings;
            controllerSo.FindProperty("_target").objectReferenceValue = target;
            controllerSo.FindProperty("_ufoFade").objectReferenceValue = fade;
            controllerSo.FindProperty("_camera").objectReferenceValue = cameraTransform.GetComponent<Camera>();
            controllerSo.ApplyModifiedPropertiesWithoutUndo();
            log.Append(" controller on " + cameraTransform.name + ";");

            int added = AddSceneOccluders(scene, out int fromPrefabs);
            log.Append(" OcclusionObject added to " + added + " scene objects (" + fromPrefabs + " more already get it from their prefab).");
            EditorSceneManager.MarkSceneDirty(scene);
            return log.ToString();
        }

        private static int AddSceneOccluders(Scene scene, out int fromPrefabs)
        {
            var excludedLayerIds = new HashSet<int>();
            foreach (string layer in ExcludedLayers)
            {
                int id = LayerMask.NameToLayer(layer);
                if (id >= 0)
                {
                    excludedLayerIds.Add(id);
                }
            }

            // Re-running after a rule change: drop scene-added markers whose object no longer qualifies (markers that
            // come from a prefab asset are managed by ApplyToKitPrefabs, not here).
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (OcclusionObject marker in root.GetComponentsInChildren<OcclusionObject>(true))
                {
                    if (PrefabUtility.IsPartOfPrefabInstance(marker) && !PrefabUtility.IsAddedComponentOverride(marker))
                    {
                        // A kit tree / rock / umbrella carries its marker in the prefab: take it off this instance only.
                        if (IsSoftScenery(marker.transform))
                        {
                            Object.DestroyImmediate(marker);
                        }

                        continue;
                    }

                    bool qualifies = false;
                    foreach (Renderer r in marker.GetComponentsInChildren<Renderer>(true))
                    {
                        if ((r is MeshRenderer || r is SkinnedMeshRenderer) && !excludedLayerIds.Contains(r.gameObject.layer)
                            && r.bounds.size.y >= MinHeight && !IsExcluded(r))
                        {
                            qualifies = true;
                            break;
                        }
                    }

                    if (!qualifies)
                    {
                        Object.DestroyImmediate(marker);
                    }
                }
            }

            var owners = new HashSet<GameObject>();
            fromPrefabs = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (!(r is MeshRenderer || r is SkinnedMeshRenderer) || excludedLayerIds.Contains(r.gameObject.layer))
                    {
                        continue;
                    }

                    Bounds b = r.bounds;
                    if (b.size.y < MinHeight || Mathf.Max(b.size.x, b.size.z) > MaxFootprint || IsExcluded(r))
                    {
                        continue;
                    }

                    GameObject owner = ResolveOwner(r);
                    if (owner == null || owner.GetComponentInParent<OcclusionObject>(true) != null)
                    {
                        if (owner != null && PrefabUtility.IsPartOfPrefabInstance(owner))
                        {
                            fromPrefabs++;
                        }

                        continue;
                    }

                    owners.Add(owner);
                }
            }

            foreach (GameObject owner in owners)
            {
                owner.AddComponent<OcclusionObject>();
            }

            // Bake every scene-added marker's box (kit prefab instances inherit theirs from the prefab).
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (OcclusionObject marker in root.GetComponentsInChildren<OcclusionObject>(true))
                {
                    if (!PrefabUtility.IsPartOfPrefabInstance(marker) || PrefabUtility.IsAddedComponentOverride(marker))
                    {
                        marker.BakeLocalBounds();
                        EditorUtility.SetDirty(marker);
                    }
                }
            }

            return owners.Count;
        }

        private static readonly string[] SoftSceneryNames = { "Tree", "Palm", "Cactus", "Bush", "Shrub", "Rock", "Stone", "Boulder", "Umbrella",
            "buoy", "Scarecrow", "Lifeguard", "boat-sail", "boat-fishing", "boat-tug", "boat-row", "boat-speed", "ship-", "MarketStall", "Zombie",
        };

        /// <summary>Trees, palms, bushes, rocks, parasols, boats and other small or thin things never hide the UFO as far as the game is concerned: flying
        /// behind them must not fade it or switch the beam off. Only buildings and other big structures do.</summary>
        private static bool IsSoftScenery(Transform transform)
        {
            for (Transform t = transform; t != null; t = t.parent)
            {
                foreach (string name in SoftSceneryNames)
                {
                    if (t.name.IndexOf(name, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsExcluded(Renderer r)
        {
            if (!r.enabled
                || r.GetComponentInParent<PlayerController>(true) != null || r.GetComponentInParent<EnemyController>(true) != null
                || r.GetComponentInParent<TowerController>(true) != null || r.GetComponentInParent<BuildNode>(true) != null
                || r.GetComponentInParent<Canvas>(true) != null || r.GetComponentInParent<Camera>(true) != null
                || r.GetComponentInParent<Animator>(true) != null) // animals and other animated actors, not scenery
            {
                return true;
            }

            if (IsSoftScenery(r.transform))
            {
                return true;
            }

            for (Transform t = r.transform; t != null; t = t.parent)
            {
                foreach (string name in ExcludedNames)
                {
                    if (t.name == name)
                    {
                        return true;
                    }
                }

                // Vehicles, and hidden collider proxies that happen to carry a renderer.
                if (t.name == "Vehicles" || t.name.StartsWith("PF_Vehicle") || t.name.StartsWith("PF_FireTruck")
                    || t.name.IndexOf("collider", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            foreach (Material m in r.sharedMaterials)
            {
                string shader = m != null && m.shader != null ? m.shader.name : string.Empty;
                if (shader.Contains("Water") || shader.Contains("Waterfall") || shader.Contains("Particles") || shader.Contains("Skybox"))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The object a renderer belongs to: its LODGroup, else its nearest prefab instance root, else itself -
        /// whichever is the smallest of those that is still one object (not a whole dressing group).</summary>
        private static GameObject ResolveOwner(Renderer r)
        {
            LODGroup lod = r.GetComponentInParent<LODGroup>(true);
            if (lod != null && FootprintOf(lod.gameObject) <= MaxFootprint)
            {
                return lod.gameObject;
            }

            GameObject prefabRoot = PrefabUtility.GetNearestPrefabInstanceRoot(r.gameObject);
            if (prefabRoot != null && FootprintOf(prefabRoot) <= MaxFootprint)
            {
                return prefabRoot;
            }

            return r.gameObject;
        }

        private static float FootprintOf(GameObject go)
        {
            Bounds bounds = default;
            bool any = false;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (!any)
                {
                    bounds = r.bounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }

            return any ? Mathf.Max(bounds.size.x, bounds.size.z) : 0f;
        }
    }
}
