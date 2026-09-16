using System;
using System.Collections.Generic;
using AlienDefense.Combat;
using AlienDefense.Data;
using AlienDefense.Economy;
using AlienDefense.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI
{
    /// <summary>The pause screen: audio/haptics toggles, this match's damage leaders, the skills picked so far, and
    /// the Continue / Leave / close buttons. A view only - it reads the services it is bound to and raises events;
    /// resuming and leaving the level stay with GameStateUIController.
    ///
    /// Everything is refreshed in OnEnable, because the panel is only shown while the game is paused - nothing here
    /// needs to tick.</summary>
    public sealed class PausePanelView : MonoBehaviour
    {
        /// <summary>One damage-leader line. The bar is scaled against the top contributor, not the total, so the
        /// first row always fills it.</summary>
        [Serializable]
        public struct DamageRow
        {
            public GameObject Root;
            public Image Icon;
            public TMP_Text NameText;
            public TMP_Text ValueText;
            public Image Bar;
        }

        /// <summary>One "abilities and effects" slot: an empty frame until the player picks that skill.</summary>
        [Serializable]
        public struct AbilitySlot
        {
            public GameObject Root;
            public Image Icon;
            public TMP_Text RankText;
        }

        /// <summary>A round on/off button; the dimmed icon is the "off" state.</summary>
        [Serializable]
        public struct SettingToggle
        {
            public Button Button;
            public Image Icon;
            [Tooltip("Optional. Shown while the setting is off (e.g. a slash overlay).")]
            public GameObject OffOverlay;
        }

        private const float MutedVolume = 0f;
        private static readonly Color OnTint = Color.white;
        private static readonly Color OffTint = new Color(0.45f, 0.45f, 0.5f, 1f);

        [Header("Buttons")]
        [SerializeField]
        private Button _continueButton;

        [SerializeField]
        [Tooltip("Optional. Same as Continue - the X in the panel's corner.")]
        private Button _closeButton;

        [SerializeField]
        [Tooltip("Optional. Leaves the level; GameStateUIController decides where to.")]
        private Button _leaveButton;

        [SerializeField]
        [Tooltip("Optional. Kept for levels that still offer a restart button.")]
        private Button _restartButton;

        [Header("Header")]
        [SerializeField]
        [Tooltip("Optional.")]
        private TMP_Text _titleText;

        [SerializeField]
        [Tooltip("Optional. The line under the title, e.g. Perfect Clear while the base is untouched.")]
        private TMP_Text _subtitleText;

        [Header("Damage leaders")]
        [SerializeField]
        private DamageRow[] _damageRows = new DamageRow[0];

        [SerializeField]
        [Tooltip("Optional. Shown while nothing has dealt damage yet.")]
        private GameObject _noStatisticsLabel;

        [Header("Abilities and effects")]
        [SerializeField]
        private AbilitySlot[] _abilitySlots = new AbilitySlot[0];

        [Header("Settings")]
        [SerializeField]
        private SettingToggle _sfxToggle;

        [SerializeField]
        private SettingToggle _musicToggle;

        [SerializeField]
        private SettingToggle _vibrationToggle;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Volume restored when the player switches a muted channel back on.")]
        private float _defaultSfxVolume = 0.8f;

        [SerializeField, Range(0f, 1f)]
        private float _defaultMusicVolume = 0.6f;

        public event Action ResumeClicked;
        public event Action RestartClicked;
        public event Action MainMenuClicked;

        private SettingsService _settings;
        private PlayerSkillService _skills;
        private CombatStatsService _stats;
        private readonly List<SkillType> _skillOrder = new List<SkillType>();

        private bool _listenersWired;

        private void Awake()
        {
            WireListeners();
        }

        /// <summary>Hooking the buttons is done once, from whichever comes first: Awake in a running scene, or Bind
        /// (which is also how an editor-mode test can exercise the panel without a scene).</summary>
        private void WireListeners()
        {
            if (_listenersWired)
            {
                return;
            }

            _listenersWired = true;
            AddListener(_continueButton, HandleResumeClicked);
            AddListener(_closeButton, HandleResumeClicked);
            AddListener(_leaveButton, HandleMainMenuClicked);
            AddListener(_restartButton, HandleRestartClicked);
            AddListener(_sfxToggle.Button, HandleSfxToggled);
            AddListener(_musicToggle.Button, HandleMusicToggled);
            AddListener(_vibrationToggle.Button, HandleVibrationToggled);
        }

        /// <summary>Wires the level's services. All optional: without them the panel still opens, with empty stats
        /// and toggles that do nothing.</summary>
        public void Bind(SettingsService settings, PlayerSkillService skills, CombatStatsService stats)
        {
            WireListeners();
            _settings = settings;
            _skills = skills;
            _stats = stats;

            if (isActiveAndEnabled)
            {
                Refresh();
            }
        }

        private void OnEnable()
        {
            Refresh();
        }

        /// <summary>Re-reads the services into the panel. Safe to call at any time.</summary>
        public void Refresh()
        {
            RefreshToggles();
            RefreshDamageLeaders();
            RefreshAbilities();
        }

        public void SetTitle(string text)
        {
            if (_titleText != null)
            {
                _titleText.text = text;
            }
        }

        /// <summary>Optional line under the title, e.g. "Perfect Clear" while the base has taken no damage.</summary>
        public void SetSubtitle(string text)
        {
            if (_subtitleText != null)
            {
                _subtitleText.text = text;
                _subtitleText.gameObject.SetActive(!string.IsNullOrEmpty(text));
            }
        }

        private void RefreshDamageLeaders()
        {
            IReadOnlyList<CombatStatsService.Contributor> leaders = _stats != null
                ? _stats.GetLeaders(_damageRows.Length)
                : Array.Empty<CombatStatsService.Contributor>();

            float best = leaders.Count > 0 ? Mathf.Max(1f, leaders[0].Damage) : 1f;
            for (int i = 0; i < _damageRows.Length; i++)
            {
                DamageRow row = _damageRows[i];
                bool used = i < leaders.Count;
                if (row.Root != null)
                {
                    row.Root.SetActive(used);
                }

                if (!used)
                {
                    continue;
                }

                CombatStatsService.Contributor contributor = leaders[i];
                if (row.NameText != null)
                {
                    row.NameText.text = contributor.Name;
                }

                if (row.ValueText != null)
                {
                    row.ValueText.text = Mathf.RoundToInt(contributor.Damage).ToString("N0");
                }

                if (row.Icon != null)
                {
                    row.Icon.sprite = contributor.Icon;
                    row.Icon.enabled = contributor.Icon != null;
                }

                if (row.Bar != null)
                {
                    row.Bar.fillAmount = Mathf.Clamp01(contributor.Damage / best);
                }
            }

            if (_noStatisticsLabel != null)
            {
                _noStatisticsLabel.SetActive(leaders.Count == 0);
            }
        }

        private void RefreshAbilities()
        {
            _skillOrder.Clear();
            if (_skills != null)
            {
                foreach (SkillType type in (SkillType[])Enum.GetValues(typeof(SkillType)))
                {
                    if (_skills.GetRank(type) > 0)
                    {
                        _skillOrder.Add(type);
                    }
                }
            }

            for (int i = 0; i < _abilitySlots.Length; i++)
            {
                AbilitySlot slot = _abilitySlots[i];
                bool used = i < _skillOrder.Count;
                SkillDefinition definition = used ? _skills.GetDefinition(_skillOrder[i]) : null;

                if (slot.Icon != null)
                {
                    slot.Icon.sprite = definition != null ? definition.Icon : null;
                    slot.Icon.enabled = definition != null && definition.Icon != null;
                }

                if (slot.RankText != null)
                {
                    int rank = used ? _skills.GetRank(_skillOrder[i]) : 0;
                    slot.RankText.text = rank > 0 ? "x" + rank : string.Empty;
                    slot.RankText.gameObject.SetActive(rank > 0);
                }
            }
        }

        private void RefreshToggles()
        {
            if (_settings == null)
            {
                ApplyToggleVisual(_sfxToggle, true);
                ApplyToggleVisual(_musicToggle, true);
                ApplyToggleVisual(_vibrationToggle, true);
                return;
            }

            ApplyToggleVisual(_sfxToggle, _settings.Current.SfxVolume > 0f);
            ApplyToggleVisual(_musicToggle, _settings.Current.MusicVolume > 0f);
            ApplyToggleVisual(_vibrationToggle, _settings.Current.HapticsEnabled);
        }

        private static void ApplyToggleVisual(SettingToggle toggle, bool isOn)
        {
            if (toggle.Icon != null)
            {
                toggle.Icon.color = isOn ? OnTint : OffTint;
            }

            if (toggle.OffOverlay != null)
            {
                toggle.OffOverlay.SetActive(!isOn);
            }
        }

        private void HandleSfxToggled()
        {
            if (_settings == null)
            {
                return;
            }

            bool isOn = _settings.Current.SfxVolume > 0f;
            _settings.SetSfxVolume(isOn ? MutedVolume : _defaultSfxVolume);
            ApplyToggleVisual(_sfxToggle, !isOn);
        }

        private void HandleMusicToggled()
        {
            if (_settings == null)
            {
                return;
            }

            bool isOn = _settings.Current.MusicVolume > 0f;
            _settings.SetMusicVolume(isOn ? MutedVolume : _defaultMusicVolume);
            ApplyToggleVisual(_musicToggle, !isOn);
        }

        private void HandleVibrationToggled()
        {
            if (_settings == null)
            {
                return;
            }

            bool isOn = _settings.Current.HapticsEnabled;
            _settings.SetHapticsEnabled(!isOn);
            ApplyToggleVisual(_vibrationToggle, !isOn);
        }

        private void HandleResumeClicked()
        {
            ResumeClicked?.Invoke();
        }

        private void HandleRestartClicked()
        {
            RestartClicked?.Invoke();
        }

        private void HandleMainMenuClicked()
        {
            MainMenuClicked?.Invoke();
        }

        private void OnDestroy()
        {
            RemoveListener(_continueButton, HandleResumeClicked);
            RemoveListener(_closeButton, HandleResumeClicked);
            RemoveListener(_leaveButton, HandleMainMenuClicked);
            RemoveListener(_restartButton, HandleRestartClicked);
            RemoveListener(_sfxToggle.Button, HandleSfxToggled);
            RemoveListener(_musicToggle.Button, HandleMusicToggled);
            RemoveListener(_vibrationToggle.Button, HandleVibrationToggled);
        }

        private static void AddListener(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
            {
                button.onClick.AddListener(action);
            }
        }

        private static void RemoveListener(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
            {
                button.onClick.RemoveListener(action);
            }
        }
    }
}
