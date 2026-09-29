using System;
using System.Collections.Generic;
using System.Globalization;
using AlienDefense.Audio;
using AlienDefense.Base;
using AlienDefense.Core;
using AlienDefense.Meta;
using AlienDefense.Save;
using AlienDefense.UI.MainMenu;
using UnityEngine;

namespace AlienDefense.UI.BaseBuilding
{
    /// <summary>Runs the Base tab: shows the 3D base world while the tab is open, turns taps into popups, keeps the
    /// floating timers and bubbles in sync, and plays the completion celebration.
    ///
    /// Lives inside BasePanel, so the existing MenuShellPresenter's SetActive on that panel is what switches the
    /// base on and off - no new navigation code.
    ///
    /// All rules live in BaseProgressionService; this class only reads it and forwards button presses to it. It
    /// is also the single "construction ticker" for the whole base: one Update that moves the widgets every frame
    /// (they must follow a panning camera) and refreshes state four times a second, instead of an Update on every
    /// building.</summary>
    public sealed class BaseWorldPresenter : MonoBehaviour
    {
        [Header("World")]
        [SerializeField]
        [Tooltip("The 3D base (a scene root, not under the canvas). Only active while this tab is open.")]
        private GameObject _worldRoot;

        [SerializeField]
        private BaseCameraController _cameraController;

        [SerializeField]
        private BaseBuildingView[] _buildingViews = Array.Empty<BaseBuildingView>();

        [SerializeField]
        private LayerMask _buildingLayers = 1 << 17;

        [SerializeField]
        [Tooltip("Shared golden-flash / dust / sparkle burst played where a building completes (timer or Finish).")]
        private BaseBuildCompleteFx _completionFx;

        [SerializeField]
        [Tooltip("Static scenery (ground, cliffs, nature). Combined once with StaticBatchingUtility the first time the " +
            "base is shown: the world is inactive at scene load, so the automatic static batching never sees it. " +
            "Must not contain anything that moves or animates (plots, scaffolding, buildings).")]
        private GameObject _staticBatchRoot;

        private bool _staticBatched;

        [Header("Menu pieces hidden while the base is shown")]
        [SerializeField]
        [Tooltip("The menu's own camera. It draws nothing but its clear colour while the base is up, so it is switched " +
            "off to save a full render pass on mobile.")]
        private Camera _menuCamera;

        [SerializeField]
        [Tooltip("Full-screen menu background that would otherwise cover the world.")]
        private GameObject _menuBackground;

        [Header("UI")]
        [SerializeField]
        private RectTransform _indicatorLayer;

        [SerializeField]
        private BaseBuildingIndicatorView _indicatorTemplate;

        [SerializeField]
        private BaseBuildingPopupView _popup;

        [SerializeField]
        private BaseCompletionBannerView _banner;

        [SerializeField]
        private BaseQuestTrackerView _questTracker;

        [SerializeField]
        [Tooltip("Used for GO buttons that lead out of the base (campaign) and to refresh the top HUD.")]
        private MenuShellPresenter _menuShell;

        [Header("Icons (optional)")]
        [SerializeField]
        [Tooltip("For the produced material's icon on the bubble and in the Extraction section.")]
        private MetaItemCatalog _items;

        [SerializeField]
        private Sprite _buildBubbleIcon;

        [SerializeField]
        private Sprite _productionIcon;

        [SerializeField]
        private Sprite _powerIcon;

        [SerializeField]
        private Sprite _timeIcon;

        [SerializeField]
        private Sprite _campaignIcon;

        [Header("Audio (optional - MainMenu currently has no AudioService)")]
        [SerializeField]
        private AudioService _audio;

        [SerializeField]
        private AudioClip _openClip;

        [SerializeField]
        private AudioClip _startClip;

        [SerializeField]
        private AudioClip _completeClip;

        [SerializeField]
        private AudioClip _collectClip;

        [Header("Timing")]
        [SerializeField, Range(1f, 10f)]
        [Tooltip("State refreshes per second. The widgets still move every frame.")]
        private float _refreshRate = 4f;

        private ApplicationServices _services;
        private BaseProgressionService _base;
        private PlayerProfileService _profile;

        private readonly List<BaseBuildingIndicatorView> _indicators = new List<BaseBuildingIndicatorView>();
        private BaseBuildingView _selected;
        private bool _subscribed;
        private float _nextRefresh;

        private BaseBuildingGoal _shownGoal;
        private float _questHoldUntil;

        public void Initialize(ApplicationServices services)
        {
            _services = services;
            _base = services != null ? services.BaseProgression : null;
            _profile = services != null ? services.PlayerProfileService : null;

            if (_base == null)
            {
                Debug.LogWarning("[BaseWorldPresenter] No BaseProgressionService (BaseBuildingCatalog not assigned " +
                    "on Bootstrap). The Base tab will show the world without any building logic.", this);
            }

            BuildIndicators();

            if (_popup != null)
            {
                _popup.CloseRequested = ClosePopup;
                _popup.StartRequested = HandleStart;
                _popup.FinishRequested = HandleFinish;
            }

            // Already open when services arrive (the tab was entered first): start now.
            if (isActiveAndEnabled)
            {
                Show();
            }
        }

        private Sprite GetItemIcon(string itemId)
        {
            if (string.IsNullOrEmpty(itemId) || _items == null)
            {
                return null;
            }

            return _items.TryGet(itemId, out MetaItemDefinition def) ? def.Icon : null;
        }

        private void OnEnable()
        {
            if (_services != null)
            {
                Show();
            }
        }

        private void OnDisable()
        {
            Hide();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            // Coming back to the app is the other moment an offline build may have finished.
            if (hasFocus && _base != null && isActiveAndEnabled)
            {
                _base.ResolveFinishedConstructions(DateTime.UtcNow);
            }
        }

        // ------------------------------------------------------------------ Show / hide

        private void Show()
        {
            if (_worldRoot != null) _worldRoot.SetActive(true);
            if (!_staticBatched && _staticBatchRoot != null)
            {
                StaticBatchingUtility.Combine(_staticBatchRoot);
                _staticBatched = true;
            }

            if (_menuCamera != null) _menuCamera.enabled = false;
            if (_menuBackground != null) _menuBackground.SetActive(false);

            Subscribe();

            // Offline completion: anything that finished while the game was closed completes (and celebrates) now.
            _base?.ResolveFinishedConstructions(DateTime.UtcNow);

            RefreshAll();
            _nextRefresh = 0f;
        }

        private void Hide()
        {
            Unsubscribe();
            _popup?.Close();

            if (_worldRoot != null) _worldRoot.SetActive(false);
            if (_menuCamera != null) _menuCamera.enabled = true;
            if (_menuBackground != null) _menuBackground.SetActive(true);
        }

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            if (_base != null)
            {
                _base.Changed += RefreshAll;
                _base.BuildingStarted += HandleBuildingStarted;
                _base.BuildingCompleted += HandleBuildingCompleted;
                _base.ResourceCollected += HandleResourceCollected;
            }

            if (_cameraController != null)
            {
                _cameraController.Tapped += HandleWorldTap;
            }

            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            if (_base != null)
            {
                _base.Changed -= RefreshAll;
                _base.BuildingStarted -= HandleBuildingStarted;
                _base.BuildingCompleted -= HandleBuildingCompleted;
                _base.ResourceCollected -= HandleResourceCollected;
            }

            if (_cameraController != null)
            {
                _cameraController.Tapped -= HandleWorldTap;
            }

            _subscribed = false;
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        // ------------------------------------------------------------------ Tick

        private void Update()
        {
            PositionIndicators();

            if (_base == null || Time.unscaledTime < _nextRefresh)
            {
                return;
            }

            _nextRefresh = Time.unscaledTime + 1f / Mathf.Max(1f, _refreshRate);

            // Completes builds live while the player watches; Changed then refreshes everything.
            if (_base.ResolveFinishedConstructions(DateTime.UtcNow) == 0)
            {
                RefreshIndicators(DateTime.UtcNow);
                if (_popup != null && _popup.IsOpen)
                {
                    RefreshPopup(DateTime.UtcNow);
                }
            }

            if (_questHoldUntil > 0f && Time.unscaledTime >= _questHoldUntil)
            {
                _questHoldUntil = 0f;
                RefreshQuest();
            }
        }

        private void RefreshAll()
        {
            DateTime now = DateTime.UtcNow;

            if (_base != null)
            {
                for (int i = 0; i < _buildingViews.Length; i++)
                {
                    BaseBuildingView view = _buildingViews[i];
                    if (view == null || view.Definition == null)
                    {
                        continue;
                    }

                    BaseBuildingSnapshot saved = _base.GetState(view.Definition.Id);
                    view.Apply(_base.GetDisplayState(view.Definition, now), saved.Level);
                }
            }

            RefreshIndicators(now);
            RefreshQuest();

            if (_popup != null && _popup.IsOpen)
            {
                RefreshPopup(now);
            }

            _menuShell?.RefreshHud();
        }

        // ------------------------------------------------------------------ Indicators

        private void BuildIndicators()
        {
            if (_indicatorTemplate == null || _indicatorLayer == null || _indicators.Count > 0)
            {
                return;
            }

            _indicatorTemplate.gameObject.SetActive(false);
            for (int i = 0; i < _buildingViews.Length; i++)
            {
                BaseBuildingIndicatorView indicator = Instantiate(_indicatorTemplate, _indicatorLayer);
                indicator.gameObject.SetActive(true);
                _indicators.Add(indicator);
            }
        }

        private void RefreshIndicators(DateTime now)
        {
            if (_base == null)
            {
                return;
            }

            for (int i = 0; i < _buildingViews.Length && i < _indicators.Count; i++)
            {
                BaseBuildingView view = _buildingViews[i];
                BaseBuildingIndicatorView indicator = _indicators[i];
                if (view == null || view.Definition == null || indicator == null)
                {
                    continue;
                }

                BaseBuildingDefinition definition = view.Definition;
                BaseBuildingSnapshot saved = _base.GetState(definition.Id);
                BaseBuildingState state = _base.GetDisplayState(definition, now);
                bool building = saved.IsBuilding;

                indicator.BindLevel(saved.Level, !building && _base.CanStartNow(definition, now));

                if (building)
                {
                    indicator.BindTimer(saved.GetProgress01(now), FormatDuration(saved.GetRemaining(now)));
                    indicator.BindBubble(null, null, null);
                    continue;
                }

                indicator.BindTimer(-1f, null);

                // Bubble priority after a running build: resources to collect, then "you can build here".
                int pending = _base.GetPendingProduction(definition, now, out string materialId);
                if (pending > 0)
                {
                    string id = definition.Id;
                    indicator.BindBubble(GetItemIcon(materialId) ?? _productionIcon, "+" + CurrencyFormatter.Format(pending),
                        () => _base.TryCollectProduction(id, DateTime.UtcNow));
                }
                else if (state == BaseBuildingState.Available)
                {
                    BaseBuildingView captured = view;
                    indicator.BindBubble(_buildBubbleIcon, null, () => OpenPopup(captured));
                }
                else
                {
                    indicator.BindBubble(null, null, null);
                }
            }
        }

        /// <summary>Every frame while the base is up: glue each widget to its building's anchor on screen.</summary>
        private void PositionIndicators()
        {
            Camera camera = _cameraController != null ? _cameraController.Camera : null;
            if (camera == null || _indicatorLayer == null)
            {
                return;
            }

            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < _buildingViews.Length && i < _indicators.Count; i++)
            {
                BaseBuildingView view = _buildingViews[i];
                BaseBuildingIndicatorView indicator = _indicators[i];
                if (view == null || indicator == null)
                {
                    continue;
                }

                Vector3 screen = camera.WorldToScreenPoint(view.IndicatorAnchor.position);
                bool visible = screen.z > 0f;
                if (indicator.gameObject.activeSelf != visible)
                {
                    indicator.gameObject.SetActive(visible);
                }

                if (!visible)
                {
                    continue;
                }

                // Overlay canvas: no camera for the conversion.
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_indicatorLayer, screen, null, out Vector2 local))
                {
                    indicator.Rect.anchoredPosition = local;
                }

                indicator.TickVisuals(dt);
            }
        }

        // ------------------------------------------------------------------ Popup

        private void HandleWorldTap(Vector2 screenPosition)
        {
            if (_popup != null && _popup.IsOpen)
            {
                return;
            }

            Camera camera = _cameraController != null ? _cameraController.Camera : null;
            if (camera == null)
            {
                return;
            }

            Ray ray = camera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 1000f, _buildingLayers, QueryTriggerInteraction.Collide))
            {
                return;
            }

            BaseBuildingView view = hit.collider.GetComponentInParent<BaseBuildingView>();
            if (view != null)
            {
                OpenPopup(view);
            }
        }

        private void OpenPopup(BaseBuildingView view)
        {
            if (_popup == null || view == null || view.Definition == null || _base == null)
            {
                return;
            }

            _selected = view;
            RefreshPopup(DateTime.UtcNow);
            _popup.Open();
            _audio?.PlaySfx(_openClip);
        }

        private void ClosePopup()
        {
            _selected = null;
            _popup?.Close();
        }

        private void RefreshPopup(DateTime now)
        {
            if (_selected == null || _base == null || _popup == null)
            {
                return;
            }

            BaseBuildingDefinition definition = _selected.Definition;
            BaseBuildingSnapshot saved = _base.GetState(definition.Id);
            int level = saved.Level;
            bool building = saved.IsBuilding;

            definition.TryGetLevel(level, out BuildingLevelDefinition current);
            bool hasNext = definition.TryGetNextLevel(level, out BuildingLevelDefinition next);

            _popup.SetHeader(level > 0 ? $"{definition.DisplayName} Lvl {level}" : definition.DisplayName,
                definition.Description);

            bool isProducer = definition.Type == BaseBuildingType.Production;

            // Upgrade bonus (non-producers): current value with the next level's gain beside it, as in "100 +90".
            _popup.BeginBonus("Upgrade bonus");
            if (!isProducer)
            {
                AddStatRows(current, next, level, hasNext, bonusSection: true);
            }

            _popup.EndBonus();

            // Extraction (producers): production, building power and production time together.
            _popup.BeginProduction();
            if (isProducer)
            {
                BuildingLevelDefinition shown = level > 0 ? current : next;
                if (shown != null && shown.Production != null && shown.Production.IsActive)
                {
                    _popup.AddProduction(GetItemIcon(shown.Production.MaterialId) ?? _productionIcon, "Production",
                        shown.Production.AmountPerInterval.ToString(CultureInfo.InvariantCulture));
                    AddStatRows(current, next, level, hasNext, bonusSection: false);
                    _popup.AddProduction(_timeIcon, "Production time",
                        FormatDuration(TimeSpan.FromSeconds(shown.Production.IntervalSeconds)));
                }
            }

            _popup.EndProduction();

            // Requirements - only while there is something to start.
            _popup.BeginRequirements();
            if (hasNext && !building)
            {
                List<RequirementStatus> statuses = _base.Requirements.Evaluate(next);
                for (int i = 0; i < statuses.Count; i++)
                {
                    RequirementStatus status = statuses[i];
                    _popup.AddRequirement(ResolveRequirementIcon(status), FormatRequirement(status), status.Satisfied,
                        BuildGoAction(status));
                }
            }

            _popup.EndRequirements();

            // Buttons.
            _popup.SetMaxLevel(!hasNext && !building);

            if (building)
            {
                int cost = _base.GetFinishNowGemCost(definition.Id, now);
                _popup.SetStart(true, false, FormatDuration(saved.GetRemaining(now)));
                _popup.SetFinish(cost > 0, _profile != null && _profile.Gems >= cost, $"Finish  {cost}");
                return;
            }

            if (!hasNext)
            {
                _popup.SetStart(false, false, string.Empty);
                _popup.SetFinish(false, false, string.Empty);
                return;
            }

            bool requirementsMet = _base.Requirements.AreAllSatisfied(next);
            bool busy = _base.IsBuilderBusy(now);
            string verb = level > 0 ? "Upgrade" : "Start";
            string startLabel = busy ? "Builder Busy" : $"{verb} {FormatDuration(TimeSpan.FromSeconds(next.ConstructionSeconds))}";
            _popup.SetStart(true, requirementsMet && !busy, startLabel);

            int instantCost = _base.GetGemCostForSeconds(next.ConstructionSeconds);
            _popup.SetFinish(instantCost > 0, requirementsMet && !busy && _profile != null && _profile.Gems >= instantCost,
                $"Finish  {instantCost}");
        }

        /// <summary>One row per bonus stat, showing the value the player has now and what the next level adds.</summary>
        private void AddStatRows(BuildingLevelDefinition current, BuildingLevelDefinition next, int level, bool hasNext,
            bool bonusSection)
        {
            BuildingLevelDefinition source = level > 0 ? current : next;
            if (source == null)
            {
                return;
            }

            BuildingStat[] stats = source.Bonuses;
            for (int i = 0; i < stats.Length; i++)
            {
                BuildingStat stat = stats[i];
                if (stat == null)
                {
                    continue;
                }

                string delta = null;
                if (level > 0 && hasNext && next != null)
                {
                    float nextValue = FindStat(next, stat.StatId);
                    float diff = nextValue - stat.Value;
                    if (Mathf.Abs(diff) > 0.001f)
                    {
                        delta = (diff > 0 ? "+" : string.Empty) + FormatNumber(diff, stat.IsPercent);
                    }
                }

                Sprite icon = stat.Icon != null ? stat.Icon : _powerIcon;
                string value = FormatNumber(stat.Value, stat.IsPercent);

                if (bonusSection)
                {
                    _popup.AddBonus(icon, stat.DisplayName, value, delta);
                }
                else
                {
                    _popup.AddProduction(icon, stat.DisplayName, delta != null ? value + "  " + delta : value);
                }
            }
        }

        private static float FindStat(BuildingLevelDefinition level, string statId)
        {
            BuildingStat[] stats = level.Bonuses;
            for (int i = 0; i < stats.Length; i++)
            {
                if (stats[i] != null && stats[i].StatId == statId)
                {
                    return stats[i].Value;
                }
            }

            return 0f;
        }

        private Sprite ResolveRequirementIcon(RequirementStatus status)
        {
            if (status.Icon != null)
            {
                return status.Icon;
            }

            return status.Requirement != null && status.Requirement.Type == BuildingRequirementType.CampaignLevel
                ? _campaignIcon
                : null;
        }

        private static string FormatRequirement(RequirementStatus status)
        {
            switch (status.Requirement.Type)
            {
                case BuildingRequirementType.Material:
                case BuildingRequirementType.Currency:
                case BuildingRequirementType.Gems:
                    return $"{CurrencyFormatter.Format(status.Owned)}/{CurrencyFormatter.Format(status.Required)}";
                default:
                    return status.DisplayLabel;
            }
        }

        private Action BuildGoAction(RequirementStatus status)
        {
            if (!status.HasNavigation || status.Satisfied)
            {
                return null;
            }

            string target = status.Requirement.TargetId;
            switch (status.Requirement.Type)
            {
                case BuildingRequirementType.BuildingLevel:
                    return () => FocusBuilding(target);
                case BuildingRequirementType.CampaignLevel:
                    return () =>
                    {
                        ClosePopup();
                        _menuShell?.SwitchTab(MenuTab.Play);
                    };
                default:
                    return null;
            }
        }

        private void FocusBuilding(string buildingId)
        {
            ClosePopup();
            for (int i = 0; i < _buildingViews.Length; i++)
            {
                BaseBuildingView view = _buildingViews[i];
                if (view != null && view.Definition != null && view.Definition.Id == buildingId)
                {
                    _cameraController?.FocusOn(view.transform.position);
                    view.PlayHighlight();
                    return;
                }
            }
        }

        private void HandleStart()
        {
            if (_selected == null || _base == null)
            {
                return;
            }

            BaseProgressionService.StartResult result = _base.TryStartConstruction(_selected.Definition.Id, DateTime.UtcNow);
            if (result == BaseProgressionService.StartResult.Started)
            {
                _audio?.PlaySfx(_startClip);
                ClosePopup();
            }
            else
            {
                RefreshPopup(DateTime.UtcNow);
            }
        }

        private void HandleFinish()
        {
            if (_selected == null || _base == null)
            {
                return;
            }

            string id = _selected.Definition.Id;
            DateTime now = DateTime.UtcNow;
            bool done = _base.GetState(id).IsBuilding ? _base.TryFinishNow(id, now) : _base.TryBuildInstantly(id, now);

            if (done)
            {
                ClosePopup();
            }
            else
            {
                RefreshPopup(now);
            }
        }

        // ------------------------------------------------------------------ Events

        /// <summary>A build was just started while the player watches: the construction site pops up. The service
        /// raises Changed right after this, and RefreshAll then switches the rest of the plot's state.</summary>
        private void HandleBuildingStarted(BaseBuildingDefinition definition, int targetLevel)
        {
            BaseBuildingView view = FindView(definition);
            view?.PlayConstructionStart();
        }

        /// <summary>The one visual completion path. BaseProgressionService raises BuildingCompleted from its single
        /// CompleteConstruction method, for the timer running out (ResolveFinishedConstructions) and for Finish
        /// (TryFinishNow / TryBuildInstantly) alike - so both look and behave identically here.</summary>
        private void HandleBuildingCompleted(BaseBuildingDefinition definition, int newLevel)
        {
            BaseBuildingView completed = FindView(definition);
            if (completed != null)
            {
                // Apply first so the pop plays on the NEW model, not on the scaffolding.
                completed.Apply(BaseBuildingState.Built, newLevel);
                completed.PlayCompletionPunch();
                Vector3 ground = completed.transform.position;
                _completionFx?.Play(ground, Mathf.Max(2f, completed.IndicatorAnchor.position.y - ground.y - 1f));
            }

            int delta = definition.TryGetLevel(newLevel, out BuildingLevelDefinition level) ? level.ForceReward : 0;
            int total = PlayerPowerCalculator.Compute(_profile, _services?.TowerCatalog, _base);
            _banner?.Enqueue(total, delta, $"{definition.DisplayName} Lvl {newLevel}");
            _audio?.PlaySfx(_completeClip);

            if (_shownGoal != null && _shownGoal.BuildingId == definition.Id && newLevel >= _shownGoal.Level)
            {
                _questTracker?.Bind(GoalText(_shownGoal, definition, 1), null);
                _questTracker?.PlayCompleted();
                _questHoldUntil = Time.unscaledTime + 1.6f;
            }
        }

        private BaseBuildingView FindView(BaseBuildingDefinition definition)
        {
            for (int i = 0; i < _buildingViews.Length; i++)
            {
                if (_buildingViews[i] != null && _buildingViews[i].Definition == definition)
                {
                    return _buildingViews[i];
                }
            }

            return null;
        }

        private void HandleResourceCollected(BaseBuildingDefinition definition, string materialId, int amount)
        {
            _audio?.PlaySfx(_collectClip);
        }

        // ------------------------------------------------------------------ Quest

        private void RefreshQuest()
        {
            if (_questTracker == null || _base == null || _questHoldUntil > 0f)
            {
                return;
            }

            if (!_base.TryGetCurrentGoal(out BaseBuildingGoal goal, out BaseBuildingDefinition definition))
            {
                _shownGoal = null;
                _questTracker.Bind(null, null);
                return;
            }

            _shownGoal = goal;
            string id = definition.Id;
            _questTracker.Bind(GoalText(goal, definition, 0), () => FocusBuilding(id));
        }

        private static string GoalText(BaseBuildingGoal goal, BaseBuildingDefinition definition, int done)
        {
            string label = !string.IsNullOrWhiteSpace(goal.LabelOverride)
                ? goal.LabelOverride
                : $"Build {definition.DisplayName} lvl. {goal.Level}";
            return $"{label} ({done}/1)";
        }

        // ------------------------------------------------------------------ Formatting

        private static string FormatDuration(TimeSpan span)
        {
            if (span.TotalSeconds < 60d)
            {
                return Mathf.CeilToInt((float)span.TotalSeconds) + "s";
            }

            if (span.TotalHours < 1d)
            {
                return $"{span.Minutes}m {span.Seconds:00}s";
            }

            return $"{(int)span.TotalHours}h {span.Minutes:00}m";
        }

        private static string FormatNumber(float value, bool isPercent)
        {
            string number = Mathf.Approximately(value, Mathf.Round(value))
                ? Mathf.RoundToInt(value).ToString(CultureInfo.InvariantCulture)
                : value.ToString("0.#", CultureInfo.InvariantCulture);
            return isPercent ? number + "%" : number;
        }
    }
}
