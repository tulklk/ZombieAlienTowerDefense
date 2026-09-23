using AlienDefense.UI.MainMenu;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AlienDefense.EditorTools
{
    /// <summary>Hooks MainMenu's TopHUD pills up to MainMenuResourcePresenter: the Coin and Gem pills had lost their
    /// references (they printed the layout's placeholder "60/60"), and the Energy pill gets the small countdown line
    /// under it. The pills are told apart by the names the HUD was built with (CoinWidget / GemWidget /
    /// EnergyWidget). Only assigns what is missing and adds the countdown text once; re-runnable. Leaves saving the
    /// scene to you, so any unsaved edits of your own in it stay your call.</summary>
    internal static class TopHudResourceSetup
    {
        private const string CountdownName = "RegenTimerText";

        [MenuItem("AlienDefense/Setup/HUD/Wire TopHUD Resources (MainMenu)")]
        private static void Wire()
        {
            var presenter = Object.FindFirstObjectByType<MainMenuResourcePresenter>(FindObjectsInactive.Include);
            if (presenter == null)
            {
                Debug.LogWarning("[TopHudResourceSetup] No MainMenuResourcePresenter in the open scene - open MainMenu first.");
                return;
            }

            ResourceWidgetView coin = null, gem = null, energy = null;
            foreach (ResourceWidgetView widget in Object.FindObjectsByType<ResourceWidgetView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                string name = widget.name.ToLowerInvariant();
                if (name.Contains("coin")) coin = widget;
                else if (name.Contains("gem")) gem = widget;
                else if (name.Contains("energy")) energy = widget;
            }

            var so = new SerializedObject(presenter);
            AssignIfEmpty(so, "_coinWidget", coin);
            AssignIfEmpty(so, "_premiumCurrencyWidget", gem);
            AssignIfEmpty(so, "_energyWidget", energy);
            so.ApplyModifiedProperties();

            if (energy != null)
            {
                AddCountdown(energy);
            }

            var profileWidget = Object.FindFirstObjectByType<PlayerProfileWidgetView>(FindObjectsInactive.Include);
            if (profileWidget != null)
            {
                AddXpText(profileWidget);
            }

            EditorSceneManager.MarkSceneDirty(presenter.gameObject.scene);
            Debug.Log($"[TopHudResourceSetup] coin={Name(coin)} gem={Name(gem)} energy={Name(energy)} wired. Save the scene to keep it.");
        }

        private static void AssignIfEmpty(SerializedObject so, string field, Object value)
        {
            SerializedProperty property = so.FindProperty(field);
            if (property != null && property.objectReferenceValue == null && value != null)
            {
                property.objectReferenceValue = value;
            }
        }

        /// <summary>A second, smaller line under the pill in the amount's own font ("7m 16s").</summary>
        private static void AddCountdown(ResourceWidgetView widget)
        {
            var so = new SerializedObject(widget);
            SerializedProperty sub = so.FindProperty("_subText");
            if (sub.objectReferenceValue != null)
            {
                return;
            }

            var amount = so.FindProperty("_amountText").objectReferenceValue as TMP_Text;
            var go = new GameObject(CountdownName, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Energy countdown");
            go.transform.SetParent(widget.transform, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -4f);
            rect.sizeDelta = new Vector2(0f, 34f);

            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = "7m 16s";
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = amount != null ? amount.fontSize * 0.72f : 24f;
            text.color = Color.white;
            text.raycastTarget = false;
            if (amount != null)
            {
                text.font = amount.font;
                text.fontSharedMaterial = amount.fontSharedMaterial;
                text.fontStyle = amount.fontStyle;
            }

            sub.objectReferenceValue = text;
            so.ApplyModifiedProperties();
            go.SetActive(false); // the presenter shows it only while the bar is refilling
        }

        /// <summary>"1.2K/3.5K" centred on the XP bar, over its fill, in the level number's font.</summary>
        private static void AddXpText(PlayerProfileWidgetView widget)
        {
            var so = new SerializedObject(widget);
            SerializedProperty xpText = so.FindProperty("_xpText");
            var fill = so.FindProperty("_xpFillImage").objectReferenceValue as UnityEngine.UI.Image;
            if (xpText.objectReferenceValue != null || fill == null)
            {
                return;
            }

            Transform bar = fill.transform.parent != null ? fill.transform.parent : fill.transform;
            var levelText = so.FindProperty("_playerLevelText").objectReferenceValue as TMP_Text;
            var go = new GameObject("XpText", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "XP text");
            go.transform.SetParent(bar, false);
            go.transform.SetAsLastSibling(); // drawn over the fill

            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(4f, 0f);
            rect.offsetMax = new Vector2(-4f, 0f);

            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = "0/1K";
            text.alignment = TextAlignmentOptions.Center;
            text.enableAutoSizing = true;
            text.fontSizeMin = 8f;
            text.fontSizeMax = 26f;
            text.color = Color.white;
            text.raycastTarget = false;
            if (levelText != null)
            {
                text.font = levelText.font;
                text.fontSharedMaterial = levelText.fontSharedMaterial;
            }

            xpText.objectReferenceValue = text;
            so.ApplyModifiedProperties();
        }

        private static string Name(Object value) => value != null ? value.name : "missing";
    }
}
