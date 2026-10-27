using System;
using System.Collections.Generic;
using AlienDefense.Shop;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.Shop
{
    /// <summary>The Shop's popup layer: purchase confirmation, "Purchase Successful!" (rays, panel scale-in, reward
    /// tiles popping in one by one) and errors. One popup at a time; reward tiles are pooled.</summary>
    public sealed class ShopPopupView : MonoBehaviour
    {
        [SerializeField]
        private Image _dim;

        [Header("Confirm")]
        [SerializeField]
        private RectTransform _confirmPanel;

        [SerializeField]
        private TMP_Text _confirmTitle;

        [SerializeField]
        private TMP_Text _confirmMessage;

        [SerializeField]
        private TMP_Text _confirmNote;

        [SerializeField]
        private Button _confirmButton;

        [SerializeField]
        private TMP_Text _confirmPrice;

        [SerializeField]
        private Button _cancelButton;

        [Header("Success")]
        [SerializeField]
        private RectTransform _successPanel;

        [SerializeField]
        private TMP_Text _successTitle;

        [SerializeField]
        private RectTransform _rays;

        [SerializeField]
        private Transform _rewardGrid;

        [SerializeField]
        private ShopRewardIconView _rewardTemplate;

        [SerializeField]
        private Button _collectButton;

        [Header("Error")]
        [SerializeField]
        private RectTransform _errorPanel;

        [SerializeField]
        private TMP_Text _errorMessage;

        [SerializeField]
        private Button _errorButton;

        private readonly List<ShopRewardIconView> _rewardViews = new List<ShopRewardIconView>();
        private Action _onConfirm;
        private Action _onClosed;

        public bool IsOpen => gameObject.activeSelf;

        private void Awake()
        {
            _confirmButton?.onClick.AddListener(() =>
            {
                Action confirm = _onConfirm;
                Close();
                confirm?.Invoke();
            });
            _cancelButton?.onClick.AddListener(Close);
            _collectButton?.onClick.AddListener(Close);
            _errorButton?.onClick.AddListener(Close);
        }

        public void ShowConfirm(string productName, string price, bool testStore, Action onConfirm)
        {
            _onConfirm = onConfirm;
            _onClosed = null;
            if (_confirmTitle != null) _confirmTitle.text = "Confirm Purchase";
            if (_confirmMessage != null) _confirmMessage.text = "Buy <color=#FFE35A>" + productName + "</color>?";
            if (_confirmPrice != null) _confirmPrice.text = price;
            if (_confirmNote != null)
            {
                _confirmNote.gameObject.SetActive(testStore);
                _confirmNote.text = "Test store - no real payment is made.";
            }

            Open(_confirmPanel);
        }

        public void ShowSuccess(string title, IReadOnlyList<ShopGrantedReward> rewards, ShopCatalog catalog, Action onClosed = null)
        {
            _onConfirm = null;
            _onClosed = onClosed;
            if (_successTitle != null) _successTitle.text = title;
            int shown = 0;
            for (int i = 0; i < rewards.Count; i++)
            {
                ShopRewardData reward = rewards[i].Reward;
                ShopRewardIconView view = GetRewardView(shown++);
                string amount = reward.Type == ShopRewardType.VipDays || reward.Type == ShopRewardType.PrivilegeDays
                    ? rewards[i].Amount + "d"
                    : ShopFormat.Compact(rewards[i].Amount);
                view.Bind(catalog.GetIcon(reward), amount, reward.Tag, ShopPackCardView.TileColor(reward.Type), catalog.GetName(reward));
                view.PlayPop(0.18f + i * 0.08f);
            }

            for (int i = shown; i < _rewardViews.Count; i++)
            {
                _rewardViews[i].gameObject.SetActive(false);
            }

            Open(_successPanel);
            if (_rays != null)
            {
                _rays.DOKill();
                _rays.localRotation = Quaternion.identity;
                _rays.DORotate(new Vector3(0f, 0f, -360f), 12f, RotateMode.FastBeyond360).SetLoops(-1).SetEase(Ease.Linear)
                    .SetUpdate(true).SetLink(_rays.gameObject);
            }
        }

        public void ShowError(string message)
        {
            _onConfirm = null;
            _onClosed = null;
            if (_errorMessage != null) _errorMessage.text = message;
            Open(_errorPanel);
        }

        public void Close()
        {
            if (!gameObject.activeSelf)
            {
                return;
            }

            _rays?.DOKill();
            gameObject.SetActive(false);
            Action closed = _onClosed;
            _onClosed = null;
            closed?.Invoke();
        }

        private void Open(RectTransform panel)
        {
            gameObject.SetActive(true);
            SetPanel(_confirmPanel, panel);
            SetPanel(_successPanel, panel);
            SetPanel(_errorPanel, panel);
            if (_dim != null)
            {
                _dim.DOKill();
                Color c = _dim.color;
                c.a = 0f;
                _dim.color = c;
                Image dim = _dim;
                DOTween.To(() => dim.color.a, a => { Color k = dim.color; k.a = a; dim.color = k; }, 0.7f, 0.15f)
                    .SetUpdate(true).SetLink(gameObject).SetTarget(dim);
            }

            if (panel != null)
            {
                panel.DOKill();
                panel.localScale = Vector3.one * 0.75f;
                panel.DOScale(1f, 0.25f).SetEase(Ease.OutBack).SetUpdate(true).SetLink(panel.gameObject);
            }
        }

        private static void SetPanel(RectTransform panel, RectTransform shown)
        {
            if (panel != null)
            {
                panel.gameObject.SetActive(panel == shown);
            }
        }

        private ShopRewardIconView GetRewardView(int index)
        {
            while (_rewardViews.Count <= index)
            {
                ShopRewardIconView view = Instantiate(_rewardTemplate, _rewardGrid);
                _rewardViews.Add(view);
            }

            _rewardViews[index].gameObject.SetActive(true);
            return _rewardViews[index];
        }
    }
}
