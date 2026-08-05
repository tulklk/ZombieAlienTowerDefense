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

        public BuildNodeState State { get; private set; } = BuildNodeState.Available;
        public TowerController CurrentTower { get; private set; }
        public Transform BuildPoint => _buildPoint != null ? _buildPoint : transform;

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

        private void ApplyVisualState()
        {
            if (_visual != null)
            {
                _visual.SetState(State);
            }
        }
    }
}
