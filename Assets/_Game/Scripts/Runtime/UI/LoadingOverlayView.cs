using AlienDefense.Core;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AlienDefense.UI
{
    /// <summary>Full-screen blocking loading panel, spawned by SceneTransitionService for the duration of each
    /// transition and destroyed when the load completes or fails. Dumb view: only displays progress — never
    /// starts a scene load, never contains routing rules.
    ///
    /// Branding fields (Background/Logo/Handle sprites) are intentionally left with no Sprite assigned by the
    /// Editor scaffolder — this is the "base" the artwork gets dropped onto by hand in the Inspector.</summary>
    public sealed class LoadingOverlayView : MonoBehaviour
    {
        private const int OverlayCanvasSortOrder = 32767;

#if UNITY_EDITOR
        /// <summary>Editor scaffolding sets this while creating/instantiating prefabs so Awake does not touch UI.</summary>
        public static bool SuppressEditorLifecycleCallbacks;
#endif

        [SerializeField]
        private CanvasGroup _canvasGroup;

        [SerializeField]
        private TMP_Text _loadingText;

        [SerializeField]
        [Tooltip("Optional.")]
        private Image _progressFillImage;

        [Header("Branding (assign sprites by hand — this script only positions/updates them)")]
        [SerializeField]
        [Tooltip("Optional. Full-screen background art.")]
        private Image _backgroundImage;

        [SerializeField]
        [Tooltip("Optional. Game logo, positioned above the progress bar.")]
        private Image _logoImage;

        [SerializeField]
        [Tooltip("Optional. \"Version: X\" text, top-right corner. Auto-filled from Application.version on spawn.")]
        private TMP_Text _versionText;

        [SerializeField]
        [Tooltip("Optional. \"Loading X %\" text below the progress bar.")]
        private TMP_Text _percentageText;

        [SerializeField]
        [Tooltip("Optional. The progress bar's own RectTransform — needed to compute the Handle's slide range from its width.")]
        private RectTransform _progressBarTrack;

        [SerializeField]
        [Tooltip("Optional. Icon (e.g. the UFO) that slides along the bar as progress advances — assign a sprite " +
            "to its own Image component; this script only moves it.")]
        private RectTransform _progressHandle;

        private Canvas _canvas;
        private Camera _boundCamera;

        public Camera BoundCamera => _boundCamera;

        private void Awake()
        {
#if UNITY_EDITOR
            if (SuppressEditorLifecycleCallbacks)
            {
                return;
            }
#endif

            ResolveReferences();

            if (ShouldRunBootstrapSetup() && IsBootstrapScene())
            {
                EnsureBootstrapControllerIfNeeded();
            }
        }

        private bool IsBootstrapScene()
        {
            Scene scene = gameObject.scene;
            return scene.IsValid() && scene.name == SceneNames.Bootstrap;
        }

        private static bool ShouldRunBootstrapSetup()
        {
#if UNITY_EDITOR
            return UnityEditor.EditorApplication.isPlaying;
#else
            return true;
#endif
        }

        private void EnsureBootstrapControllerIfNeeded()
        {
            Scene scene = gameObject.scene;
            if (!scene.IsValid() || scene.name != SceneNames.Bootstrap)
            {
                return;
            }

            if (FindFirstObjectByType<BootstrapLoadingController>() != null)
            {
                return;
            }

            GameObject hostObject = new GameObject("BootstrapHost");
            hostObject.AddComponent<BootstrapLoadingController>();
        }

        private void ResolveReferences()
        {
            if (_canvas == null)
            {
                _canvas = GetComponent<Canvas>();
            }

            if (_canvasGroup == null)
            {
                Transform safeArea = FindChildRecursive(transform, "SafeArea");
                if (safeArea != null)
                {
                    _canvasGroup = safeArea.GetComponent<CanvasGroup>();
                }
            }

            if (_loadingText == null)
            {
                Transform loadingText = FindChildRecursive(transform, "LoadingText");
                if (loadingText != null)
                {
                    _loadingText = loadingText.GetComponent<TMP_Text>();
                }
            }

            if (_progressFillImage == null)
            {
                Transform fill = FindChildRecursive(transform, "Fill");
                if (fill != null)
                {
                    _progressFillImage = fill.GetComponent<Image>();
                }
            }

            if (_backgroundImage == null)
            {
                Transform background = FindChildRecursive(transform, "Background");
                if (background != null)
                {
                    _backgroundImage = background.GetComponent<Image>();
                }
            }

            if (_logoImage == null)
            {
                Transform logo = FindChildRecursive(transform, "Logo");
                if (logo != null)
                {
                    _logoImage = logo.GetComponent<Image>();
                }
            }

            if (_versionText == null)
            {
                Transform versionText = FindChildRecursive(transform, "VersionText");
                if (versionText != null)
                {
                    _versionText = versionText.GetComponent<TMP_Text>();
                }
            }

            if (_percentageText == null)
            {
                Transform percentageText = FindChildRecursive(transform, "PercentageText");
                if (percentageText != null)
                {
                    _percentageText = percentageText.GetComponent<TMP_Text>();
                }
            }

            if (_progressBarTrack == null)
            {
                Transform track = FindChildRecursive(transform, "ProgressBarTrack");
                if (track != null)
                {
                    _progressBarTrack = track.GetComponent<RectTransform>();
                }
            }

            if (_progressHandle == null)
            {
                Transform handle = FindChildRecursive(transform, "Handle");
                if (handle != null)
                {
                    _progressHandle = handle.GetComponent<RectTransform>();
                }
            }
        }

        private static Transform FindChildRecursive(Transform parent, string childName)
        {
            if (parent.name == childName)
            {
                return parent;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindChildRecursive(parent.GetChild(i), childName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        /// <summary>Assigns the Screen Space Camera used to render this overlay.</summary>
        public void BindCamera(Camera camera)
        {
            _boundCamera = camera;
            ResolveReferences();
            LoadingOverlayCameraUtility.BindCanvas(_canvas, camera);
        }

        /// <summary>Called after Instantiate so version text and canvas sort order are correct even if Awake already ran on the prefab asset.</summary>
        public void InitializeForTransition()
        {
            ResolveReferences();

            if (_canvas != null)
            {
                _canvas.sortingOrder = OverlayCanvasSortOrder;
            }

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
                _canvasGroup.interactable = true;
                _canvasGroup.blocksRaycasts = true;
            }

            if (_versionText != null)
            {
                _versionText.text = $"Version: {Application.version}";
            }

            SetProgress(0f);
        }

        /// <summary>Makes the overlay visible when bound to a valid Screen Space Camera.</summary>
        public void Show()
        {
            ResolveReferences();
            gameObject.SetActive(true);

            if (_boundCamera != null)
            {
                LoadingOverlayCameraUtility.BindCanvas(_canvas, _boundCamera);
            }

            if (_canvas != null)
            {
                _canvas.enabled = true;
            }

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
                _canvasGroup.interactable = true;
                _canvasGroup.blocksRaycasts = true;
            }
        }

        public void SetProgress(float progress01)
        {
            progress01 = Mathf.Clamp01(progress01);

            if (_progressFillImage != null)
            {
                _progressFillImage.fillAmount = progress01;
            }

            if (_percentageText != null)
            {
                _percentageText.text = $"Loading {(progress01 * 100f):0.0} %";
            }

            if (_progressHandle != null && _progressBarTrack != null)
            {
                try
                {
                    float trackWidth = _progressBarTrack.rect.width;
                    Vector2 anchoredPosition = _progressHandle.anchoredPosition;
                    anchoredPosition.x = trackWidth * progress01;
                    _progressHandle.anchoredPosition = anchoredPosition;
                }
                catch (MissingReferenceException)
                {
                    _progressHandle = null;
                    _progressBarTrack = null;
                }
            }
        }

        /// <summary>Hides the overlay immediately so its canvas stops drawing before deferred Destroy runs.</summary>
        public void Dismiss()
        {
            ResolveReferences();

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
            }

            if (_canvas != null)
            {
                _canvas.worldCamera = null;
                _canvas.enabled = false;
            }

            _boundCamera = null;
            Canvas.ForceUpdateCanvases();
            gameObject.SetActive(false);
        }

        public void SetLoadingText(string text)
        {
            if (_loadingText != null)
            {
                _loadingText.text = text;
            }
        }
    }
}
