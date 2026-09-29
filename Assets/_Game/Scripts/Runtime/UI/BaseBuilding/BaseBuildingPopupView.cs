using AlienDefense.UI.Base;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.BaseBuilding
{
    /// <summary>The building detail / build / upgrade popup: title, Details, an optional Upgrade Bonus section, an
    /// optional Extraction section, Requirements, and the Start / Finish buttons.
    ///
    /// A dumb view: every string and every enabled flag comes from BaseWorldPresenter, which in turn reads them
    /// from the building's level data. Nothing here knows what a Sawmill is. Section rows are pooled, so the row
    /// count follows the data (a building with three bonuses draws three rows) without rebuilding anything.</summary>
    public sealed class BaseBuildingPopupView : MonoBehaviour
    {
        [Header("Frame")]
        [SerializeField]
        private GameObject _root;

        [SerializeField]
        [Tooltip("Dark full-screen overlay behind the panel.")]
        private CanvasGroup _dim;

        [SerializeField]
        private RectTransform _panel;

        [SerializeField]
        private TMP_Text _titleText;

        [SerializeField]
        private Button _closeButton;

        [SerializeField]
        [Tooltip("Optional: a button on the dim overlay so tapping outside the panel closes the popup.")]
        private Button _dimCloseButton;

        [Header("Details")]
        [SerializeField]
        private TMP_Text _descriptionText;

        [Header("Bonus section")]
        [SerializeField]
        private GameObject _bonusSection;

        [SerializeField]
        private TMP_Text _bonusHeader;

        [SerializeField]
        private Transform _bonusRows;

        [Header("Production section")]
        [SerializeField]
        private GameObject _productionSection;

        [SerializeField]
        private Transform _productionRows;

        [Header("Requirements section")]
        [SerializeField]
        private GameObject _requirementsSection;

        [SerializeField]
        private Transform _requirementRows;

        [Header("Templates")]
        [SerializeField]
        private BaseStatRowView _statRowTemplate;

        [SerializeField]
        private BaseRequirementRowView _requirementRowTemplate;

        [Header("Actions")]
        [SerializeField]
        private Button _startButton;

        [SerializeField]
        private TMP_Text _startLabel;

        [SerializeField]
        private Button _finishButton;

        [SerializeField]
        private TMP_Text _finishLabel;

        [SerializeField]
        [Tooltip("Shown instead of the buttons at max level.")]
        private GameObject _maxLevelLabel;

        [Header("Auto height")]
        [SerializeField]
        [Tooltip("Layout group holding the description and sections. When set, the panel grows or shrinks to fit " +
            "it, so a building with one bonus row does not leave half the panel empty.")]
        private RectTransform _body;

        [SerializeField, Min(0f)]
        [Tooltip("Panel height that is not the body: header, footer buttons and paddings.")]
        private float _panelChrome = 350f;

        [SerializeField, Min(0f)]
        private float _minPanelHeight = 760f;

        private int _fittedSignature = -1;
        private string _fittedDescription;

        private UiViewPool<BaseStatRowView> _bonusPool;
        private UiViewPool<BaseStatRowView> _productionPool;
        private UiViewPool<BaseRequirementRowView> _requirementPool;
        private Sequence _openSequence;

        public System.Action CloseRequested;
        public System.Action StartRequested;
        public System.Action FinishRequested;

        public bool IsOpen => _root != null && _root.activeSelf;

        private void Awake()
        {
            if (_statRowTemplate != null)
            {
                _statRowTemplate.gameObject.SetActive(false);
                _bonusPool = new UiViewPool<BaseStatRowView>(_statRowTemplate, _bonusRows);
                _productionPool = new UiViewPool<BaseStatRowView>(_statRowTemplate, _productionRows);
            }

            if (_requirementRowTemplate != null)
            {
                _requirementRowTemplate.gameObject.SetActive(false);
                _requirementPool = new UiViewPool<BaseRequirementRowView>(_requirementRowTemplate, _requirementRows);
            }

            _closeButton?.onClick.AddListener(() => CloseRequested?.Invoke());
            _dimCloseButton?.onClick.AddListener(() => CloseRequested?.Invoke());
            _startButton?.onClick.AddListener(() => StartRequested?.Invoke());
            _finishButton?.onClick.AddListener(() => FinishRequested?.Invoke());
        }

        private void OnDestroy()
        {
            _openSequence?.Kill();
            _closeButton?.onClick.RemoveAllListeners();
            _dimCloseButton?.onClick.RemoveAllListeners();
            _startButton?.onClick.RemoveAllListeners();
            _finishButton?.onClick.RemoveAllListeners();
        }

        public void Open()
        {
            if (_root == null)
            {
                return;
            }

            bool wasOpen = _root.activeSelf;
            _root.SetActive(true);

            // The presenter fills the popup before opening it, while its layout groups were still inactive and
            // measured nothing - measure again now that they are live.
            _fittedSignature = -1;
            FitPanel(_maxLevelLabel != null && _maxLevelLabel.activeSelf);

            if (wasOpen)
            {
                return;
            }

            // Unscaled: the popup may open while something else has paused time.
            _openSequence?.Kill();
            _openSequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
            if (_dim != null)
            {
                _dim.alpha = 0f;
                _openSequence.Join(DOTween.To(() => _dim.alpha, a => _dim.alpha = a, 1f, 0.18f));
            }

            if (_panel != null)
            {
                _panel.localScale = Vector3.one * 0.9f;
                _openSequence.Join(_panel.DOScale(1f, 0.22f).SetEase(Ease.OutBack));
            }
        }

        public void Close()
        {
            _openSequence?.Kill();
            if (_root != null)
            {
                _root.SetActive(false);
            }
        }

        public void SetHeader(string title, string description)
        {
            if (_titleText != null)
            {
                _titleText.text = title;
            }

            if (_descriptionText != null)
            {
                _descriptionText.text = description;
            }
        }

        // ------------------------------------------------------------------ Sections

        public void BeginBonus(string header)
        {
            if (_bonusHeader != null)
            {
                _bonusHeader.text = header;
            }

            _bonusPool?.Begin();
        }

        public void AddBonus(Sprite icon, string label, string value, string delta)
        {
            _bonusPool?.Take()?.Bind(icon, label, value, delta);
        }

        public void EndBonus()
        {
            _bonusPool?.End();
            SetActive(_bonusSection, _bonusPool != null && _bonusPool.ActiveCount > 0);
        }

        public void BeginProduction()
        {
            _productionPool?.Begin();
        }

        public void AddProduction(Sprite icon, string label, string value)
        {
            _productionPool?.Take()?.Bind(icon, label, value, null);
        }

        public void EndProduction()
        {
            _productionPool?.End();
            SetActive(_productionSection, _productionPool != null && _productionPool.ActiveCount > 0);
        }

        public void BeginRequirements()
        {
            _requirementPool?.Begin();
        }

        public void AddRequirement(Sprite icon, string label, bool satisfied, System.Action go)
        {
            _requirementPool?.Take()?.Bind(icon, label, satisfied, go);
        }

        public void EndRequirements()
        {
            _requirementPool?.End();
            SetActive(_requirementsSection, _requirementPool != null && _requirementPool.ActiveCount > 0);
        }

        // ------------------------------------------------------------------ Buttons

        public void SetStart(bool visible, bool interactable, string label)
        {
            if (_startButton != null)
            {
                _startButton.gameObject.SetActive(visible);
                _startButton.interactable = interactable;
            }

            if (_startLabel != null)
            {
                _startLabel.text = label;
            }
        }

        public void SetFinish(bool visible, bool interactable, string label)
        {
            if (_finishButton != null)
            {
                _finishButton.gameObject.SetActive(visible);
                _finishButton.interactable = interactable;
            }

            if (_finishLabel != null)
            {
                _finishLabel.text = label;
            }
        }

        /// <summary>Called last in every refresh, so it is also where the panel height is fitted to the content.</summary>
        public void SetMaxLevel(bool isMax)
        {
            SetActive(_maxLevelLabel, isMax);
            FitPanel(isMax);
        }

        /// <summary>Re-measures only when the content shape changed (row counts, max-level label, description) -
        /// the popup refreshes four times a second for its timer and must not rebuild layout every time.</summary>
        private void FitPanel(bool isMax)
        {
            if (_body == null || _panel == null)
            {
                return;
            }

            int signature = (_bonusPool != null ? _bonusPool.ActiveCount : 0)
                + (_productionPool != null ? _productionPool.ActiveCount : 0) * 16
                + (_requirementPool != null ? _requirementPool.ActiveCount : 0) * 256
                + (isMax ? 4096 : 0);
            string description = _descriptionText != null ? _descriptionText.text : null;
            if (signature == _fittedSignature && ReferenceEquals(description, _fittedDescription))
            {
                return;
            }

            _fittedSignature = signature;
            _fittedDescription = description;

            LayoutRebuilder.ForceRebuildLayoutImmediate(_body);
            float height = Mathf.Max(_minPanelHeight, LayoutUtility.GetPreferredHeight(_body) + _panelChrome);
            _panel.sizeDelta = new Vector2(_panel.sizeDelta.x, height);
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }
    }
}
