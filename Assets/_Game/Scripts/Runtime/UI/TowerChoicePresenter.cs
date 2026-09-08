using AlienDefense.Building;
using AlienDefense.Core;
using AlienDefense.Towers;
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
        private BuildNode _pendingNode;

        public bool IsShowing => _pendingNode != null;

        /// <summary>Whether at least one catalog tower is currently affordable - gates whether opening this
        /// popup is even worth it (see PlayerBuildNodeProximityController.ResolveChannelComplete).</summary>
        public bool HasAnyAffordableTower()
        {
            if (_catalog == null || _transactionService == null)
            {
                return false;
            }

            for (int i = 0; i < _catalog.Length; i++)
            {
                if (_catalog[i] != null && _transactionService.CanAffordBuild(_catalog[i]))
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

        public void Initialize(EnergyTowerTransactionService transactionService, TowerDefinition[] catalog, GameSpeedController gameSpeed)
        {
            Unsubscribe();

            _transactionService = transactionService;
            _catalog = catalog;
            _gameSpeed = gameSpeed;

            if (_view != null)
            {
                _view.CardClicked += HandleCardClicked;
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

            var cards = new TowerCardData[_catalog.Length];
            for (int i = 0; i < _catalog.Length; i++)
            {
                TowerDefinition definition = _catalog[i];
                if (definition == null)
                {
                    continue;
                }

                int cost = _transactionService != null ? _transactionService.GetBuildCost(definition) : 0;
                bool affordable = _transactionService != null && _transactionService.CanAffordBuild(definition);
                cards[i] = new TowerCardData(definition.DisplayName, definition.Description, cost + " Energy", definition.Icon, affordable);
            }

            _view.Show(cards);
            _gameSpeed?.Pause();
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
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }
    }
}
