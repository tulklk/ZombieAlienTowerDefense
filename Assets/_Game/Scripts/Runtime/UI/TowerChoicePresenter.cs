using AlienDefense.Building;
using AlienDefense.Core;
using AlienDefense.Save;
using AlienDefense.Towers;
using AlienDefense.UI.MainMenu;
using UnityEngine;

namespace AlienDefense.UI
{
    /// <summary>Shows the "Choose Tower" popup (see PlayerBuildNodeProximityController - opened once a build
    /// channel completes with enough Energy on an Available node) offering every TowerDefinition in this
    /// project's catalog; picking a card spends Energy and builds via EnergyTowerTransactionService. Pauses
    /// GameSpeed (not GameFlow - a mid-gameplay choice, not the manual Pause menu) while a choice is pending.</summary>
    public sealed class TowerChoicePresenter : MonoBehaviour
    {
        [SerializeField]
        private TowerChoiceView _view;

        private EnergyTowerTransactionService _transactionService;
        private TowerDefinition[] _catalog;
        private GameSpeedController _gameSpeed;
        private PlayerProfileService _profile;
        private BuildNode _pendingNode;

        public bool IsShowing => _pendingNode != null;

        /// <summary>Whether at least one catalog tower is currently affordable - gates whether opening this
        /// popup is even worth it (see PlayerBuildNodeProximityController.ResolveChannelComplete).</summary>
        public bool HasAnyAffordableTower()
        {
            return HasAnyAffordableTower(null);
        }

        /// <summary>Same, counting the Energy already deposited into <paramref name="node"/> (see
        /// EnergyTowerTransactionService.TryDeposit).</summary>
        public bool HasAnyAffordableTower(BuildNode node)
        {
            if (_catalog == null || _transactionService == null)
            {
                return false;
            }

            for (int i = 0; i < _catalog.Length; i++)
            {
                if (_catalog[i] != null && _transactionService.CanAffordBuild(node, _catalog[i]))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The lowest build cost across the whole catalog - used for the BuildNode cost badge on an
        /// Available node (see PlayerBuildNodeProximityController.RefreshAllCostBadges), since which specific
        /// tower gets built isn't decided until this popup opens.</summary>
        public int CheapestBuildCost()
        {
            if (_catalog == null || _transactionService == null)
            {
                return 0;
            }

            int cheapest = int.MaxValue;
            for (int i = 0; i < _catalog.Length; i++)
            {
                if (_catalog[i] == null)
                {
                    continue;
                }

                int cost = _transactionService.GetBuildCost(_catalog[i]);
                if (cost < cheapest)
                {
                    cheapest = cost;
                }
            }

            return cheapest == int.MaxValue ? 0 : cheapest;
        }

        /// <param name="profile">Optional - only read to print the player's saved Gem total on the panel.</param>
        public void Initialize(EnergyTowerTransactionService transactionService, TowerDefinition[] catalog,
            GameSpeedController gameSpeed, PlayerProfileService profile = null)
        {
            Unsubscribe();

            _transactionService = transactionService;
            _catalog = catalog;
            _gameSpeed = gameSpeed;
            _profile = profile;

            if (_view != null)
            {
                _view.CardClicked += HandleCardClicked;
                _view.RerollClicked += HandleRerollClicked;
                _view.Hide();
            }
        }

        /// <summary>Intended caller: PlayerBuildNodeProximityController, right after its fly-to-node animation
        /// finishes on an Available node it just confirmed can afford at least the cheapest tower.</summary>
        public void ShowForNode(BuildNode node)
        {
            if (_view == null || node == null || node.State != BuildNodeState.Available || _catalog == null)
            {
                return;
            }

            _pendingNode = node;
            PopulateCards();
            _gameSpeed?.Pause();
        }

        /// <summary>Fills the panel from the pending node: which towers exist, what they cost, which are affordable
        /// right now, and the player's Gem total. Called when the panel opens and when Free is tapped.</summary>
        private void PopulateCards()
        {
            BuildNode node = _pendingNode;
            if (_view == null || node == null || _catalog == null)
            {
                return;
            }

            var cards = new TowerCardData[_catalog.Length];
            for (int i = 0; i < _catalog.Length; i++)
            {
                TowerDefinition definition = _catalog[i];
                if (definition == null)
                {
                    continue;
                }

                int cost = _transactionService != null ? _transactionService.GetBuildCost(definition) : 0;
                bool affordable = _transactionService != null && _transactionService.CanAffordBuild(node, definition);
                cards[i] = new TowerCardData(definition.DisplayName, definition.Description, cost + " Energy",
                    definition.Icon, affordable, definition.ChoiceCardSprite);
            }

            _view.Show(cards);
            _view.SetGems(CurrencyFormatter.Format(_profile != null ? _profile.Gems : 0));
        }

        /// <summary>The Free button. This project's catalog is its whole choice, so there is no other set of towers
        /// to deal - refreshing re-reads the offer instead (Energy may have changed while the panel was open).</summary>
        private void HandleRerollClicked()
        {
            PopulateCards();
        }

        private void HandleCardClicked(int index)
        {
            if (_pendingNode == null || _catalog == null || index < 0 || index >= _catalog.Length)
            {
                return;
            }

            TowerDefinition chosen = _catalog[index];
            _transactionService?.TryBuild(_pendingNode, chosen);

            _pendingNode = null;
            _view?.Hide();
            _gameSpeed?.Resume();
        }

        private void Unsubscribe()
        {
            if (_view != null)
            {
                _view.CardClicked -= HandleCardClicked;
                _view.RerollClicked -= HandleRerollClicked;
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }
    }
}
