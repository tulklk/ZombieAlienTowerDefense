using AlienDefense.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AlienDefense.EditorTools
{
    /// <summary>Patches existing Level scenes so GameHUD gets CargoFullBanner + EnergyForbiddenIcon wired.</summary>
    internal static class CargoFullHudSetup
    {
        private const string PlaySpriteDir = "Assets/_Game/Art/Sprite/Play";

        private static readonly string[] LevelScenes =
        {
            "Assets/_Game/Scenes/Levels/Level_01.unity",
            "Assets/_Game/Scenes/Levels/Level_02.unity",
            "Assets/_Game/Scenes/Levels/Level_03.unity",
        };

        [MenuItem("AlienDefense/Setup/33. Upgrade Cargo Full HUD (All Levels)")]
        public static void UpgradeAllLevels()
        {
            foreach (string path in LevelScenes)
            {
                if (!System.IO.File.Exists(path))
                {
                    continue;
                }

                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                int patched = UpgradeOpenScene();
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[CargoFullHudSetup] Patched " + patched + " GameHUDView(s) in " + path);
            }
        }

        /// <summary>Batchmode entry — upgrades Level_01 then quits-friendly (caller uses -quit).</summary>
        public static void UpgradeLevel01AndReport()
        {
            const string path = "Assets/_Game/Scenes/Levels/Level_01.unity";
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            int patched = UpgradeOpenScene();
            EditorSceneManager.SaveScene(scene);

            string reportDir = "Assets/_Game/EditorReports";
            if (!System.IO.Directory.Exists(reportDir))
            {
                System.IO.Directory.CreateDirectory(reportDir);
            }

            System.IO.File.WriteAllText(
                reportDir + "/CargoFullHudUpgrade.done",
                "patched=" + patched + "\nscene=" + path + "\n");
            Debug.Log("[CargoFullHudSetup] Level_01 cargo HUD upgrade done. patched=" + patched);
        }

        private static int UpgradeOpenScene()
        {
            int count = 0;
            GameHUDView[] views = Object.FindObjectsByType<GameHUDView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (GameHUDView view in views)
            {
                if (UpgradeView(view))
                {
                    count++;
                    EditorUtility.SetDirty(view);
                }
            }

            return count;
        }

        private static bool UpgradeView(GameHUDView view)
        {
            Transform gameHud = view.transform;
            Transform energyRing = FindDeepChild(gameHud, "EnergyRing");
            if (energyRing == null)
            {
                Debug.LogWarning("[CargoFullHudSetup] No EnergyRing under " + view.name);
                return false;
            }

            Image forbidden = EnsureForbiddenIcon(energyRing);
            Transform bannerParent = energyRing.parent != null ? energyRing.parent : gameHud;
            CanvasGroup banner = EnsureCargoFullBanner(bannerParent);

            var so = new SerializedObject(view);
            bool changed = false;
            changed |= AssignIfNull(so, "_energyForbiddenIcon", forbidden);
            changed |= AssignIfNull(so, "_cargoFullBanner", banner);
            // Always re-assign so a previous failed find doesn't leave nulls.
            so.FindProperty("_energyForbiddenIcon").objectReferenceValue = forbidden;
            so.FindProperty("_cargoFullBanner").objectReferenceValue = banner;
            so.ApplyModifiedPropertiesWithoutUndo();

            return true;
        }

        private static Transform FindDeepChild(Transform root, string name)
        {
            if (root.name == name)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeepChild(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static bool AssignIfNull(SerializedObject so, string property, Object value)
        {
            SerializedProperty prop = so.FindProperty(property);
            if (prop == null)
            {
                return false;
            }

            if (prop.objectReferenceValue == null && value != null)
            {
                prop.objectReferenceValue = value;
                return true;
            }

            return false;
        }

        private static Image EnsureForbiddenIcon(Transform energyRing)
        {
            Transform existing = energyRing.Find("EnergyForbiddenIcon");
            if (existing != null)
            {
                return existing.GetComponent<Image>();
            }

            Sprite forbiddenSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlaySpriteDir + "/forbidenicon.png");
            var go = new GameObject("EnergyForbiddenIcon", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(energyRing, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(48f, 48f);
            var image = go.GetComponent<Image>();
            image.sprite = forbiddenSprite;
            image.raycastTarget = false;
            go.SetActive(false);
            return image;
        }

        private static CanvasGroup EnsureCargoFullBanner(Transform gameHud)
        {
            Transform existing = gameHud.Find("CargoFullBanner");
            if (existing != null)
            {
                return existing.GetComponent<CanvasGroup>();
            }

            Sprite bannerSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlaySpriteDir + "/cargofull.png");
            var go = new GameObject("CargoFullBanner", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            go.transform.SetParent(gameHud, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -120f);
            rect.sizeDelta = new Vector2(420f, 90f);
            var image = go.GetComponent<Image>();
            image.sprite = bannerSprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            var group = go.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            go.SetActive(false);
            return group;
        }
    }
}
