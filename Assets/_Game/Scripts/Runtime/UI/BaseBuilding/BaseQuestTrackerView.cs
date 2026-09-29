using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.BaseBuilding
{
    /// <summary>"Build Patrol Post lvl. 1 (0/1)" above the bottom navigation. Tapping it focuses the camera on the
    /// building the goal is about. When a goal completes it flashes "(1/1)" briefly before moving on to the next.</summary>
    public sealed class BaseQuestTrackerView : MonoBehaviour
    {
        [SerializeField]
        private GameObject _root;

        [SerializeField]
        private TMP_Text _label;

        [SerializeField]
        private Button _button;

        [SerializeField]
        [Tooltip("Optional glow behind the tracker, pulsed when a goal completes.")]
        private Image _glow;

        private System.Action _clicked;
        private Tweener _pulse;

        private void Awake()
        {
            _button?.onClick.AddListener(() => _clicked?.Invoke());
        }

        private void OnDestroy()
        {
            _pulse?.Kill();
            _button?.onClick.RemoveAllListeners();
        }

        public void Bind(string text, System.Action clicked)
        {
            _clicked = clicked;
            bool visible = !string.IsNullOrEmpty(text);
            if (_root != null)
            {
                _root.SetActive(visible);
            }

            if (_label != null)
            {
                _label.text = text;
            }
        }

        public void PlayCompleted()
        {
            if (_glow == null)
            {
                return;
            }

            _pulse?.Kill();
            Color c = _glow.color;
            c.a = 0f;
            _glow.color = c;
            _pulse = DOTween.To(() => _glow.color.a, a =>
                {
                    Color g = _glow.color;
                    g.a = a;
                    _glow.color = g;
                }, 0.8f, 0.25f)
                .SetLoops(4, LoopType.Yoyo)
                .SetUpdate(true)
                .SetLink(gameObject);
        }
    }
}
