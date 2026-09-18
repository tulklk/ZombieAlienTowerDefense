using System;
using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.Profile
{
    /// <summary>Dumb view for the Profile overlay. Presenter owns open/close and data binding.</summary>
    public sealed class ProfileView : MonoBehaviour
    {
        [SerializeField]
        private GameObject _root;

        [SerializeField]
        private CanvasGroup _canvasGroup;

        [SerializeField]
        private RectTransform _contentRoot;

        [SerializeField]
        private Button _backButton;

        [SerializeField]
        private Button _editNameButton;

        [SerializeField]
        private Button _editAvatarButton;

        [SerializeField]
        private Button _copyIdButton;

        [SerializeField]
        private Image _avatarImage;

        [SerializeField]
        private TMP_Text _playerNameText;

        [SerializeField]
        private TMP_Text _playerIdText;

        [SerializeField]
        private TMP_Text _levelText;

        [SerializeField]
        private TMP_Text _energyText;

        [SerializeField]
        private TMP_Text _energyTimerText;

        [SerializeField]
        private Image _energyFill;

        [SerializeField]
        private ProfileStatRowView _powerRow;

        [SerializeField]
        private ProfileStatRowView _towersRow;

        [SerializeField]
        private ProfileStatRowView _campaignRow;

        [SerializeField]
        private ProfileStatRowView _damageRow;

        [SerializeField]
        private ProfileStatRowView _killsRow;

        [SerializeField]
        private CanvasGroup _toastGroup;

        [SerializeField]
        private TMP_Text _toastText;

        [SerializeField]
        private EditNamePopupView _editNamePopup;

        [SerializeField]
        private RawImage _ufoPreviewImage;

        [SerializeField]
        private RectTransform _header;

        [SerializeField]
        private RectTransform _showcase;

        [SerializeField]
        private RectTransform _card;

        public event Action BackClicked;
        public event Action EditNameClicked;
        public event Action EditAvatarClicked;
        public event Action CopyIdClicked;

        public EditNamePopupView EditNamePopup => _editNamePopup;
        public RawImage UfoPreviewImage => _ufoPreviewImage;
        public bool IsVisible => _root != null && _root.activeSelf;

        public void SetShowcaseActive(bool active)
        {
            if (_showcase != null)
            {
                _showcase.gameObject.SetActive(active);
            }
        }

        public void Wire(
            GameObject root,
            CanvasGroup canvasGroup,
            RectTransform contentRoot,
            Button backButton,
            Button editNameButton,
            Button editAvatarButton,
            Button copyIdButton,
            Image avatarImage,
            TMP_Text playerNameText,
            TMP_Text playerIdText,
            TMP_Text levelText,
            TMP_Text energyText,
            TMP_Text energyTimerText,
            ProfileStatRowView powerRow,
            ProfileStatRowView towersRow,
            ProfileStatRowView campaignRow,
            ProfileStatRowView damageRow,
            ProfileStatRowView killsRow,
            CanvasGroup toastGroup,
            TMP_Text toastText,
            EditNamePopupView editNamePopup,
            RawImage ufoPreviewImage,
            RectTransform header,
            RectTransform showcase,
            RectTransform card)
        {
            _root = root;
            _canvasGroup = canvasGroup;
            _contentRoot = contentRoot;
            _backButton = backButton;
            _editNameButton = editNameButton;
            _editAvatarButton = editAvatarButton;
            _copyIdButton = copyIdButton;
            _avatarImage = avatarImage;
            _playerNameText = playerNameText;
            _playerIdText = playerIdText;
            _levelText = levelText;
            _energyText = energyText;
            _energyTimerText = energyTimerText;
            _powerRow = powerRow;
            _towersRow = towersRow;
            _campaignRow = campaignRow;
            _damageRow = damageRow;
            _killsRow = killsRow;
            _toastGroup = toastGroup;
            _toastText = toastText;
            _editNamePopup = editNamePopup;
            _ufoPreviewImage = ufoPreviewImage;
            _header = header;
            _showcase = showcase;
            _card = card;
            BindButtons();
        }

        private Tween _panelTween;
        private Coroutine _toastRoutine;

        private void Awake()
        {
            BindButtons();

            if (_toastGroup != null)
            {
                _toastGroup.alpha = 0f;
                _toastGroup.gameObject.SetActive(false);
            }

            if (_root != null)
            {
                _root.SetActive(false);
            }

            // Scene may serialize alpha=1 for Edit Mode visibility; force hidden at runtime.
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
            }
        }

        private void BindButtons()
        {
            if (_backButton != null)
            {
                _backButton.onClick.RemoveListener(InvokeBack);
                _backButton.onClick.AddListener(InvokeBack);
            }

            if (_editNameButton != null)
            {
                _editNameButton.onClick.RemoveListener(InvokeEditName);
                _editNameButton.onClick.AddListener(InvokeEditName);
            }

            if (_editAvatarButton != null)
            {
                _editAvatarButton.onClick.RemoveListener(InvokeEditAvatar);
                _editAvatarButton.onClick.AddListener(InvokeEditAvatar);
            }

            if (_copyIdButton != null)
            {
                _copyIdButton.onClick.RemoveListener(InvokeCopyId);
                _copyIdButton.onClick.AddListener(InvokeCopyId);
            }
        }

        private void InvokeBack() => BackClicked?.Invoke();
        private void InvokeEditName() => EditNameClicked?.Invoke();
        private void InvokeEditAvatar() => EditAvatarClicked?.Invoke();
        private void InvokeCopyId() => CopyIdClicked?.Invoke();

        private void OnDestroy()
        {
            _panelTween?.Kill();
        }

        public void SetIdentity(string displayName, string profileId, Sprite avatar)
        {
            if (_playerNameText != null)
            {
                _playerNameText.text = displayName ?? string.Empty;
            }

            if (_playerIdText != null)
            {
                string id = profileId ?? string.Empty;
                if (id.Length > 28)
                {
                    id = id.Substring(0, 28);
                }

                _playerIdText.text = "ID: " + id;
            }

            // Only assign when provided — never clear an existing scene Potrait.
            if (_avatarImage != null && avatar != null)
            {
                _avatarImage.sprite = avatar;
                _avatarImage.color = Color.white;
                _avatarImage.enabled = true;
            }
        }

        public Sprite CurrentAvatarSprite => _avatarImage != null ? _avatarImage.sprite : null;

        public void SetLevel(int level)
        {
            if (_levelText != null)
            {
                _levelText.text = "Level: " + level;
            }
        }

        public void SetEnergyUnavailable()
        {
            if (_energyText != null)
            {
                _energyText.text = "—";
            }

            if (_energyFill != null)
            {
                _energyFill.fillAmount = 0f;
            }

            if (_energyTimerText != null)
            {
                _energyTimerText.text = string.Empty;
                _energyTimerText.gameObject.SetActive(false);
            }
        }

        public void SetStats(string power, string towers, string campaign, string damage, string kills)
        {
            _powerRow?.Set("Power", power);
            _towersRow?.Set("Central Building", towers);
            _campaignRow?.Set("Campaign mission", campaign);
            _damageRow?.Set("Towers damage", damage);
            _killsRow?.Set("Zombies killed", kills);
        }

        public void ShowInstant()
        {
            if (_root != null)
            {
                _root.SetActive(true);
            }

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
                _canvasGroup.blocksRaycasts = true;
                _canvasGroup.interactable = true;
            }
        }

        public void HideInstant()
        {
            _panelTween?.Kill();
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.blocksRaycasts = false;
                _canvasGroup.interactable = false;
            }

            if (_root != null)
            {
                _root.SetActive(false);
            }
        }

        public void PlayOpenAnimation(Action onComplete = null)
        {
            if (_root != null)
            {
                _root.SetActive(true);
            }

            _panelTween?.Kill();
            if (_canvasGroup != null)
            {
                // Keep current alpha if already shown (ShowInstant failsafe); otherwise fade in.
                if (_canvasGroup.alpha < 0.99f)
                {
                    _canvasGroup.alpha = Mathf.Max(_canvasGroup.alpha, 0f);
                }

                _canvasGroup.blocksRaycasts = true;
                _canvasGroup.interactable = false;
            }

            float headerTargetY = _header != null ? _header.anchoredPosition.y : 0f;
            float showcaseTargetY = _showcase != null ? _showcase.anchoredPosition.y : 0f;
            float cardTargetY = _card != null ? _card.anchoredPosition.y : 0f;
            PrepareSlide(_header, -24f);
            PrepareSlide(_showcase, 20f);
            PrepareSlide(_card, 40f);

            Sequence seq = DOTween.Sequence().SetUpdate(true);
            if (_canvasGroup != null && _canvasGroup.alpha < 0.99f)
            {
                seq.Join(DOTween.To(() => _canvasGroup.alpha, a => _canvasGroup.alpha = a, 1f, 0.28f)
                    .SetEase(Ease.OutCubic));
            }

            seq.Join(SlideInTo(_header, headerTargetY, 0f, 0.22f));
            seq.Join(SlideInTo(_showcase, showcaseTargetY, 0.05f, 0.26f));
            seq.Join(SlideInTo(_card, cardTargetY, 0.1f, 0.3f));
            seq.OnComplete(() =>
            {
                if (_canvasGroup != null)
                {
                    _canvasGroup.alpha = 1f;
                    _canvasGroup.interactable = true;
                }

                onComplete?.Invoke();
            });
            _panelTween = seq;

            if (seq.Duration(false) <= 0f)
            {
                if (_canvasGroup != null)
                {
                    _canvasGroup.alpha = 1f;
                    _canvasGroup.interactable = true;
                }

                onComplete?.Invoke();
            }
        }

        public void PlayCloseAnimation(Action onComplete = null)
        {
            _panelTween?.Kill();
            if (_canvasGroup != null)
            {
                _canvasGroup.interactable = false;
            }

            Sequence seq = DOTween.Sequence().SetUpdate(true);
            if (_canvasGroup != null)
            {
                seq.Join(DOTween.To(() => _canvasGroup.alpha, a => _canvasGroup.alpha = a, 0f, 0.2f)
                    .SetEase(Ease.InCubic));
            }

            if (_contentRoot != null)
            {
                Vector2 start = _contentRoot.anchoredPosition;
                seq.Join(DOTween.To(() => _contentRoot.anchoredPosition, p => _contentRoot.anchoredPosition = p,
                    start + new Vector2(40f, 0f), 0.2f).SetEase(Ease.InCubic));
            }

            seq.OnComplete(() =>
            {
                if (_contentRoot != null)
                {
                    _contentRoot.anchoredPosition = Vector2.zero;
                }

                HideInstant();
                onComplete?.Invoke();
            });
            _panelTween = seq;
        }

        public void ShowToast(string message, float duration = 1.25f)
        {
            if (_toastGroup == null)
            {
                return;
            }

            if (_toastText != null)
            {
                _toastText.text = message ?? string.Empty;
            }

            if (_toastRoutine != null)
            {
                StopCoroutine(_toastRoutine);
            }

            _toastRoutine = StartCoroutine(ToastRoutine(duration));
        }

        private IEnumerator ToastRoutine(float duration)
        {
            _toastGroup.gameObject.SetActive(true);
            _toastGroup.alpha = 0f;
            yield return FadeToast(1f, 0.12f);
            yield return new WaitForSecondsRealtime(duration);
            yield return FadeToast(0f, 0.2f);
            _toastGroup.gameObject.SetActive(false);
            _toastRoutine = null;
        }

        private IEnumerator FadeToast(float target, float duration)
        {
            float start = _toastGroup.alpha;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                _toastGroup.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(t / duration));
                yield return null;
            }

            _toastGroup.alpha = target;
        }

        private static void PrepareSlide(RectTransform rect, float fromYOffset)
        {
            if (rect == null)
            {
                return;
            }

            Vector2 pos = rect.anchoredPosition;
            pos.y += fromYOffset;
            rect.anchoredPosition = pos;
        }

        private static Tween SlideIn(RectTransform rect, float delay, float duration)
        {
            if (rect == null)
            {
                return DOTween.Sequence();
            }

            Vector2 target = new Vector2(rect.anchoredPosition.x, 0f);
            return DOTween.To(() => rect.anchoredPosition, p => rect.anchoredPosition = p, target, duration)
                .SetDelay(delay)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true);
        }

        private static Tween SlideInTo(RectTransform rect, float targetY, float delay, float duration)
        {
            if (rect == null)
            {
                return DOTween.Sequence();
            }

            Vector2 target = new Vector2(rect.anchoredPosition.x, targetY);
            return DOTween.To(() => rect.anchoredPosition, p => rect.anchoredPosition = p, target, duration)
                .SetDelay(delay)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true);
        }
    }
}
