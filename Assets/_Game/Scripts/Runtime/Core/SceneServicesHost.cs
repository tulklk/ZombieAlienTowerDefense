using AlienDefense.DebugTools;
using AlienDefense.UI;
using UnityEngine;

namespace AlienDefense.Core
{
    /// <summary>Per-scene host that exposes this scene's SceneTransitionService (and optional save-debug
    /// controls) to ApplicationCompositionRoot. The root discovers this host itself, through a scene-scoped
    /// GetComponentInChildren scan right after this scene finishes loading (see
    /// ApplicationCompositionRoot.HandleSceneLoaded) — this host never reaches upward via a global
    /// Find/FindObjectOfType, it only responds when the root calls BindApplicationRoot.</summary>
    public sealed class SceneServicesHost : MonoBehaviour
    {
        [SerializeField]
        private SceneTransitionService _sceneTransition;

        [SerializeField]
        [Tooltip("Optional. Development-only save testing controls.")]
        private SaveDebugControls _saveDebugControls;

        public SceneTransitionService SceneTransition => _sceneTransition;

        private void Awake()
        {
            LoadingOverlayCleanup.DestroyAllRuntimeInstances();

            if (_sceneTransition == null)
            {
                _sceneTransition = GetComponent<SceneTransitionService>();
            }

            if (_sceneTransition == null)
            {
                Debug.LogError("[SceneServicesHost] No SceneTransitionService assigned.", this);
            }
        }

        private void Start()
        {
            LoadingOverlayCleanup.DestroyAllRuntimeInstances();
        }

        /// <summary>Called by ApplicationCompositionRoot right after this scene finishes loading.</summary>
        public void BindApplicationRoot(ApplicationCompositionRoot root)
        {
            if (root == null || _sceneTransition == null)
            {
                return;
            }

            root.BindSceneTransition(_sceneTransition);
            root.InitializeSaveDebugControls(_saveDebugControls);
        }
    }
}
