using AlienDefense.Core;
using AlienDefense.UI.MainMenu;
using UnityEngine;

namespace AlienDefense.UI.Base
{
    // Intentionally NOT IApplicationServicesReceiver: this presenter lives inside the shared MainMenu
    // composition root alongside 4 other former scene-entry-point presenters (Shop/Upgrade/Defense/MainMenu
    // itself). ApplicationCompositionRoot.HandleSceneLoaded only pushes services to a single
    // IApplicationServicesReceiver per loaded scene — implementing it here too would silently steal that slot
    // from MainMenuPresenter (whichever component GetComponentInChildren finds first "wins", the rest never
    // get initialized). Services instead cascade in explicitly via MenuShellPresenter.Initialize.
    public sealed class BaseScreenPresenter : MonoBehaviour
    {
        [SerializeField]
        private BaseScreenView _view;

        private ApplicationServices _services;

        public void ReceiveApplicationServices(ApplicationServices services)
        {
            _services = services;
            Refresh();
        }

        public void Refresh()
        {
            if (_view == null || _services?.PlayerProfileService == null)
            {
                return;
            }

            var profile = _services.PlayerProfileService;
            _view.SetStats(
                profile.GetCompletedLevelCount(),
                profile.GetTotalStarsEarned(),
                profile.GetUnlockedTowerCount(),
                profile.VipTier,
                CurrencyFormatter.Format(profile.MetaCurrency),
                CurrencyFormatter.Format(profile.Gems));
        }
    }
}
