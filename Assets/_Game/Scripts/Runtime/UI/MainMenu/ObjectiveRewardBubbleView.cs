using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using AlienDefense.Meta;

namespace AlienDefense.UI.MainMenu
{
    /// <summary>
    /// Per-chest purple bubblepanel preview (child of Objective_0N).
    /// Uses fixed scene-authored RewardItem slots so Edit mode matches Play layout.
    /// </summary>
    public sealed class ObjectiveRewardBubbleView : MonoBehaviour
    {
        private const float OpenDuration = 0.22f;
        private const float CloseDuration = 0.15f;

        [SerializeField]
        private Image _bubbleBackground;

        [SerializeField]
        private RectTransform _rewardContainer;

        [SerializeField]
        private ObjectiveRewardItemView[] _itemSlots = System.Array.Empty<ObjectiveRewardItemView>();

        [SerializeField]
        private Button _claimButton;

        [SerializeField]
        private GameObject _claimButtonRoot;

        [SerializeField]
        private MetaItemCatalog _itemCatalog;

        [SerializeField]
        private Sprite _mysteryIcon;

        private Action _onClaim;
        private Action<string, int, bool> _onItemClicked;
        private Tween _pulseTween;
        private Tween _visibilityTween;
        private bool _isOpen;
        private bool _showClaimPulse;

        public bool IsOpen => _isOpen;

        private void Awake()
        {
            if (_claimButton != null)
            {
                _claimButton.onClick.AddListener(HandleClaimClicked);
            }

            if (_bubbleBackground != null)
            {
                _bubbleBackground.preserveAspect = false;
                _bubbleBackground.raycastTarget = true;
            }

            // Do not SetActive(false) here — first Show() would re-enter Awake and hide itself.
            EnsureSlotsFromChildren();
        }

        private void OnDestroy()
        {
            _pulseTween?.Kill();
            _visibilityTween?.Kill();
            if (_claimButton != null)
            {
                _claimButton.onClick.RemoveListener(HandleClaimClicked);
            }
        }

        public void Configure(MetaItemCatalog catalog, Sprite mysteryIcon)
        {
            _itemCatalog = catalog;
            _mysteryIcon = mysteryIcon;
        }

        public void SetItemSlots(ObjectiveRewardItemView[] slots)
        {
            _itemSlots = slots ?? System.Array.Empty<ObjectiveRewardItemView>();
        }

        public void SetItemClickedHandler(Action<string, int, bool> onItemClicked)
        {
            _onItemClicked = onItemClicked;
        }

        public void Show(
            ObjectiveRewardPreviewEntry[] entries,
            bool showClaim,
            Action onClaim)
        {
            if (entries == null || entries.Length == 0)
            {
                Hide();
                return;
            }

            EnsureSlotsFromChildren();

            _visibilityTween?.Kill();
            _pulseTween?.Kill();
            _pulseTween = null;

            _onClaim = onClaim;
            _showClaimPulse = showClaim;
            _isOpen = true;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();

            int count = Mathf.Min(entries.Length, _itemSlots.Length);
            for (int i = 0; i < _itemSlots.Length; i++)
            {
                ObjectiveRewardItemView slot = _itemSlots[i];
                if (slot == null)
                {
                    continue;
                }

                if (i < count)
                {
                    ObjectiveRewardPreviewEntry entry = entries[i];
                    slot.gameObject.SetActive(true);
                    Sprite icon = ResolveIcon(entry);
                    slot.BindPreview(icon, entry.Amount, entry.ItemId, entry.IsMystery, HandleItemClicked);
                }
                else
                {
                    slot.gameObject.SetActive(false);
                }
            }

            if (_claimButtonRoot != null)
            {
                _claimButtonRoot.SetActive(showClaim);
            }
            else if (_claimButton != null)
            {
                _claimButton.gameObject.SetActive(showClaim);
            }

            transform.localScale = Vector3.zero;
            _visibilityTween = transform.DOScale(1f, OpenDuration)
                .SetEase(Ease.OutBack)
                .SetUpdate(true)
                .OnComplete(StartClaimPulseIfNeeded);
        }

        public void Hide()
        {
            _pulseTween?.Kill();
            _pulseTween = null;
            _visibilityTween?.Kill();
            _visibilityTween = null;

            if (!_isOpen || !gameObject.activeSelf)
            {
                _isOpen = false;
                _onClaim = null;
                _showClaimPulse = false;
                transform.localScale = Vector3.one;
                HideSlotsOnly();
                if (gameObject.activeSelf)
                {
                    gameObject.SetActive(false);
                }

                return;
            }

            _isOpen = false;
            _onClaim = null;
            _showClaimPulse = false;

            _visibilityTween = transform.DOScale(0f, CloseDuration)
                .SetEase(Ease.InBack)
                .SetUpdate(true)
                .OnComplete(FinishHide);
        }

        private void StartClaimPulseIfNeeded()
        {
            if (!_isOpen || !_showClaimPulse)
            {
                return;
            }

            _pulseTween?.Kill();
            transform.localScale = Vector3.one;
            _pulseTween = transform.DOScale(1.03f, 1f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true);
        }

        private void FinishHide()
        {
            _visibilityTween = null;
            HideSlotsOnly();
            transform.localScale = Vector3.one;
            gameObject.SetActive(false);
        }

        private void HideSlotsOnly()
        {
            EnsureSlotsFromChildren();
            for (int i = 0; i < _itemSlots.Length; i++)
            {
                if (_itemSlots[i] != null)
                {
                    _itemSlots[i].gameObject.SetActive(false);
                }
            }
        }

        private void EnsureSlotsFromChildren()
        {
            if (_itemSlots != null && _itemSlots.Length > 0)
            {
                return;
            }

            if (_rewardContainer == null)
            {
                _itemSlots = System.Array.Empty<ObjectiveRewardItemView>();
                return;
            }

            _itemSlots = _rewardContainer.GetComponentsInChildren<ObjectiveRewardItemView>(true);
        }

        private void HandleClaimClicked()
        {
            Action claim = _onClaim;
            Hide();
            claim?.Invoke();
        }

        private void HandleItemClicked(string itemId, int amount, bool isMystery)
        {
            _onItemClicked?.Invoke(itemId, amount, isMystery);
        }

        private Sprite ResolveIcon(ObjectiveRewardPreviewEntry entry)
        {
            if (entry.IsMystery)
            {
                return _mysteryIcon;
            }

            if (_itemCatalog != null && _itemCatalog.TryGet(entry.ItemId, out MetaItemDefinition def) && def != null)
            {
                return def.Icon;
            }

            return null;
        }
    }
}
