using UnityEngine;

namespace AlienDefense.UI
{
    /// <summary>Fits its RectTransform to Screen.safeArea so children stay clear of notches/cutouts.</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform _rectTransform;
        private Rect _lastSafeArea;
        private Vector2Int _lastScreenSize;
        private ScreenOrientation _lastOrientation;
        private bool _hasAppliedOnce;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
        }

        private void OnEnable()
        {
            Apply();
        }

        private void Update()
        {
            if (HasChanged())
            {
                Apply();
            }
        }

        private bool HasChanged()
        {
            return !_hasAppliedOnce
                || Screen.safeArea != _lastSafeArea
                || Screen.width != _lastScreenSize.x
                || Screen.height != _lastScreenSize.y
                || Screen.orientation != _lastOrientation;
        }

        private void Apply()
        {
            if (Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

#if UNITY_EDITOR
            // The Editor's Device Simulator reports a real notch/status-bar inset while Playing (e.g. Note10),
            // but a plain (non-simulated) Edit-mode preview never does — so authoring a layout by eye in Edit
            // mode and then pressing Play used to visibly shift every SafeArea-nested element. Skipping the
            // inset in the Editor entirely keeps Edit and Play pixel-identical for iteration; real builds
            // (this #if is compiled out) still get the full dynamic safe-area protection below.
            _rectTransform.anchorMin = Vector2.zero;
            _rectTransform.anchorMax = Vector2.one;
            _lastSafeArea = Screen.safeArea;
            _lastScreenSize = new Vector2Int(Screen.width, Screen.height);
            _lastOrientation = Screen.orientation;
            _hasAppliedOnce = true;
            return;
#else
            Rect safeArea = Screen.safeArea;

            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;
            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;

            _rectTransform.anchorMin = anchorMin;
            _rectTransform.anchorMax = anchorMax;

            _lastSafeArea = safeArea;
            _lastScreenSize = new Vector2Int(Screen.width, Screen.height);
            _lastOrientation = Screen.orientation;
            _hasAppliedOnce = true;
#endif
        }
    }
}
