using System.Collections;
using AlienDefense.UI;
using UnityEngine;

namespace AlienDefense.Building
{
    /// <summary>Continuously watches the Player's XZ distance to a fixed set of BuildNodes. Flying into a node's
    /// proximity radius and staying there for _channelDuration seconds (visualized as BuildNodeChannelUI's ring
    /// filling clockwise - reset the instant the Player leaves or a different node becomes nearest) triggers,
    /// if the Energy wallet can afford this node's next action: a short fly-animation of Energy Ball icons from
    /// the Player to the node, then either the TowerChoicePresenter popup (node Available - the player still
    /// picks which tower) or an immediate EnergyTowerTransactionService upgrade (node Occupied - already a
    /// specific tower, no choice needed). Deliberately does NOT touch TowerSelectionService - proximity alone
    /// used to auto-select the nearest tower (showing its RangeIndicator, a green circle) but that visually
    /// clashed with this controller's own yellow channel ring, so tower selection here is tap-only again (see
    /// WorldSelectionController). Never spawns/charges anything itself - only forwards to
    /// EnergyTowerTransactionService/TowerChoicePresenter.</summary>
    public sealed class PlayerBuildNodeProximityController : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Every BuildNode this controller should react to. Scene-local list, not a global registry.")]
        private BuildNode[] _nodes;

        [SerializeField, Min(0.1f)]
        [Tooltip("World units (XZ only) the Player must be within for a node to react.")]
        private float _proximityRadius = 2f;

        [SerializeField, Min(0.02f)]
        private float _scanInterval = 0.1f;

        [SerializeField, Min(0.1f)]
        [Tooltip("Seconds the Player must stay in range before a build/upgrade actually triggers.")]
        private float _channelDuration = 3f;

        [SerializeField, Min(0.05f)]
        private float _flyDuration = 0.5f;

        [SerializeField]
        [Tooltip("Optional. Spawned as a world-space SpriteRenderer that arcs from the Player to the node during " +
            "the fly animation - purely decorative.")]
        private Sprite _energyBallSprite;

        private Transform _player;
        private EnergyTowerTransactionService _energyTransactions;
        private TowerChoicePresenter _towerChoicePresenter;

        private BuildNode _channelingNode;
        private float _channelTimer;
        private bool _isChannelBusy; // true while a fly animation / choice popup is resolving - suspends new channels
        private float _scanTimer;
        private bool _isInputEnabled = true;

        public void Initialize(Transform player, EnergyTowerTransactionService energyTransactions, TowerChoicePresenter towerChoicePresenter, Transform cameraTransform)
        {
            _player = player;
            _energyTransactions = energyTransactions;
            _towerChoicePresenter = towerChoicePresenter;
            _channelingNode = null;
            _channelTimer = 0f;
            _isChannelBusy = false;
            _scanTimer = 0f;

            InitializeBillboards(cameraTransform);
            RefreshAllCostBadges();
        }

        /// <summary>Keeps every node's ring/badge facing the fixed isometric camera - otherwise the World Space
        /// canvas plane renders edge-on and unreadable from this camera's steep top-down angle.</summary>
        private void InitializeBillboards(Transform cameraTransform)
        {
            if (_nodes == null || cameraTransform == null)
            {
                return;
            }

            for (int i = 0; i < _nodes.Length; i++)
            {
                _nodes[i]?.ChannelUI?.Initialize(cameraTransform);
            }
        }

        /// <summary>Mirrors WorldSelectionController.SetInputEnabled: disabled during Paused/Victory/Defeat. Just
        /// stops new proximity triggers — never force-clears an already-open TowerDetailsPanel or in-flight
        /// channel/animation.</summary>
        public void SetInputEnabled(bool value)
        {
            _isInputEnabled = value;
        }

        private void Update()
        {
            if (_player == null || _nodes == null || _nodes.Length == 0)
            {
                return;
            }

            _scanTimer -= Time.deltaTime;
            if (_scanTimer <= 0f)
            {
                _scanTimer = _scanInterval;
                Scan();
            }

            TickChannel(Time.deltaTime);
        }

        private void Scan()
        {
            if (!_isInputEnabled)
            {
                return;
            }

            Vector3 playerPosition = _player.position;
            float nearestSqrDistance = _proximityRadius * _proximityRadius;
            BuildNode nearestNode = null;

            for (int i = 0; i < _nodes.Length; i++)
            {
                BuildNode node = _nodes[i];
                if (node == null || node.State == BuildNodeState.Disabled)
                {
                    continue;
                }

                Vector3 toNode = node.BuildPoint.position - playerPosition;
                toNode.y = 0f;
                float sqrDistance = toNode.sqrMagnitude;
                if (sqrDistance > nearestSqrDistance)
                {
                    continue;
                }

                nearestSqrDistance = sqrDistance;
                nearestNode = node;
            }

            BuildNode channelTarget = nearestNode != null && IsChannelable(nearestNode) ? nearestNode : null;
            if (channelTarget != _channelingNode && !_isChannelBusy)
            {
                CancelChannel();
                _channelingNode = channelTarget;
            }
        }

        private bool IsChannelable(BuildNode node)
        {
            if (node.State == BuildNodeState.Available)
            {
                return true;
            }

            return node.State == BuildNodeState.Occupied && node.CurrentTower != null && !node.CurrentTower.IsMaxLevel;
        }

        private void TickChannel(float deltaTime)
        {
            if (_channelingNode == null || _isChannelBusy || !_isInputEnabled)
            {
                return;
            }

            _channelTimer += deltaTime;
            _channelingNode.ChannelUI?.SetChannelProgress(_channelTimer / _channelDuration);

            if (_channelTimer < _channelDuration)
            {
                return;
            }

            BuildNode node = _channelingNode;
            CancelChannel();
            StartCoroutine(ResolveChannelComplete(node));
        }

        private void CancelChannel()
        {
            _channelingNode?.ChannelUI?.SetChannelProgress(0f);
            _channelingNode = null;
            _channelTimer = 0f;
        }

        /// <summary>Runs the fly animation then the build/upgrade action - a coroutine (not an instant call) so
        /// the visual read as "energy is being spent" rather than the tower just appearing.</summary>
        private IEnumerator ResolveChannelComplete(BuildNode node)
        {
            if (node == null || _energyTransactions == null)
            {
                yield break;
            }

            bool isBuild = node.State == BuildNodeState.Available;
            bool canAfford = isBuild
                ? _towerChoicePresenter != null && _towerChoicePresenter.HasAnyAffordableTower()
                : _energyTransactions.CanAffordUpgrade(node.CurrentTower);

            if (!canAfford)
            {
                yield break; // channel simply fizzles - no fly, no popup
            }

            _isChannelBusy = true;
            yield return PlayFlyAnimation(node);

            if (isBuild)
            {
                _towerChoicePresenter?.ShowForNode(node);
            }
            else
            {
                _energyTransactions.TryUpgrade(node.CurrentTower);
            }

            _isChannelBusy = false;
        }

        private IEnumerator PlayFlyAnimation(BuildNode node)
        {
            if (_energyBallSprite == null || _player == null)
            {
                yield break;
            }

            var go = new GameObject("EnergyBallFlyVisual");
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = _energyBallSprite;
            renderer.sortingOrder = 100;
            go.transform.localScale = Vector3.one * 0.6f;

            Vector3 start = _player.position + Vector3.up * 1.5f;
            Vector3 end = node.BuildPoint.position + Vector3.up * 0.5f;
            float arcHeight = 1.5f;

            float t = 0f;
            while (t < _flyDuration)
            {
                t += Time.deltaTime;
                float normalized = Mathf.Clamp01(t / _flyDuration);
                Vector3 flat = Vector3.Lerp(start, end, normalized);
                flat.y += Mathf.Sin(normalized * Mathf.PI) * arcHeight;
                go.transform.position = flat;
                yield return null;
            }

            Destroy(go);
        }

        /// <summary>Cheap - just text updates, called once on Initialize and again whenever the Energy wallet
        /// changes (see LevelCompositionRoot wiring) so every badge stays accurate without a per-frame cost.</summary>
        public void RefreshAllCostBadges()
        {
            if (_nodes == null || _energyTransactions == null)
            {
                return;
            }

            for (int i = 0; i < _nodes.Length; i++)
            {
                BuildNode node = _nodes[i];
                if (node == null || node.ChannelUI == null)
                {
                    continue;
                }

                int cost;
                if (node.State == BuildNodeState.Available)
                {
                    cost = _towerChoicePresenter != null ? _towerChoicePresenter.CheapestBuildCost() : 0;
                }
                else if (node.State == BuildNodeState.Occupied)
                {
                    cost = _energyTransactions.GetUpgradeCost(node.CurrentTower);
                }
                else
                {
                    cost = 0;
                }

                int wallet = _energyTransactions != null ? CurrentWalletEnergy() : 0;
                node.ChannelUI.SetCost(wallet, cost);

                node.ChannelUI.SetRingVisible(IsChannelable(node));
            }
        }

        private int CurrentWalletEnergy()
        {
            return _energyTransactions != null ? _energyTransactions.CurrentWalletEnergy : 0;
        }
    }
}
