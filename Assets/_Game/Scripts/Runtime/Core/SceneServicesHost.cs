using AlienDefense.DebugTools;
using AlienDefense.UI;
using UnityEngine;

namespace AlienDefense.Core
{
    /// <summary>Per-scene host for SceneTransitionService. Binds the transition service into ApplicationRuntime
    /// and ticks profile saves while this scene is active.</summary>
    public sealed class SceneServicesHost : MonoBehaviour
    {
        [SerializeField]
        private SceneTransitionService _sceneTransition;

        [SerializeField]
        [Tooltip("Optional. Development-only save testing controls.")]
        private SaveDebugControls _saveDebugControls;

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
                return;
            }

            ApplicationRuntime.BindSceneTransition(_sceneTransition);
            ApplicationRuntime.InitializeSaveDebugControls(_saveDebugControls);
            ApplicationRuntime.InjectScene(gameObject.scene);
        }

        private void Start()
        {
            LoadingOverlayCleanup.DestroyAllRuntimeInstances();
        }

        private void Update()
        {
            ApplicationRuntime.Tick(Time.unscaledDeltaTime);
        }
    }
}
