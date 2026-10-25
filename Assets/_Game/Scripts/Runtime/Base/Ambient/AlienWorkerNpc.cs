using UnityEngine;

namespace AlienDefense.Base.Ambient
{
    /// <summary>A BaseWorld resident that stands at its work spot and loops its Working animation (decoration
    /// only). The spot and what it looks at are anchors under the plot root - never under the building model, which
    /// BaseBuildingView replaces on every upgrade - so the worker survives any building change. It does not move or
    /// turn; the clip plays in place (root motion off, AmbientInPlaceAnimation on the model).
    ///
    /// With a Required Building it only shows up once that plot is built (user, 2026-10-25): it listens to
    /// BaseBuildingView.BuildingChanged rather than polling, and hides its visual (model + Animator) meanwhile.</summary>
    public sealed class AlienWorkerNpc : MonoBehaviour
    {
        [SerializeField]
        private Animator _animator;

        [SerializeField]
        [Tooltip("The model branch switched off while the building does not exist yet (VisualRoot).")]
        private GameObject _visual;

        [SerializeField]
        [Tooltip("Optional. The worker only appears once this plot has a finished building. Empty = always there.")]
        private BaseBuildingView _requiredBuilding;

        [SerializeField]
        [Tooltip("Where the worker stands (a persistent child of the plot root).")]
        private Transform _workAnchor;

        [SerializeField]
        [Tooltip("Optional. What the worker faces - the bench, the machine, the building. Empty = the anchor's own forward.")]
        private Transform _lookTarget;

        [SerializeField]
        [Tooltip("Animator state that loops while working.")]
        private string _workingState = "Working";

        private int _workingHash;

        private void Awake()
        {
            _workingHash = Animator.StringToHash(_workingState);
        }

        private void OnEnable()
        {
            if (_workAnchor != null)
            {
                Vector3 position = _workAnchor.position;
                Vector3 facing = _lookTarget != null ? _lookTarget.position - position : _workAnchor.forward;
                facing.y = 0f;
                transform.SetPositionAndRotation(position,
                    facing.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(facing) : _workAnchor.rotation);
            }

            if (_requiredBuilding != null)
            {
                _requiredBuilding.BuildingChanged += HandleBuildingChanged;
            }

            Refresh(forceRestart: true);
        }

        private void OnDisable()
        {
            if (_requiredBuilding != null)
            {
                _requiredBuilding.BuildingChanged -= HandleBuildingChanged;
            }
        }

        private void HandleBuildingChanged(BaseBuildingView view)
        {
            Refresh(forceRestart: false);
        }

        private void Refresh(bool forceRestart)
        {
            bool show = _requiredBuilding == null || _requiredBuilding.HasBuilding;
            bool wasShown = _visual == null || _visual.activeSelf;
            if (_visual != null && _visual.activeSelf != show)
            {
                _visual.SetActive(show);
            }

            if (show && (forceRestart || !wasShown) && _animator != null)
            {
                _animator.applyRootMotion = false;
                _animator.speed = 1f;
                _animator.Play(_workingHash, 0, Random.value);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (_workAnchor == null)
            {
                return;
            }

            Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.9f);
            Gizmos.DrawWireSphere(_workAnchor.position + Vector3.up * 0.2f, 0.5f);
            if (_lookTarget != null)
            {
                Gizmos.DrawLine(_workAnchor.position + Vector3.up * 0.8f, _lookTarget.position);
            }
        }
#endif
    }
}
