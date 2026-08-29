using AlienDefense.Core;
using UnityEngine;

namespace AlienDefense.UI
{
    /// <summary>Main Menu's scene entry point. Receives application services via IApplicationServicesReceiver
    /// (never a global lookup) and routes Play/Exit to SceneTransitionService. Knows nothing about gameplay systems.</summary>
    public sealed class MainMenuPresenter : MonoBehaviour, IApplicationServicesReceiver
    {
        [SerializeField]
        private MainMenuView _view;

        [SerializeField]
        [Tooltip("Optional.")]
        private BackNavigationController _backNavigation;

        private ApplicationServices _services;

        public void ReceiveApplicationServices(ApplicationServices services)
        {
            _services = services;

            if (_view != null)
            {
                _view.PlayClicked += HandlePlayClicked;
                _view.ExitClicked += HandleExitClicked;
            }

            _backNavigation?.SetHandler(HandleExitClicked);
        }

        private void HandlePlayClicked()
        {
            _services?.SceneTransition.TryLoadSceneViaBootstrap(SceneNames.LevelSelection);
        }

        private void HandleExitClicked()
        {
#if UNITY_EDITOR
            Debug.Log("[MainMenuPresenter] Exit requested (no-op in the Editor).");
#else
            Application.Quit();
#endif
        }

        private void OnDestroy()
        {
            if (_view != null)
            {
                _view.PlayClicked -= HandlePlayClicked;
                _view.ExitClicked -= HandleExitClicked;
            }
        }
    }
}
