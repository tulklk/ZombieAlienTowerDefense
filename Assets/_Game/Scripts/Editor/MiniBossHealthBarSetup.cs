using System.Collections.Generic;
using AlienDefense.UI;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace AlienDefense.EditorTools
{
    /// <summary>Gives every regular Enemy_* prefab the boss's skull icon on its world health bar (a copy of the
    /// BossIcon from Enemy_Boss_Level3's bar, hidden by default) and wires it to EnemyHealthBarView._bossIcon, so a
    /// mini boss (EnemyRank.MiniBoss) shows a boss-style bar: always visible, red, skull icon. Safe to run again.</summary>
    public static class MiniBossHealthBarSetup
    {
        private const string BossPrefabPath = "Assets/_Game/Prefabs/Enemies/Enemy_Boss_Level3.prefab";

        private static readonly string[] EnemyPrefabs =
        {
            "Assets/_Game/Prefabs/Enemies/Enemy_Normal.prefab",
            "Assets/_Game/Prefabs/Enemies/Enemy_Runner.prefab",
            "Assets/_Game/Prefabs/Enemies/Enemy_Armored.prefab",
            "Assets/_Game/Prefabs/Enemies/Enemy_Shield.prefab",
            "Assets/_Game/Prefabs/Enemies/Enemy_Tank.prefab",
        };

        [MenuItem("Tools/Tower Defense/Enemies/Setup Mini Boss Health Bars")]
        public static void SetupFromMenu()
        {
            Debug.Log(Setup());
        }

        private const string FontPath = "Assets/_Game/Font/Fredoka-Bold SDF.asset";
        private const string FontMaterialPath = "Assets/_Game/Font/Materials/Fredoka_HUD.mat";

        /// <summary>The "x5" label to the right of the bar (hidden until a layered mini boss uses it).</summary>
        private static TMP_Text EnsureLayerLabel(Transform bar)
        {
            Transform existing = bar.Find("LayerCount");
            GameObject go = existing != null ? existing.gameObject : new GameObject("LayerCount", typeof(RectTransform));
            go.transform.SetParent(bar, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(6f, 0f);
            rect.sizeDelta = new Vector2(70f, 40f);
            var text = go.GetComponent<TextMeshProUGUI>();
            if (text == null)
            {
                text = go.AddComponent<TextMeshProUGUI>();
            }

            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(FontMaterialPath);
            if (material != null)
            {
                text.fontSharedMaterial = material;
            }

            text.text = "x5";
            text.fontSize = 30f;
            text.fontStyle = FontStyles.Bold;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.raycastTarget = false;
            go.SetActive(false);
            return text;
        }

        public static string Setup()
        {
            var report = new List<string>();
            var boss = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath);
            Transform bossIcon = null;
            foreach (Transform t in boss.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "BossIcon")
                {
                    bossIcon = t;
                }
            }

            if (bossIcon == null)
            {
                return "[MiniBossHealthBarSetup] No BossIcon on " + BossPrefabPath;
            }

            foreach (string path in EnemyPrefabs)
            {
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var view = root.GetComponentInChildren<EnemyHealthBarView>(true);
                    if (view == null)
                    {
                        report.Add(path + ": no EnemyHealthBarView, skipped.");
                        continue;
                    }

                    var so = new SerializedObject(view);
                    var visualRoot = so.FindProperty("_visualRoot").objectReferenceValue as GameObject;
                    Transform parent = visualRoot != null ? visualRoot.transform : view.transform;
                    Transform icon = parent.Find("BossIcon");
                    if (icon == null)
                    {
                        icon = Object.Instantiate(bossIcon.gameObject, parent, false).transform;
                        icon.name = "BossIcon";
                    }

                    icon.gameObject.SetActive(false);
                    so.FindProperty("_bossIcon").objectReferenceValue = icon.gameObject;
                    so.FindProperty("_layerLabel").objectReferenceValue = EnsureLayerLabel(parent);
                    so.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    report.Add(System.IO.Path.GetFileNameWithoutExtension(path) + ": BossIcon added to the health bar (hidden unless mini boss).");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            return "[MiniBossHealthBarSetup] Done.\n" + string.Join("\n", report);
        }
    }
}
