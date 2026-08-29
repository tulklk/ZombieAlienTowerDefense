using AlienDefense.Towers;
using UnityEngine;

namespace AlienDefense.Building
{
    /// <summary>Continuously watches the Player's XZ distance to a fixed set of BuildNodes. Flying into a node's
    /// proximity radius triggers exactly what a tap on that node would (see WorldSelectionController): builds the
    /// currently-selected tower on an empty node, or opens TowerDetailsPanel on an occupied one. Flying away from
    /// the currently-open tower's node clears the selection again. Never spawns towers itself, never touches
    /// Economy/Canvas — only forwards to BuildService/TowerSelectionService, same as the tap path.</summary>
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

        private Transform _player;
        private BuildService _buildService;
        private TowerSelectionService _towerSelectionService;
        private BuildNode _activeOccupiedNode;
        private float _scanTimer;
        private bool _isInputEnabled = true;

        public void Initialize(Transform player, BuildService buildService, TowerSelectionService towerSelectionService)
        {
            _player = player;
            _buildService = buildService;
            _towerSelectionService = towerSelectionService;
            _activeOccupiedNode = null;
            _scanTimer = 0f;
        }

        /// <summary>Mirrors WorldSelectionController.SetInputEnabled: disabled during Paused/Victory/Defeat. Just
        /// stops new proximity triggers — never force-clears an already-open TowerDetailsPanel.</summary>
        public void SetInputEnabled(bool value)
        {
            _isInputEnabled = value;
        }

        private void Update()
        {
            if (!_isInputEnabled || _player == null || _nodes == null || _nodes.Length == 0)
            {
                return;
            }

            _scanTimer -= Time.deltaTime;
            if (_scanTimer > 0f)
            {
                return;
            }

            _scanTimer = _scanInterval;
            Scan();
        }

        /// <summary>Only ever acts on the single nearest in-range node, so hovering near two nodes at once can't
        /// build on both or fight over which tower's details are shown.</summary>
        private void Scan()
        {
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

            if (nearestNode != null && nearestNode.State == BuildNodeState.Available)
            {
                _buildService?.TryBuild(nearestNode);
                // TryBuild mutates nearestNode.State in place on success; a failed attempt (e.g. no tower
                // selected, can't afford it) leaves it Available and simply falls through below.
            }

            BuildNode occupiedNode = nearestNode != null && nearestNode.State == BuildNodeState.Occupied ? nearestNode : null;
            if (occupiedNode == _activeOccupiedNode)
            {
                return;
            }

            _activeOccupiedNode = occupiedNode;
            if (occupiedNode != null)
            {
                _towerSelectionService?.Select(occupiedNode.CurrentTower);
            }
            else
            {
                _towerSelectionService?.Clear();
            }
        }
    }
}
