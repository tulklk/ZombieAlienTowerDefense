using System.IO;
using AlienDefense.CameraSystem;
using AlienDefense.Core;
using AlienDefense.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AlienDefense.EditorTools
{
    /// <summary>Creates Level_01 UFO intro anchors (LaunchPoint / SkyStartPoint) and wires UFOFlightIntro.</summary>
    public static class UFOFlightIntroSetup
    {
        private const string Level01Path = "Assets/_Game/Scenes/Levels/Level_01.unity";
        private const string LandingModelPath =
            "Assets/_Game/Models/UFOLanding/golden+circular+medallion+3d+model/tripo_convert_30bbdae6-c7bf-42b9-a691-55c803251b5d.fbx";
        private const string AutoRunRequestPath = "Assets/_Game/EditorReports/SetupUfoIntro.request";

        // Captured from Level_01 Player pose before intro work (screenshot / scene disk).
        private static readonly Vector3 DefaultSkyPosition = new Vector3(48.94f, 4.67f, 62.9f);
        private static readonly Vector3 DefaultSkyEuler = new Vector3(0f, -85.838f, 0f);
        private static readonly Vector3 DefaultLandingPosition = new Vector3(63.7f, 3.21f, 60.16f);
        private static readonly Vector3 DefaultLandingScale = new Vector3(5f, 5f, 5f);

        [InitializeOnLoadMethod]
        private static void AutoRunIfRequested()
        {
            if (!File.Exists(AutoRunRequestPath))
            {
                return;
            }

            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(AutoRunRequestPath))
                {
                    return;
                }

                try
                {
                    File.Delete(AutoRunRequestPath);
                    string meta = AutoRunRequestPath + ".meta";
                    if (File.Exists(meta))
                    {
                        File.Delete(meta);
                    }
                }
                catch
                {
                    // ignore
                }

                SetupLevel01();
            };
        }

        [MenuItem("AlienDefense/Level Tools/Setup UFO Intro Anchors (Level_01)")]
        public static void SetupLevel01()
        {
            Scene scene = EditorSceneManager.OpenScene(Level01Path, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError("[UFOFlightIntroSetup] Failed to open Level_01.");
                return;
            }

            Transform playerRoot = FindRootTransform("Player");
            PlayerController playerController = Object.FindFirstObjectByType<PlayerController>();
            if (playerController == null)
            {
                Debug.LogError("[UFOFlightIntroSetup] No PlayerController in scene.");
                return;
            }

            Transform ufoTransform = playerController.transform;
            CharacterController characterController = ufoTransform.GetComponent<CharacterController>();
            UFOTractorBeamController beam = ufoTransform.GetComponent<UFOTractorBeamController>();
            TopDownCameraController camera = Object.FindFirstObjectByType<TopDownCameraController>();

            // Capture sky from current Player root (or UFO) before we change anything.
            Vector3 skyPos = playerRoot != null ? playerRoot.position : ufoTransform.position;
            Quaternion skyRot = playerRoot != null ? playerRoot.rotation : ufoTransform.rotation;
            if (skyPos.sqrMagnitude < 0.01f)
            {
                skyPos = DefaultSkyPosition;
                skyRot = Quaternion.Euler(DefaultSkyEuler);
            }

            Transform maps = FindRootTransform("Maps");
            if (maps == null)
            {
                GameObject mapsGo = new GameObject("Maps");
                maps = mapsGo.transform;
                Undo.RegisterCreatedObjectUndo(mapsGo, "Create Maps");
            }

            Transform landing = FindInChildren(maps, "UFOLanding");
            if (landing == null)
            {
                landing = FindAnywhere("UFOLanding")?.transform;
            }

            if (landing == null)
            {
                landing = CreateLandingPad(maps);
            }
            else
            {
                EnsureLandingVisual(landing);
            }

            Transform launchPoint = landing.Find("LaunchPoint");
            if (launchPoint == null)
            {
                GameObject launchGo = new GameObject("LaunchPoint");
                Undo.RegisterCreatedObjectUndo(launchGo, "Create LaunchPoint");
                launchPoint = launchGo.transform;
                launchPoint.SetParent(landing, false);
            }

            // Sit slightly above pad center (world Y up).
            launchPoint.position = landing.position + Vector3.up * 0.55f;
            launchPoint.rotation = Quaternion.Euler(0f, skyRot.eulerAngles.y, 0f);

            Transform anchorsRoot = FindRootTransform("LevelAnchors");
            if (anchorsRoot == null)
            {
                GameObject anchorsGo = new GameObject("LevelAnchors");
                Undo.RegisterCreatedObjectUndo(anchorsGo, "Create LevelAnchors");
                anchorsRoot = anchorsGo.transform;
            }

            Transform skyStart = anchorsRoot.Find("UFO_SkyStartPoint");
            if (skyStart == null)
            {
                GameObject skyGo = new GameObject("UFO_SkyStartPoint");
                Undo.RegisterCreatedObjectUndo(skyGo, "Create UFO_SkyStartPoint");
                skyStart = skyGo.transform;
                skyStart.SetParent(anchorsRoot, false);
            }

            skyStart.SetPositionAndRotation(skyPos, skyRot);

            GameObject joystick = GameObject.Find("BottomControls");
            if (joystick == null)
            {
                joystick = GameObject.Find("MovementJoystick");
            }

            LevelCompositionRoot root = Object.FindFirstObjectByType<LevelCompositionRoot>();
            if (root == null)
            {
                Debug.LogError("[UFOFlightIntroSetup] No LevelCompositionRoot in scene.");
                return;
            }

            // Prefer CompositionRoot host (one intro for the level). Remove accidental copies on the UFO.
            UFOFlightIntro[] existingIntros = Object.FindObjectsByType<UFOFlightIntro>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            UFOFlightIntro intro = root.GetComponent<UFOFlightIntro>();
            for (int i = 0; i < existingIntros.Length; i++)
            {
                UFOFlightIntro candidate = existingIntros[i];
                if (candidate == null)
                {
                    continue;
                }

                if (intro == null && candidate.gameObject == root.gameObject)
                {
                    intro = candidate;
                    continue;
                }

                if (candidate.gameObject != root.gameObject)
                {
                    Undo.DestroyObjectImmediate(candidate);
                }
            }

            if (intro == null)
            {
                intro = Undo.AddComponent<UFOFlightIntro>(root.gameObject);
            }

            SerializedObject introSo = new SerializedObject(intro);
            introSo.FindProperty("_ufo").objectReferenceValue = ufoTransform;
            introSo.FindProperty("_launchPoint").objectReferenceValue = launchPoint;
            introSo.FindProperty("_skyStartPoint").objectReferenceValue = skyStart;
            introSo.FindProperty("_playerController").objectReferenceValue = playerController;
            introSo.FindProperty("_tractorBeam").objectReferenceValue = beam;
            introSo.FindProperty("_characterController").objectReferenceValue = characterController;
            introSo.FindProperty("_cameraController").objectReferenceValue = camera;
            if (joystick != null)
            {
                introSo.FindProperty("_joystickRoot").objectReferenceValue = joystick;
            }

            // DOTween Ease: OutCubic=9, InOutSine=4, OutSine=3
            introSo.FindProperty("_liftEase").enumValueIndex = 9;
            introSo.FindProperty("_travelEase").enumValueIndex = 4;
            introSo.FindProperty("_settleEase").enumValueIndex = 3;

            introSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(intro);

            SerializedObject rootSo = new SerializedObject(root);
            rootSo.FindProperty("_ufoFlightIntro").objectReferenceValue = intro;
            rootSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(root);

            // Keep Player empty root + UFO aligned to sky capture for editor preview;
            // runtime intro will teleport to LaunchPoint in Awake.
            if (playerRoot != null)
            {
                Undo.RecordObject(playerRoot, "Align Player root to sky start");
                playerRoot.SetPositionAndRotation(skyPos, skyRot);
            }

            Undo.RecordObject(ufoTransform, "Align UFO to sky start");
            // Prefer local identity under Player root when parented.
            if (playerRoot != null && ufoTransform.parent == playerRoot)
            {
                ufoTransform.localPosition = Vector3.zero;
                ufoTransform.localRotation = Quaternion.identity;
            }
            else
            {
                ufoTransform.SetPositionAndRotation(skyPos, skyRot);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            string reportDir = "Assets/_Game/EditorReports";
            if (!Directory.Exists(reportDir))
            {
                Directory.CreateDirectory(reportDir);
            }

            bool hasLandingMesh = landing != null &&
                                  landing.GetComponentInChildren<MeshRenderer>(true) != null;
            File.WriteAllText(
                Path.Combine(reportDir, "SetupUfoIntro.done"),
                "ok\nsky=" + skyPos + "\nhasLandingMesh=" + hasLandingMesh + "\n");

            Debug.Log(
                "[UFOFlightIntroSetup] Level_01 ready: UFOLanding/LaunchPoint, LevelAnchors/UFO_SkyStartPoint, " +
                "UFOFlightIntro wired. Sky captured at " + skyPos + ". LandingMesh=" + hasLandingMesh);
        }

        [MenuItem("AlienDefense/Level Tools/Capture Player As Sky Start")]
        public static void CapturePlayerAsSkyStart()
        {
            PlayerController player = Object.FindFirstObjectByType<PlayerController>();
            if (player == null)
            {
                Debug.LogError("[UFOFlightIntroSetup] No PlayerController in active scene.");
                return;
            }

            Transform anchorsRoot = FindRootTransform("LevelAnchors");
            if (anchorsRoot == null)
            {
                GameObject anchorsGo = new GameObject("LevelAnchors");
                Undo.RegisterCreatedObjectUndo(anchorsGo, "Create LevelAnchors");
                anchorsRoot = anchorsGo.transform;
            }

            Transform skyStart = anchorsRoot.Find("UFO_SkyStartPoint");
            if (skyStart == null)
            {
                GameObject skyGo = new GameObject("UFO_SkyStartPoint");
                Undo.RegisterCreatedObjectUndo(skyGo, "Create UFO_SkyStartPoint");
                skyStart = skyGo.transform;
                skyStart.SetParent(anchorsRoot, false);
            }

            Transform source = FindRootTransform("Player") != null ? FindRootTransform("Player") : player.transform;
            Undo.RecordObject(skyStart, "Capture Sky Start");
            skyStart.SetPositionAndRotation(source.position, source.rotation);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[UFOFlightIntroSetup] UFO_SkyStartPoint = " + skyStart.position + " / " + skyStart.eulerAngles);
        }

        private static Transform CreateLandingPad(Transform maps)
        {
            GameObject landingGo = new GameObject("UFOLanding");
            Undo.RegisterCreatedObjectUndo(landingGo, "Create UFOLanding");
            landingGo.transform.SetParent(maps, true);
            landingGo.transform.position = DefaultLandingPosition;
            landingGo.transform.rotation = Quaternion.identity;
            landingGo.transform.localScale = DefaultLandingScale;
            EnsureLandingVisual(landingGo.transform);
            return landingGo.transform;
        }

        private static void EnsureLandingVisual(Transform landing)
        {
            if (landing == null)
            {
                return;
            }

            Transform existingMesh = landing.Find("LandingMesh");
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(LandingModelPath);

            // Prefer the authored landing FBX over a placeholder cylinder.
            if (model != null)
            {
                bool hasModelInstance = false;
                MeshFilter[] filters = landing.GetComponentsInChildren<MeshFilter>(true);
                for (int i = 0; i < filters.Length; i++)
                {
                    if (filters[i] != null && PrefabUtility.GetCorrespondingObjectFromSource(filters[i].gameObject) != null)
                    {
                        hasModelInstance = true;
                        break;
                    }
                }

                if (!hasModelInstance)
                {
                    if (existingMesh != null)
                    {
                        Undo.DestroyObjectImmediate(existingMesh.gameObject);
                    }

                    GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
                    visual.name = "LandingMesh";
                    Undo.RegisterCreatedObjectUndo(visual, "Create LandingMesh");
                    visual.transform.SetParent(landing, false);
                    visual.transform.localPosition = Vector3.zero;
                    visual.transform.localRotation = Quaternion.identity;
                    visual.transform.localScale = Vector3.one;
                }

                return;
            }

            if (landing.GetComponentInChildren<MeshRenderer>(true) != null)
            {
                return;
            }

            GameObject placeholder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            placeholder.name = "LandingMesh";
            Object.DestroyImmediate(placeholder.GetComponent<Collider>());
            Undo.RegisterCreatedObjectUndo(placeholder, "Create LandingMesh");
            placeholder.transform.SetParent(landing, false);
            placeholder.transform.localPosition = Vector3.zero;
            placeholder.transform.localRotation = Quaternion.identity;
            placeholder.transform.localScale = new Vector3(1f, 0.08f, 1f);
        }

        private static Transform FindRootTransform(string name)
        {
            Scene scene = SceneManager.GetActiveScene();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == name)
                {
                    return roots[i].transform;
                }
            }

            return null;
        }

        private static Transform FindInChildren(Transform parent, string name)
        {
            if (parent == null)
            {
                return null;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == name)
                {
                    return child;
                }

                Transform nested = FindInChildren(child, name);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private static GameObject FindAnywhere(string name)
        {
            GameObject[] all = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name == name)
                {
                    return all[i];
                }
            }

            return null;
        }
    }
}
