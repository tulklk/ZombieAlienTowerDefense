using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using AlienDefense.Meta;

namespace AlienDefense.UI.MainMenu
{
    /// <summary>Speech-bubble above an objective chest showing reward icons/amounts (+ optional Claim).</summary>
    public sealed class ObjectiveRewardPreviewBubbleView : MonoBehaviour
    {
        [SerializeField]
        private RectTransform _root;

        [SerializeField]
        private RectTransform _panel;

        [SerializeField]
        private Transform _itemsRow;

        [SerializeField]
        private GameObject _itemPrefab;

        [SerializeField]
        private Button _claimButton;

        [SerializeField]
        private GameObject _claimButtonRoot;

        [SerializeField]
        private Button _dismissBlocker;

        [SerializeField]
        private MetaItemCatalog _itemCatalog;

        [SerializeField]
        private Sprite _mysteryIcon;

        private readonly List<GameObject> _spawned = new List<GameObject>();
        private Action _onClaim;
        private LevelObjectiveKind _activeKind;
        private bool _isOpen;

        public bool IsOpen => _isOpen;
        public LevelObjectiveKind ActiveKind => _activeKind;

        private void Awake()
        {
            if (_claimButton != null)
            {
                _claimButton.onClick.AddListener(HandleClaimClicked);
            }

            if (_dismissBlocker != null)
            {
                _dismissBlocker.onClick.AddListener(Hide);
            }
        }

        private void OnDestroy()
        {
            if (_claimButton != null)
            {
                _claimButton.onClick.RemoveListener(HandleClaimClicked);
            }

            if (_dismissBlocker != null)
            {
                _dismissBlocker.onClick.RemoveListener(Hide);
            }
        }

        public void Configure(MetaItemCatalog catalog, Sprite mysteryIcon)
        {
            _itemCatalog = catalog;
            _mysteryIcon = mysteryIcon;
        }

        public void Show(
            RectTransform anchor,
            LevelObjectiveKind kind,
            ObjectiveRewardPreviewEntry[] entries,
            bool showClaim,
            Action onClaim)
        {
            if (anchor == null || entries == null || entries.Length == 0)
            {
                Hide();
                return;
            }

            _activeKind = kind;
            _onClaim = onClaim;
            _isOpen = true;
            gameObject.SetActive(true);
            EnsureOverlayCanvas();
            transform.SetAsLastSibling();

            PositionAbove(anchor);

            ClearItems();
            if (_itemsRow != null && _itemPrefab != null)
            {
                for (int i = 0; i < entries.Length; i++)
                {
                    ObjectiveRewardPreviewEntry entry = entries[i];
                    GameObject go = Instantiate(_itemPrefab, _itemsRow);
                    go.SetActive(true);
                    BindItem(go, entry);
                    _spawned.Add(go);
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
        }

        public void Hide()
        {
            _isOpen = false;
            _onClaim = null;
            ClearItems();
            gameObject.SetActive(false);
        }

        private void HandleClaimClicked()
        {
            Action claim = _onClaim;
            Hide();
            claim?.Invoke();
        }

        private void PositionAbove(RectTransform anchor)
        {
            RectTransform content = _root != null ? _root : transform as RectTransform;
            if (content == null || anchor == null)
            {
                return;
            }

            RectTransform parentRt = content.parent as RectTransform;
            if (parentRt == null)
            {
                content.position = anchor.position + Vector3.up * 120f;
                return;
            }

            Canvas canvas = parentRt.GetComponentInParent<Canvas>();
            Camera cam = null;
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                cam = canvas.worldCamera;
            }

            Vector3 worldTop = anchor.TransformPoint(new Vector3(0f, anchor.rect.height * 0.5f, 0f));
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, worldTop);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRt, screen, cam, out Vector2 local))
            {
                content.anchoredPosition = local + new Vector2(0f, 70f);
            }
        }

        private void EnsureOverlayCanvas()
        {
            // Layering: dismiss (250) < objective chests (260) < bubble content (300)
            // so the bubble is visible, chests stay tappable to switch, and outside tap still hits dismiss.
            Canvas rootCanvas = gameObject.GetComponent<Canvas>();
            if (rootCanvas == null)
            {
                rootCanvas = gameObject.AddComponent<Canvas>();
            }

            rootCanvas.overrideSorting = true;
            rootCanvas.sortingOrder = 250;
            if (gameObject.GetComponent<GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }

            if (_root != null)
            {
                Canvas contentCanvas = _root.GetComponent<Canvas>();
                if (contentCanvas == null)
                {
                    contentCanvas = _root.gameObject.AddComponent<Canvas>();
                }

                contentCanvas.overrideSorting = true;
                contentCanvas.sortingOrder = 300;
                if (_root.GetComponent<GraphicRaycaster>() == null)
                {
                    _root.gameObject.AddComponent<GraphicRaycaster>();
                }
            }
        }

        private void BindItem(GameObject go, ObjectiveRewardPreviewEntry entry)
        {
            Image icon = null;
            TMP_Text amount = null;
            Transform iconT = go.transform.Find("Icon");
            Transform amountT = go.transform.Find("Amount");
            if (iconT != null)
            {
                icon = iconT.GetComponent<Image>();
            }

            if (amountT != null)
            {
                amount = amountT.GetComponent<TMP_Text>();
            }

            if (icon == null)
            {
                icon = go.GetComponentInChildren<Image>(true);
            }

            if (amount == null)
            {
                amount = go.GetComponentInChildren<TMP_Text>(true);
            }

            if (amount != null)
            {
                amount.text = CurrencyFormatter.Format(entry.Amount);
            }

            if (icon != null)
            {
                if (entry.IsMystery)
                {
                    icon.sprite = _mysteryIcon;
                    icon.enabled = _mysteryIcon != null;
                    if (_mysteryIcon == null && amount != null)
                    {
                        amount.text = "?  " + amount.text;
                    }
                }
                else if (_itemCatalog != null && _itemCatalog.TryGet(entry.ItemId, out MetaItemDefinition def) && def != null)
                {
                    icon.sprite = def.Icon;
                    icon.enabled = def.Icon != null;
                    icon.color = Color.white;
                }
                else
                {
                    icon.enabled = false;
                }
            }
        }

        private void ClearItems()
        {
            for (int i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] == null)
                {
                    continue;
                }

                UnityEngine.Object.DestroyImmediate(_spawned[i]);
            }

            _spawned.Clear();

            if (_itemsRow == null)
            {
                return;
            }

            for (int i = _itemsRow.childCount - 1; i >= 0; i--)
            {
                Transform child = _itemsRow.GetChild(i);
                if (child != null)
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
            }
        }
    }
}
