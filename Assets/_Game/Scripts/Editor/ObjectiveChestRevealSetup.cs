using System.IO;
using AlienDefense.UI.MainMenu;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.EditorTools
{
    /// <summary>Wires the objective-chest reveal in MainMenu: the closed/open chest sprites on the three
    /// LevelObjectiveView slots, and the two green progress fills laid over the dark pipes that are painted into the
    /// reward-board background sprite (the board is one image, so the pipes cannot be recoloured themselves).
    /// Safe to re-run: existing connectors keep whatever position/size they were hand-tuned to.</summary>
    public static class ObjectiveChestRevealSetup
    {
        private const string MainMenuPath = "Assets/_Game/Scenes/Menu/MainMenu.unity";
        private const string RewardFolder = "Assets/_Game/Art/Sprite/MainMenu/Reward";
        private const string FillSpritePath = RewardFolder + "/connector-fill.png";

        // Where the two pipes sit on reward-board, in ObjectivesPanel space (measured off the sprite).
        private static readonly Vector2[] ConnectorPositions = { new Vector2(-133.4f, 16f), new Vector2(133.2f, 16f) };
        private static readonly Vector2 ConnectorSize = new Vector2(54f, 30f);

        [MenuItem("AlienDefense/Objectives/Setup Chest Reveal (MainMenu)")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[ObjectiveChestRevealSetup] Exit Play Mode first.");
                return;
            }

            Sprite closed = AssetDatabase.LoadAssetAtPath<Sprite>(RewardFolder + "/chest.png");
            Sprite open = AssetDatabase.LoadAssetAtPath<Sprite>(RewardFolder + "/openchest.png");
            if (closed == null || open == null)
            {
                Debug.LogError("[ObjectiveChestRevealSetup] chest.png / openchest.png not found in " + RewardFolder);
                return;
            }

            Sprite fill = EnsureFillSprite();

            var scene = EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Single);
            LevelObjectivePanelView panel = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                panel = root.GetComponentInChildren<LevelObjectivePanelView>(true);
                if (panel != null)
                {
                    break;
                }
            }

            if (panel == null)
            {
                Debug.LogError("[ObjectiveChestRevealSetup] LevelObjectivePanelView not found in MainMenu.");
                return;
            }

            foreach (LevelObjectiveView slot in panel.GetComponentsInChildren<LevelObjectiveView>(true))
            {
                var slotSo = new SerializedObject(slot);
                slotSo.FindProperty("_closedSprite").objectReferenceValue = closed;
                slotSo.FindProperty("_openSprite").objectReferenceValue = open;
                slotSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(slot);
            }

            var panelSo = new SerializedObject(panel);
            SerializedProperty connectors = panelSo.FindProperty("_connectors");
            connectors.arraySize = ConnectorPositions.Length;
            for (int i = 0; i < ConnectorPositions.Length; i++)
            {
                connectors.GetArrayElementAtIndex(i).objectReferenceValue = EnsureConnector(panel.transform, i, fill);
            }

            panelSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(panel);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[ObjectiveChestRevealSetup] Done.");
        }

        private static Image EnsureConnector(Transform panel, int index, Sprite fill)
        {
            string name = "ConnectorFill_0" + (index + 1);
            Transform existing = panel.Find(name);
            GameObject go;
            if (existing != null)
            {
                go = existing.gameObject;
            }
            else
            {
                go = new GameObject(name, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(panel, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = ConnectorPositions[index];
                rt.sizeDelta = ConnectorSize;
            }

            // Right above the board, below the chests and their bubbles.
            Transform background = panel.Find("Background");
            go.transform.SetSiblingIndex(background != null ? background.GetSiblingIndex() + 1 + index : index);

            Image image = go.GetComponent<Image>();
            image.sprite = fill;
            image.color = Color.white;
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = (int)Image.OriginHorizontal.Left;
            image.fillAmount = 0f;
            image.raycastTarget = false;
            EditorUtility.SetDirty(image);
            return image;
        }

        /// <summary>A small vertical green gradient (bright band near the top, darker rim) so the fill reads as a
        /// lit pipe rather than a flat bar. Generated once; replace the PNG freely.</summary>
        private static Sprite EnsureFillSprite()
        {
            if (!File.Exists(FillSpritePath))
            {
                const int width = 8;
                const int height = 32;
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                var dark = new Color(0.10f, 0.55f, 0.16f, 1f);
                var mid = new Color(0.24f, 0.86f, 0.30f, 1f);
                var bright = new Color(0.70f, 1f, 0.62f, 1f);
                for (int y = 0; y < height; y++)
                {
                    float v = y / (height - 1f); // 0 bottom .. 1 top
                    Color c = v < 0.7f ? Color.Lerp(dark, mid, v / 0.7f) : Color.Lerp(bright, mid, (v - 0.7f) / 0.3f);
                    if (v > 0.62f && v < 0.82f)
                    {
                        c = Color.Lerp(c, bright, 0.6f);
                    }

                    for (int x = 0; x < width; x++)
                    {
                        texture.SetPixel(x, y, c);
                    }
                }

                texture.Apply();
                File.WriteAllBytes(FillSpritePath, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(FillSpritePath);

                var importer = (TextureImporter)AssetImporter.GetAtPath(FillSpritePath);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(FillSpritePath);
        }
    }
}
