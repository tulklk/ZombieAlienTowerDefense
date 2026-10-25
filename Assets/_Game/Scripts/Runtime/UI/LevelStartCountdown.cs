using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI
{
    /// <summary>The big "3 - 2 - 1" shown in the middle of the screen between the UFO intro and the start of combat,
    /// shared by every level (LevelCompositionRoot plays it; see its start sequence). Each number pops in
    /// (scale 0.4 -> 1.25 -> 1.0) and fades out; when the last one has faded the root hides itself and OnFinished
    /// fires exactly once per Play. Runs on unscaled time so the game-speed button cannot rush it. Built from the
    /// project's existing DOTween - no per-frame code.</summary>
    public sealed class LevelStartCountdown : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text _label;

        [SerializeField]
        [Tooltip("Shown only while counting (normally the label's own GameObject). Defaults to this GameObject.")]
        private GameObject _root;

        [SerializeField, Min(1)]
        private int _startNumber = 3;

        [Header("Per-number animation (seconds, unscaled)")]
        [SerializeField, Min(0.01f)]
        private float _popInDuration = 0.22f;

        [SerializeField, Min(0.01f)]
        private float _settleDuration = 0.14f;

        [SerializeField, Min(0f)]
        private float _holdDuration = 0.26f;

        [SerializeField, Min(0.01f)]
        private float _fadeOutDuration = 0.28f;

        [SerializeField]
        private float _startScale = 0.4f;

        [SerializeField]
        private float _overshootScale = 1.25f;

        [SerializeField]
        [Tooltip("While counting, an invisible full-screen layer above every canvas swallows all taps (joystick, " +
            "pause, build buttons...).")]
        private bool _blockInput = true;

        private Sequence _sequence;
        private Action _onFinished;
        private GameObject _inputBlocker;

        public bool IsPlaying => _sequence != null && _sequence.IsActive() && _sequence.IsPlaying();

        private GameObject Root => _root != null ? _root : gameObject;

        private void Awake()
        {
            if (_label == null)
            {
                _label = GetComponentInChildren<TMP_Text>(true);
            }

            // Only a separate child root can be hidden here: hiding our own GameObject from Awake would undo the
            // SetActive(true) that Play does when this object starts inactive.
            if (_root != null && _root != gameObject && !IsPlaying)
            {
                _root.SetActive(false);
            }
        }

        /// <summary>Counts down from _startNumber to 1, then calls <paramref name="onFinished"/> once. A second call
        /// while it is already counting is ignored (the first sequence's callback still fires).</summary>
        public void Play(Action onFinished)
        {
            if (IsPlaying)
            {
                return;
            }

            if (_label == null)
            {
                Debug.LogWarning("[LevelStartCountdown] No label assigned; finishing immediately.", this);
                onFinished?.Invoke();
                return;
            }

            _onFinished = onFinished;
            Root.SetActive(true);
            SetInputBlocked(true);
            Transform labelTransform = _label.transform;

            _sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
            for (int n = _startNumber; n >= 1; n--)
            {
                int number = n;
                _sequence.AppendCallback(() =>
                {
                    _label.text = number.ToString();
                    _label.alpha = 1f;
                    labelTransform.localScale = Vector3.one * _startScale;
                    LogStep("Countdown " + number);
                });
                _sequence.Append(labelTransform.DOScale(_overshootScale, _popInDuration).SetEase(Ease.OutQuad));
                _sequence.Append(labelTransform.DOScale(1f, _settleDuration).SetEase(Ease.InOutSine));
                _sequence.AppendInterval(_holdDuration);
                _sequence.Append(DOTween.To(() => _label.alpha, a => _label.alpha = a, 0f, _fadeOutDuration).SetEase(Ease.InQuad));
            }

            _sequence.OnComplete(Finish);
        }

        private void Finish()
        {
            SetInputBlocked(false);
            _sequence = null;
            Root.SetActive(false);
            Action callback = _onFinished;
            _onFinished = null;
            callback?.Invoke();
        }

        private void OnDisable()
        {
            SetInputBlocked(false);
            // Destroyed/disabled mid-count (scene unload): drop the tween without firing the callback.
            if (_sequence != null && _sequence.IsActive())
            {
                _sequence.Kill();
            }

            _sequence = null;
        }

        /// <summary>Made once, on the label's root canvas, with its own top-most sorting so it covers every other
        /// canvas (HUD, joystick, minimap) - the EventSystem then hands every tap to it and nothing else reacts.</summary>
        private void SetInputBlocked(bool blocked)
        {
            if (!_blockInput)
            {
                return;
            }

            if (_inputBlocker == null)
            {
                if (!blocked || _label == null || _label.canvas == null)
                {
                    return;
                }

                Canvas root = _label.canvas.rootCanvas;
                _inputBlocker = new GameObject("CountdownInputBlocker", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(Image));
                _inputBlocker.transform.SetParent(root.transform, false);
                var rect = (RectTransform)_inputBlocker.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                var canvas = _inputBlocker.GetComponent<Canvas>();
                canvas.overrideSorting = true;
                canvas.sortingOrder = 32000;
                var image = _inputBlocker.GetComponent<Image>();
                image.color = new Color(0f, 0f, 0f, 0f);
                image.raycastTarget = true;
            }

            _inputBlocker.SetActive(blocked);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void LogStep(string message)
        {
            Debug.Log("[LevelStart] " + message, this);
        }
    }
}
