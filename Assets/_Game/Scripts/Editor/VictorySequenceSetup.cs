using System.Text;
using AlienDefense.CameraSystem;
using AlienDefense.Core;
using AlienDefense.Level;
using AlienDefense.Player;
using AlienDefense.UI;
using AlienDefense.UI.MainMenu;
using AlienDefense.Vfx;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AlienDefense.EditorTools
{
    /// <summary>Wires the victory finishing sequence into the two scenes that need it, so none of it has to be
    /// dragged into the Inspector by hand.
    ///
    /// Gameplay: creates the VictoryCinematic object, points it at the camera rig, the player's UFO and the pooled
    /// cartoon explosion, and hands it to LevelCompositionRoot. Most of its scene references are copied straight
    /// off BossIntroController, which is already wired to the same HUD group, joystick and tractor beam - the two
    /// cinematics deliberately lock the same things.
    ///
    /// MainMenu: creates the fly layer above the HUD, the pooled reward icon prefab, and the presenter that reads
    /// ApplicationServices.PendingRewards.
    ///
    /// Both menu items work on the scene that is currently open and say so when it is the wrong one; re-running is
    /// safe, since each pass finds and updates what it created last time.</summary>
    internal static class VictorySequenceSetup
    {
        private const string GameplayScenePath = "Assets/_Game/Scenes/Levels/Level_01.unity";
        private const string MainMenuScenePath = "Assets/_Game/Scenes/Menu/MainMenu.unity";
        private const string ExplosionVfxPath = "Assets/_Game/Data/Vfx/VfxDefinition_Vfx_RocketExplosion.asset";
        private const string EnergyPickupPrefabPath = "Assets/_Game/Prefabs/Pickups/EnergyPickup.prefab";
        private const string RewardCatalogPath = "Assets/_Game/Data/UI/VictoryRewardCatalog.asset";
        private const string IconPrefabPath = "Assets/_Game/Prefabs/UI/Prefab_RewardFlyIcon.prefab";

        [MenuItem("AlienDefense/Setup/Victory/Wire Victory Cinematic (Gameplay Scene)")]
        private static void WireGameplay()
        {
            var report = new StringBuilder("[VictorySequenceSetup] gameplay");
            var root = Object.FindFirstObjectByType<LevelCompositionRoot>(FindObjectsInactive.Include);
            if (root == null)
            {
                Debug.LogWarning($"[VictorySequenceSetup] No LevelCompositionRoot in the open scene - open {GameplayScenePath} first.");
                return;
            }

            var controller = Object.FindFirstObjectByType<VictoryCinematicController>(FindObjectsInactive.Include);
            if (controller == null)
            {
                var go = new GameObject("VictoryCinematic");
                Undo.RegisterCreatedObjectUndo(go, "Victory cinematic");
                go.transform.SetParent(root.transform, false);
                controller = go.AddComponent<VictoryCinematicController>();
                report.AppendLine();
                report.Append("  created VictoryCinematic under " + root.name);
            }

            var intro = Object.FindFirstObjectByType<BossIntroController>(FindObjectsInactive.Include);
            var serialized = new SerializedObject(controller);

            // The boss intro already locks exactly these things; copy rather than re-find them.
            if (intro != null)
            {
                var introSerialized = new SerializedObject(intro);
                CopyReference(introSerialized, serialized, "_cameraController");
                CopyReference(introSerialized, serialized, "_cameraTransform");
                CopyReference(introSerialized, serialized, "_player");
                CopyReference(introSerialized, serialized, "_tractorBeam");
                CopyReference(introSerialized, serialized, "_buildProximity");
                CopyReference(introSerialized, serialized, "_joystickRoot");
                CopyReference(introSerialized, serialized, "_hudCanvasGroup");
                report.AppendLine();
                report.Append("  copied camera/player/HUD references from BossIntroController");
            }

            AssignIfEmpty(serialized, "_cameraController", Object.FindFirstObjectByType<TopDownCameraController>(FindObjectsInactive.Include));
            Camera main = Camera.main;
            AssignIfEmpty(serialized, "_cameraTransform", main != null ? main.transform : null);
            AssignIfEmpty(serialized, "_player", Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include));

            var explosion = AssetDatabase.LoadAssetAtPath<VfxDefinition>(ExplosionVfxPath);
            AssignIfEmpty(serialized, "_explosionVfx", explosion);
            AssignIfEmpty(serialized, "_smallExplosionVfx", explosion);
            AssignIfEmpty(serialized, "_energyBurstPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(EnergyPickupPrefabPath));
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var rootSerialized = new SerializedObject(root);
            SerializedProperty slot = rootSerialized.FindProperty("_victoryCinematic");
            if (slot != null)
            {
                slot.objectReferenceValue = controller;
                rootSerialized.ApplyModifiedPropertiesWithoutUndo();
                report.AppendLine();
                report.Append("  LevelCompositionRoot._victoryCinematic assigned");
            }

            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(root);
            EditorSceneManager.MarkAllScenesDirty();
            EditorSceneManager.SaveOpenScenes();
            Debug.Log(report.ToString());
        }

        [MenuItem("AlienDefense/Setup/Victory/Wire Reward Fly (MainMenu Scene)")]
        private static void WireMainMenu()
        {
            var report = new StringBuilder("[VictorySequenceSetup] main menu");
            var presenterRoot = Object.FindFirstObjectByType<MainMenuPresenter>(FindObjectsInactive.Include);
            if (presenterRoot == null)
            {
                Debug.LogWarning($"[VictorySequenceSetup] No MainMenuPresenter in the open scene - open {MainMenuScenePath} first.");
                return;
            }

            RewardFlyIconView iconPrefab = EnsureIconPrefab();

            var fly = Object.FindFirstObjectByType<MainMenuRewardFlyPresenter>(FindObjectsInactive.Include);
            RectTransform layer = FindOrCreateFlyLayer(presenterRoot, ref report);

            if (fly == null)
            {
                fly = layer.gameObject.AddComponent<MainMenuRewardFlyPresenter>();
                report.AppendLine();
                report.Append("  created MainMenuRewardFlyPresenter on " + layer.name);
            }

            var serialized = new SerializedObject(fly);
            AssignIfEmpty(serialized, "_flyLayer", layer);
            AssignIfEmpty(serialized, "_iconPrefab", iconPrefab);
            AssignIfEmpty(serialized, "_catalog", AssetDatabase.LoadAssetAtPath<VictoryRewardCatalog>(RewardCatalogPath));
            AssignIfEmpty(serialized, "_resourcePresenter", Object.FindFirstObjectByType<MainMenuResourcePresenter>(FindObjectsInactive.Include));

            // The icons burst out over the level preview card, like the reference.
            var preview = Object.FindFirstObjectByType<LevelPreviewView>(FindObjectsInactive.Include);
            AssignIfEmpty(serialized, "_originAnchor", preview != null ? preview.transform as RectTransform : null);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var presenterSerialized = new SerializedObject(presenterRoot);
            SerializedProperty slot = presenterSerialized.FindProperty("_rewardFlyPresenter");
            if (slot != null)
            {
                slot.objectReferenceValue = fly;
                presenterSerialized.ApplyModifiedPropertiesWithoutUndo();
                report.AppendLine();
                report.Append("  MainMenuPresenter._rewardFlyPresenter assigned");
            }

            EditorUtility.SetDirty(fly);
            EditorUtility.SetDirty(presenterRoot);
            EditorSceneManager.MarkAllScenesDirty();
            EditorSceneManager.SaveOpenScenes();
            Debug.Log(report.ToString());
        }

        /// <summary>A full-screen, non-blocking layer as the last child of the menu canvas, so icons draw over the
        /// HUD without ever eating a tap.</summary>
        private static RectTransform FindOrCreateFlyLayer(MainMenuPresenter presenter, ref StringBuilder report)
        {
            var canvas = presenter.GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            }

            Transform existing = canvas != null ? canvas.transform.Find("RewardFlyLayer") : null;
            if (existing is RectTransform found)
            {
                return found;
            }

            var go = new GameObject("RewardFlyLayer", typeof(RectTransform), typeof(CanvasGroup));
            Undo.RegisterCreatedObjectUndo(go, "Reward fly layer");
            var rect = (RectTransform)go.transform;
            rect.SetParent(canvas != null ? canvas.transform : presenter.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.SetAsLastSibling();
            go.GetComponent<CanvasGroup>().blocksRaycasts = false;
            go.GetComponent<CanvasGroup>().interactable = false;

            report.AppendLine();
            report.Append("  created RewardFlyLayer (full screen, raycasts off)");
            return rect;
        }

        private static RewardFlyIconView EnsureIconPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<RewardFlyIconView>(IconPrefabPath);
            if (existing != null)
            {
                return existing;
            }

            var go = new GameObject("Prefab_RewardFlyIcon", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(84f, 84f);
            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;

            var view = go.AddComponent<RewardFlyIconView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("_icon").objectReferenceValue = image;
            serialized.FindProperty("_rectTransform").objectReferenceValue = rect;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            RewardFlyIconView saved = PrefabUtility.SaveAsPrefabAsset(go, IconPrefabPath).GetComponent<RewardFlyIconView>();
            Object.DestroyImmediate(go);
            return saved;
        }

        private static void CopyReference(SerializedObject from, SerializedObject to, string propertyPath)
        {
            SerializedProperty source = from.FindProperty(propertyPath);
            SerializedProperty destination = to.FindProperty(propertyPath);
            if (source != null && destination != null && source.objectReferenceValue != null)
            {
                destination.objectReferenceValue = source.objectReferenceValue;
            }
        }

        private static void AssignIfEmpty(SerializedObject serialized, string propertyPath, Object value)
        {
            SerializedProperty property = serialized.FindProperty(propertyPath);
            if (property != null && property.objectReferenceValue == null && value != null)
            {
                property.objectReferenceValue = value;
            }
        }
    }
}
