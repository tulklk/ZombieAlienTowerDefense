using System;
using AlienDefense.Core;
using AlienDefense.Meta;
using AlienDefense.Save;
using AlienDefense.UI.MainMenu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.Profile
{
    /// <summary>Owns Profile open/close and data refresh. Cascaded from MainMenuPresenter — does not implement
    /// IApplicationServicesReceiver (same rule as BaseScreenPresenter).</summary>
    public sealed class ProfilePresenter : MonoBehaviour
    {
        private static readonly Color AccentCyan = new Color(0.3f, 0.8f, 0.95f, 1f);
        private static readonly Color AccentYellow = new Color(0.95f, 0.85f, 0.2f, 1f);
        private static readonly Color AccentBlue = new Color(0.2f, 0.45f, 0.85f, 1f);
        private static readonly Color PanelColor = new Color(0.08f, 0.12f, 0.22f, 0.96f);

        [SerializeField]
        private ProfileView _view;

        [SerializeField]
        private ProfileUfoPreviewController _ufoPreview;

        [SerializeField]
        private ProfileBottomBarView _bottomBar;

        [SerializeField]
        private ProfileSettingsOverlayView _settingsOverlay;

        [SerializeField]
        private GameObject _menuContentRoot;

        [SerializeField]
        private GameObject _topHudRoot;

        [SerializeField]
        private GameObject _bottomNavigationRoot;

        [SerializeField]
        private Sprite _defaultAvatarSprite;

        private ApplicationServices _services;
        private bool _isOpen;
        private bool _isAnimating;
        private bool _menuContentWasActive = true;
        private bool _topHudWasActive = true;
        private bool _bottomNavWasActive = true;
        private bool _initialized;
        private bool _chromeEnsured;

        public bool IsOpen => _isOpen;

        public bool IsAnimating => _isAnimating;

        public void Configure(
            ProfileView view,
            ProfileUfoPreviewController ufoPreview,
            GameObject menuContentRoot,
            GameObject topHudRoot,
            Sprite defaultAvatarSprite,
            ProfileBottomBarView bottomBar = null,
            ProfileSettingsOverlayView settingsOverlay = null,
            GameObject bottomNavigationRoot = null)
        {
            _view = view;
            _ufoPreview = ufoPreview;
            _menuContentRoot = menuContentRoot;
            _topHudRoot = topHudRoot;
            _defaultAvatarSprite = defaultAvatarSprite;
            _bottomBar = bottomBar;
            _settingsOverlay = settingsOverlay;
            _bottomNavigationRoot = bottomNavigationRoot;
        }

        public void Initialize(ApplicationServices services)
        {
            _services = services;
            EnsureChrome();
            _settingsOverlay?.Bind(services?.SettingsService);

            if (_view == null || _initialized)
            {
                return;
            }

            _initialized = true;
            WireEvents();
        }

        private void WireEvents()
        {
            _view.BackClicked -= Close;
            _view.BackClicked += Close;
            _view.EditNameClicked -= HandleEditNameClicked;
            _view.EditNameClicked += HandleEditNameClicked;
            _view.EditAvatarClicked -= HandleEditAvatarClicked;
            _view.EditAvatarClicked += HandleEditAvatarClicked;
            _view.CopyIdClicked -= HandleCopyIdClicked;
            _view.CopyIdClicked += HandleCopyIdClicked;

            if (_view.EditNamePopup != null)
            {
                _view.EditNamePopup.Cancelled -= HandleEditNameCancelled;
                _view.EditNamePopup.Cancelled += HandleEditNameCancelled;
                _view.EditNamePopup.Saved -= HandleEditNameSaved;
                _view.EditNamePopup.Saved += HandleEditNameSaved;
            }

            if (_bottomBar != null)
            {
                _bottomBar.BackClicked -= Close;
                _bottomBar.BackClicked += Close;
                _bottomBar.ProfileClicked -= HandleProfileTabClicked;
                _bottomBar.ProfileClicked += HandleProfileTabClicked;
                _bottomBar.SettingsClicked -= HandleSettingsTabClicked;
                _bottomBar.SettingsClicked += HandleSettingsTabClicked;
            }

            if (_settingsOverlay != null)
            {
                _settingsOverlay.Closed -= HandleSettingsClosed;
                _settingsOverlay.Closed += HandleSettingsClosed;
            }
        }

        private void OnDestroy()
        {
            if (_view != null)
            {
                _view.BackClicked -= Close;
                _view.EditNameClicked -= HandleEditNameClicked;
                _view.EditAvatarClicked -= HandleEditAvatarClicked;
                _view.CopyIdClicked -= HandleCopyIdClicked;

                if (_view.EditNamePopup != null)
                {
                    _view.EditNamePopup.Cancelled -= HandleEditNameCancelled;
                    _view.EditNamePopup.Saved -= HandleEditNameSaved;
                }
            }

            if (_bottomBar != null)
            {
                _bottomBar.BackClicked -= Close;
                _bottomBar.ProfileClicked -= HandleProfileTabClicked;
                _bottomBar.SettingsClicked -= HandleSettingsTabClicked;
            }

            if (_settingsOverlay != null)
            {
                _settingsOverlay.Closed -= HandleSettingsClosed;
            }
        }

        public void Open()
        {
            if (_view == null || _isOpen)
            {
                return;
            }

            // Clear a stuck animation lock so Avatar can reopen after a failed tween.
            _isAnimating = false;

            EnsureChrome();
            _isOpen = true;
            CaptureMenuVisibility();
            SetMenuVisible(false);
            RefreshAll();
            _settingsOverlay?.Hide();
            _bottomBar?.Show();
            _bottomBar?.SetProfileSelected(true);
            _view.SetShowcaseActive(true);
            _ufoPreview?.SetActive(true);

            // Failsafe: panel must be visible even if DOTween never completes.
            _view.ShowInstant();
            _isAnimating = true;
            _view.PlayOpenAnimation(() => _isAnimating = false);
        }

        public void Close()
        {
            if (!_isOpen || _view == null)
            {
                return;
            }

            _isAnimating = false;
            _view.EditNamePopup?.Hide();
            _settingsOverlay?.Hide();
            _bottomBar?.Hide();
            _isAnimating = true;
            _view.PlayCloseAnimation(() =>
            {
                _ufoPreview?.SetActive(false);
                RestoreMenuVisibility();
                _isOpen = false;
                _isAnimating = false;
            });
        }

        public void RefreshAll()
        {
            PlayerProfileService profile = _services?.PlayerProfileService;
            if (_view == null || profile == null)
            {
                return;
            }

            if (_defaultAvatarSprite == null)
            {
                _defaultAvatarSprite = TryFindHudAvatarSprite();
            }

            if (_defaultAvatarSprite == null)
            {
                _defaultAvatarSprite = _view.CurrentAvatarSprite;
            }

            if (_defaultAvatarSprite == null)
            {
                _defaultAvatarSprite = TryLoadPotraitSprite();
            }

            _view.SetIdentity(profile.DisplayName, profile.ProfileId, _defaultAvatarSprite);
            _view.SetLevel(_services.PlayerLevels.Evaluate(profile.PlayerExperience).Level);
            _view.SetEnergyUnavailable();

            int power = PlayerPowerCalculator.Compute(profile, _services.TowerCatalog);
            string campaign = ResolveCampaignLabel(profile);
            _view.SetStats(
                CurrencyFormatter.Format(power),
                profile.GetUnlockedTowerCount().ToString(),
                campaign,
                CurrencyFormatter.Format(profile.TotalTowerDamage),
                CurrencyFormatter.Format(profile.ZombiesKilled));
        }

        private void EnsureChrome()
        {
            if (_bottomNavigationRoot == null)
            {
                _bottomNavigationRoot = GameObject.Find("BottomNavigationSlot");
            }

            if (_menuContentRoot == null)
            {
                _menuContentRoot = GameObject.Find("MenuContentRoot");
            }

            if (_topHudRoot == null)
            {
                _topHudRoot = GameObject.Find("TopHUD");
            }

            Transform profileRoot = _view != null ? _view.transform : null;
            if (profileRoot == null)
            {
                return;
            }

            if (!_chromeEnsured)
            {
                PatchFonts(profileRoot);
                _chromeEnsured = true;
            }

            if (_bottomBar == null)
            {
                _bottomBar = BuildBottomBar(profileRoot);
            }

            if (_settingsOverlay == null)
            {
                _settingsOverlay = BuildSettingsOverlay(profileRoot);
            }

            if (_initialized)
            {
                WireEvents();
            }

            _settingsOverlay?.Bind(_services?.SettingsService);
        }

        private static void PatchFonts(Transform root)
        {
            TMP_FontAsset font = FindUiFont();
            if (font == null)
            {
                return;
            }

            TextMeshProUGUI[] texts = root.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                texts[i].font = font;
                texts[i].isOrthographic = true;
            }
        }

        private static TMP_FontAsset FindUiFont()
        {
            TextMeshProUGUI[] all = UnityEngine.Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].font != null && all[i].font.name.IndexOf("Fredoka", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return all[i].font;
                }
            }

            return TMP_Settings.defaultFontAsset;
        }

        private static Sprite TryFindHudAvatarSprite()
        {
            PlayerProfileWidgetView widget = UnityEngine.Object.FindFirstObjectByType<PlayerProfileWidgetView>(
                FindObjectsInactive.Include);
            if (widget == null)
            {
                return null;
            }

            Image[] images = widget.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] != null && images[i].gameObject.name.Contains("Avatar") && images[i].sprite != null)
                {
                    return images[i].sprite;
                }
            }

            return null;
        }

        private static Sprite TryLoadPotraitSprite()
        {
#if UNITY_EDITOR
            Sprite editorSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Game/Art/Sprite/MainMenu/Avatar/Potrait.png");
            if (editorSprite != null)
            {
                return editorSprite;
            }
#endif
            return Resources.Load<Sprite>("MainMenu/Avatar/Potrait");
        }

        private ProfileBottomBarView BuildBottomBar(Transform profileRoot)
        {
            Transform existing = profileRoot.Find("ProfileBottomBar");
            if (existing != null)
            {
                ProfileBottomBarView existingView = existing.GetComponent<ProfileBottomBarView>();
                if (existingView != null)
                {
                    return existingView;
                }

                UnityEngine.Object.Destroy(existing.gameObject);
            }

            var root = new GameObject("ProfileBottomBar", typeof(RectTransform));
            root.transform.SetParent(profileRoot, false);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0f, 0f);
            rootRect.anchorMax = new Vector2(1f, 0f);
            rootRect.pivot = new Vector2(0.5f, 0f);
            rootRect.sizeDelta = new Vector2(0f, 200f);

            var row = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(root.transform, false);
            Stretch(row.GetComponent<RectTransform>(), 24f, 16f, 24f, 24f);
            HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 24f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = true;
            layout.padding = new RectOffset(32, 32, 12, 12);

            Button back = BuildCircleButton(row.transform, "BackButton", "<", Color.white, AccentCyan);
            Button profile = BuildTileButton(row.transform, "ProfileButton", "Profile", AccentYellow, out Image profileTile);
            Button settings = BuildTileButton(row.transform, "SettingsButton", "Settings", AccentBlue, out Image settingsTile);

            ProfileBottomBarView view = root.AddComponent<ProfileBottomBarView>();
            view.Wire(root, back, profile, settings, profileTile, settingsTile);
            return view;
        }

        private ProfileSettingsOverlayView BuildSettingsOverlay(Transform profileRoot)
        {
            Transform existing = profileRoot.Find("SettingsOverlay");
            if (existing != null)
            {
                ProfileSettingsOverlayView existingView = existing.GetComponent<ProfileSettingsOverlayView>();
                if (existingView != null)
                {
                    return existingView;
                }

                UnityEngine.Object.Destroy(existing.gameObject);
            }

            var root = new GameObject("SettingsOverlay", typeof(RectTransform));
            root.transform.SetParent(profileRoot, false);
            Stretch(root.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);

            var dim = new GameObject("Dim", typeof(RectTransform), typeof(Image));
            dim.transform.SetParent(root.transform, false);
            Stretch(dim.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
            dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            panel.transform.SetParent(root.transform, false);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(560f, 420f);
            panel.GetComponent<Image>().color = PanelColor;
            VerticalLayoutGroup vlg = panel.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(28, 28, 28, 28);
            vlg.spacing = 18f;
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;

            CreateLabel(panel.transform, "Title", "Settings", 34f, FontStyles.Bold);
            BuildToggleRow(panel.transform, "SfxRow", "SFX", out Button sfxBtn, out Image sfxIcon, out TMP_Text sfxLabel);
            BuildToggleRow(panel.transform, "MusicRow", "Music", out Button musicBtn, out Image musicIcon, out TMP_Text musicLabel);
            BuildToggleRow(panel.transform, "VibrationRow", "Vibration", out Button vibBtn, out Image vibIcon, out TMP_Text vibLabel);
            Button close = BuildWideButton(panel.transform, "CloseButton", "Close", AccentCyan);

            ProfileSettingsOverlayView view = root.AddComponent<ProfileSettingsOverlayView>();
            view.Wire(root, close, sfxBtn, musicBtn, vibBtn, sfxIcon, musicIcon, vibIcon, sfxLabel, musicLabel, vibLabel);
            return view;
        }

        private static Button BuildCircleButton(Transform parent, string name, string glyph, Color bg, Color glyphColor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredWidth = 96f;
            go.GetComponent<LayoutElement>().preferredHeight = 96f;
            go.GetComponent<LayoutElement>().flexibleWidth = 0f;
            go.GetComponent<Image>().color = bg;
            TMP_Text label = CreateLabel(go.transform, "Label", glyph, 40f, FontStyles.Bold);
            label.color = glyphColor;
            label.alignment = TextAlignmentOptions.Center;
            return go.GetComponent<Button>();
        }

        private static Button BuildTileButton(Transform parent, string name, string caption, Color bg, out Image tile)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredWidth = 160f;
            go.GetComponent<LayoutElement>().preferredHeight = 140f;
            go.GetComponent<LayoutElement>().flexibleWidth = 1f;
            tile = go.GetComponent<Image>();
            tile.color = bg;
            TMP_Text label = CreateLabel(go.transform, "Label", caption, 26f, FontStyles.Bold);
            label.alignment = TextAlignmentOptions.Center;
            return go.GetComponent<Button>();
        }

        private static void BuildToggleRow(Transform parent, string name, string label, out Button button, out Image icon, out TMP_Text labelText)
        {
            var row = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            row.GetComponent<LayoutElement>().preferredHeight = 72f;
            row.GetComponent<Image>().color = new Color(0.05f, 0.08f, 0.14f, 0.9f);
            HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 8, 8);
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childForceExpandWidth = true;

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            iconGo.transform.SetParent(row.transform, false);
            iconGo.GetComponent<LayoutElement>().preferredWidth = 48f;
            iconGo.GetComponent<LayoutElement>().preferredHeight = 48f;
            icon = iconGo.GetComponent<Image>();
            icon.color = Color.white;

            labelText = CreateLabel(row.transform, "Label", label + "  ON", 26f, FontStyles.Normal);
            button = row.GetComponent<Button>();
        }

        private static Button BuildWideButton(Transform parent, string name, string caption, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = 64f;
            go.GetComponent<Image>().color = color;
            TMP_Text label = CreateLabel(go.transform, "Label", caption, 28f, FontStyles.Bold);
            label.alignment = TextAlignmentOptions.Center;
            return go.GetComponent<Button>();
        }

        private static TMP_Text CreateLabel(Transform parent, string name, string text, float size, FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = 40f;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.color = Color.white;
            tmp.isOrthographic = true;
            TMP_FontAsset font = FindUiFont();
            if (font != null)
            {
                tmp.font = font;
            }

            return tmp;
        }

        private static void Stretch(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static string ResolveCampaignLabel(PlayerProfileService profile)
        {
            if (!string.IsNullOrEmpty(profile.HighestUnlockedLevelId))
            {
                string id = profile.HighestUnlockedLevelId;
                int underscore = id.LastIndexOf('_');
                if (underscore >= 0 && underscore < id.Length - 1)
                {
                    return id.Substring(underscore + 1).TrimStart('0');
                }

                return id;
            }

            return profile.GetCompletedLevelCount().ToString();
        }

        private void HandleProfileTabClicked()
        {
            _settingsOverlay?.Hide();
            _bottomBar?.SetProfileSelected(true);
            RefreshAll();
        }

        private void HandleSettingsTabClicked()
        {
            _bottomBar?.SetProfileSelected(false);
            _settingsOverlay?.Show();
        }

        private void HandleSettingsClosed()
        {
            _bottomBar?.SetProfileSelected(true);
        }

        private void HandleEditNameClicked()
        {
            PlayerProfileService profile = _services?.PlayerProfileService;
            if (profile == null || _view?.EditNamePopup == null)
            {
                return;
            }

            _view.EditNamePopup.Show(profile.DisplayName);
        }

        private void HandleEditNameCancelled()
        {
            _view?.EditNamePopup?.Hide();
        }

        private void HandleEditNameSaved(string raw)
        {
            PlayerProfileService profile = _services?.PlayerProfileService;
            if (profile == null || _view?.EditNamePopup == null)
            {
                return;
            }

            if (!profile.TrySetDisplayName(raw, out string error))
            {
                _view.EditNamePopup.SetError(error);
                return;
            }

            _view.EditNamePopup.Hide();
            RefreshAll();
        }

        private void HandleEditAvatarClicked()
        {
            Debug.Log("[Profile] Avatar edit is not available yet.");
        }

        private void HandleCopyIdClicked()
        {
            PlayerProfileService profile = _services?.PlayerProfileService;
            if (profile == null || string.IsNullOrEmpty(profile.ProfileId))
            {
                return;
            }

            GUIUtility.systemCopyBuffer = profile.ProfileId;
            _view?.ShowToast("ID copied");
        }

        private void CaptureMenuVisibility()
        {
            _menuContentWasActive = _menuContentRoot == null || _menuContentRoot.activeSelf;
            _topHudWasActive = _topHudRoot == null || _topHudRoot.activeSelf;
            _bottomNavWasActive = _bottomNavigationRoot == null || _bottomNavigationRoot.activeSelf;
        }

        private void RestoreMenuVisibility()
        {
            if (_menuContentRoot != null)
            {
                _menuContentRoot.SetActive(_menuContentWasActive);
            }

            if (_topHudRoot != null)
            {
                _topHudRoot.SetActive(_topHudWasActive);
            }

            if (_bottomNavigationRoot != null)
            {
                _bottomNavigationRoot.SetActive(_bottomNavWasActive);
            }
        }

        private void SetMenuVisible(bool visible)
        {
            if (_menuContentRoot != null)
            {
                _menuContentRoot.SetActive(visible);
            }

            if (_topHudRoot != null)
            {
                _topHudRoot.SetActive(visible);
            }

            if (_bottomNavigationRoot != null)
            {
                _bottomNavigationRoot.SetActive(visible);
            }
        }
    }
}
