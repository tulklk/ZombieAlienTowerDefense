using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using AlienDefense.Meta;

namespace AlienDefense.UI.MainMenu
{
    /// <summary>YOUR REWARDS fullscreen overlay + nested item detail popup.</summary>
    public sealed class ObjectiveRewardOverlayView : MonoBehaviour
    {
        [SerializeField]
        private CanvasGroup _blockerGroup;

        [SerializeField]
        private Button _blockerButton;

        [SerializeField]
        private RectTransform _contentRoot;

        [SerializeField]
        private TMP_Text _titleText;

        [SerializeField]
        private Transform _rewardsGrid;

        [SerializeField]
        private ObjectiveRewardItemView _itemPrefab;

        [SerializeField]
        private GameObject _detailRoot;

        [SerializeField]
        private Image _detailHeader;

        [SerializeField]
        private TMP_Text _detailTitle;

        [SerializeField]
        private Image _detailIcon;

        [SerializeField]
        private TMP_Text _detailAmount;

        [SerializeField]
        private TMP_Text _detailDescription;

        [SerializeField]
        private Button _detailCloseButton;

        [SerializeField]
        private MetaItemCatalog _itemCatalog;

        [SerializeField]
        private Sprite _coinsIcon;

        [SerializeField]
        private Sprite _gemsIcon;

        private readonly List<ObjectiveRewardItemView> _spawned = new List<ObjectiveRewardItemView>();
        private bool _detailOpen;
        private bool _rewardsGridMode;

        private void Awake()
        {
            if (_blockerButton != null)
            {
                _blockerButton.onClick.AddListener(HandleBlockerClicked);
            }

            if (_detailCloseButton != null)
            {
                _detailCloseButton.onClick.AddListener(HandleDetailCloseClicked);
            }

            // Do not deactivate here: first Show/ShowItemDetail SetActive(true) would re-enter Awake and hide itself.
            HideDetail();
            ClearItems();
            if (_contentRoot != null)
            {
                _contentRoot.gameObject.SetActive(true);
            }
        }

        private void OnDestroy()
        {
            if (_blockerButton != null)
            {
                _blockerButton.onClick.RemoveListener(HandleBlockerClicked);
            }

            if (_detailCloseButton != null)
            {
                _detailCloseButton.onClick.RemoveListener(HandleDetailCloseClicked);
            }
        }

        public void ConfigureCatalog(MetaItemCatalog catalog, Sprite coinsIcon, Sprite gemsIcon)
        {
            _itemCatalog = catalog;
            _coinsIcon = coinsIcon;
            _gemsIcon = gemsIcon;
        }

        public void Show(IReadOnlyList<GrantedObjectiveReward> rewards)
        {
            _rewardsGridMode = true;
            gameObject.SetActive(true);
            HideDetail();
            ClearItems();

            if (_titleText != null)
            {
                _titleText.text = "YOUR REWARDS";
            }

            if (_contentRoot != null)
            {
                _contentRoot.gameObject.SetActive(true);
            }

            if (rewards != null && _itemPrefab != null && _rewardsGrid != null)
            {
                for (int i = 0; i < rewards.Count; i++)
                {
                    ObjectiveRewardItemView item = Instantiate(_itemPrefab, _rewardsGrid);
                    item.gameObject.SetActive(true);
                    ResolveVisual(rewards[i], out Sprite icon, out string amount, out Color frame);
                    item.Bind(rewards[i], icon, amount, frame, ShowDetail);
                    item.transform.localScale = Vector3.zero;
                    item.transform.DOScale(1f, 0.25f).SetDelay(0.05f * i).SetEase(Ease.OutBack).SetUpdate(true);
                    _spawned.Add(item);
                }
            }

            if (_blockerGroup != null)
            {
                _blockerGroup.alpha = 0f;
                DOTween.To(() => _blockerGroup.alpha, x => _blockerGroup.alpha = x, 0.82f, 0.2f)
                    .SetUpdate(true);
            }

            if (_contentRoot != null)
            {
                _contentRoot.localScale = Vector3.one * 0.85f;
                _contentRoot.DOScale(1f, 0.25f).SetEase(Ease.OutBack).SetUpdate(true);
            }
        }

        /// <summary>Detail-only from bubble icon tap. Mystery never reveals the real item.</summary>
        public void ShowItemDetail(string itemId, int amount, bool isMystery)
        {
            _rewardsGridMode = false;
            gameObject.SetActive(true);
            ClearItems();

            if (_contentRoot != null)
            {
                _contentRoot.gameObject.SetActive(false);
            }

            if (_blockerGroup != null)
            {
                _blockerGroup.alpha = 0.82f;
            }

            if (isMystery)
            {
                Sprite mysteryIcon = null;
                if (_itemCatalog != null && _itemCatalog.TryGet(MetaItemIds.ResearchResource, out MetaItemDefinition mysteryDef))
                {
                    mysteryIcon = mysteryDef.Icon;
                }

                ApplyDetail(
                    "Mystery Reward",
                    mysteryIcon,
                    CurrencyFormatter.Format(amount),
                    new Color(0.45f, 0.35f, 0.75f, 1f),
                    "Complete the objective to reveal this reward.");
                return;
            }

            if (_itemCatalog != null && _itemCatalog.TryGet(itemId, out MetaItemDefinition def) && def != null)
            {
                ApplyDetail(def.DisplayName, def.Icon, CurrencyFormatter.Format(amount), def.HeaderColor, def.Description);
                return;
            }

            ApplyDetail(
                string.IsNullOrEmpty(itemId) ? "Reward" : itemId,
                null,
                CurrencyFormatter.Format(amount),
                new Color(0.2f, 0.7f, 0.95f, 1f),
                string.Empty);
        }

        public void Hide()
        {
            HideDetail();
            ClearItems();
            gameObject.SetActive(false);
            _rewardsGridMode = false;
        }

        private void HideImmediate()
        {
            HideDetail();
            ClearItems();
            gameObject.SetActive(false);
            _rewardsGridMode = false;
        }

        private void HandleBlockerClicked()
        {
            if (_detailOpen)
            {
                HideDetail();
                if (!_rewardsGridMode)
                {
                    Hide();
                }

                return;
            }

            Hide();
        }

        private void ShowDetail(GrantedObjectiveReward reward)
        {
            ResolveVisual(reward, out Sprite icon, out string amount, out Color header);
            ResolveText(reward, out string title, out string description);
            ApplyDetail(title, icon, amount, header, description);
        }

        private void ApplyDetail(string title, Sprite icon, string amount, Color header, string description)
        {
            if (_detailRoot != null)
            {
                _detailRoot.SetActive(true);
            }

            if (_detailHeader != null)
            {
                _detailHeader.color = header;
            }

            if (_detailTitle != null)
            {
                _detailTitle.text = title;
            }

            if (_detailIcon != null)
            {
                _detailIcon.sprite = icon;
                _detailIcon.enabled = icon != null;
                _detailIcon.preserveAspect = true;
            }

            if (_detailAmount != null)
            {
                _detailAmount.text = amount;
            }

            if (_detailDescription != null)
            {
                _detailDescription.text = description;
            }

            _detailOpen = true;
        }

        private void HandleDetailCloseClicked()
        {
            HideDetail();
            if (!_rewardsGridMode)
            {
                Hide();
            }
        }

        private void HideDetail()
        {
            if (_detailRoot != null)
            {
                _detailRoot.SetActive(false);
            }

            _detailOpen = false;
        }

        private void ClearItems()
        {
            for (int i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] != null)
                {
                    Destroy(_spawned[i].gameObject);
                }
            }

            _spawned.Clear();
        }

        private void ResolveVisual(GrantedObjectiveReward reward, out Sprite icon, out string amountLabel, out Color frameColor)
        {
            amountLabel = CurrencyFormatter.Format(reward.Amount);
            frameColor = new Color(0.2f, 0.7f, 0.95f, 1f);
            icon = null;

            switch (reward.Kind)
            {
                case ObjectiveRewardKind.Coins:
                    icon = _coinsIcon;
                    frameColor = new Color(1f, 0.85f, 0.2f, 1f);
                    break;
                case ObjectiveRewardKind.Gems:
                    icon = _gemsIcon;
                    frameColor = new Color(0.55f, 0.35f, 0.95f, 1f);
                    break;
                default:
                    if (_itemCatalog != null && _itemCatalog.TryGet(reward.ItemId, out MetaItemDefinition def) && def != null)
                    {
                        icon = def.Icon;
                        frameColor = def.HeaderColor;
                    }

                    break;
            }
        }

        private void ResolveText(GrantedObjectiveReward reward, out string title, out string description)
        {
            switch (reward.Kind)
            {
                case ObjectiveRewardKind.Coins:
                    title = "Coins";
                    description = "Meta currency used for tower unlocks and upgrades.";
                    return;
                case ObjectiveRewardKind.Gems:
                    title = "Gems";
                    description = "Premium currency for special purchases.";
                    return;
                default:
                    if (_itemCatalog != null && _itemCatalog.TryGet(reward.ItemId, out MetaItemDefinition def) && def != null)
                    {
                        title = def.DisplayName;
                        description = def.Description;
                        return;
                    }

                    title = string.IsNullOrEmpty(reward.ItemId) ? "Reward" : reward.ItemId;
                    description = string.Empty;
                    return;
            }
        }
    }
}
