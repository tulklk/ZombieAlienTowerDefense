using AlienDefense.Towers;
using UnityEngine;

namespace AlienDefense.Building
{
    /// <summary>A fixed 3D build slot. Holds no resource/UI knowledge and never spawns towers itself.</summary>
    public sealed class BuildNode : MonoBehaviour
    {
        [SerializeField]
        private Transform _buildPoint;

        [SerializeField]
        [Tooltip("Optional.")]
        private BuildNodeVisual _visual;

        [SerializeField]
        [Tooltip("Optional. The hologram ghost shown on this node while it's Available and a tower type is selected to build.")]
        private TowerHologramPreview _hologramPreview;

        [SerializeField]
        [Tooltip("Optional. The channel ring + Energy Ball cost badge driven by PlayerBuildNodeProximityController.")]
        private BuildNodeChannelUI _channelUI;

        public BuildNodeState State { get; private set; } = BuildNodeState.Available;
        public TowerController CurrentTower { get; private set; }
        public Transform BuildPoint => _buildPoint != null ? _buildPoint : transform;
        public BuildNodeChannelUI ChannelUI => _channelUI;

        private void Awake()
        {
            ApplyVisualState();
        }

        /// <summary>Occupies this node with an already-spawned tower. Intended caller: BuildService's build transaction only.</summary>
        public bool AssignTower(TowerController tower)
        {
            if (tower == null || State != BuildNodeState.Available)
            {
                return false;
            }

            CurrentTower = tower;
            State = BuildNodeState.Occupied;
            ApplyVisualState();
            return true;
        }

        /// <summary>Frees this node. Prepared for Phase 8 (sell); not called anywhere in Phase 7.</summary>
        public void ReleaseTower()
        {
            CurrentTower = null;
            State = BuildNodeState.Available;
            ApplyVisualState();
        }

        public void SetDisabled(bool disabled)
        {
            if (State == BuildNodeState.Occupied)
            {
                return;
            }

            State = disabled ? BuildNodeState.Disabled : BuildNodeState.Available;
            ApplyVisualState();
        }

        public void SetHighlighted(bool highlighted)
        {
            if (_visual != null)
            {
                _visual.SetHighlighted(highlighted);
            }
        }

        /// <summary>Shows the hologram ghost of towerPrefab (e.g. a TowerDefinition.Prefab.gameObject) on this
        /// node. Intended caller: BuildNodeVisualCoordinator, only while this node is Available.</summary>
        public void ShowHologramPreview(GameObject towerPrefab)
        {
            if (_hologramPreview != null)
            {
                _hologramPreview.ShowPreview(towerPrefab);
            }
        }

        public void HideHologramPreview()
        {
            if (_hologramPreview != null)
            {
                _hologramPreview.HidePreview();
            }
        }

        private void ApplyVisualState()
        {
            if (_visual != null)
            {
                _visual.SetState(State);
            }

            // A tower actually being built (Occupied) or the node going Disabled both mean the ghost preview no
            // longer belongs here, regardless of what the selection coordinator does next — see
            // TowerHologramPreview.ConfirmBuild's doc comment for why AssignTower's path counts as "confirmed".
            if (State != BuildNodeState.Available && _hologramPreview != null)
            {
                _hologramPreview.HidePreview();
            }
        }
    }
}
