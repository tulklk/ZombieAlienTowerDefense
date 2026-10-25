using UnityEngine;

namespace AlienDefense.Base.Ambient
{
    /// <summary>Keeps an ambient resident's clips in place. The Mixamo clips carry the walk's forward travel in the
    /// hips bone, and on these Generic rigs the Animator leaves it there (measured: the body strides ~1 m ahead of
    /// the resident's root and snaps back every cycle). After the Animator has posed the skeleton this pins the hips'
    /// forward offset (model-space Z) to its rest value - side sway and the bob up and down are untouched - so the
    /// NPC script alone decides where the resident is. Sits on the model next to the Animator.</summary>
    [RequireComponent(typeof(Animator))]
    public sealed class AmbientInPlaceAnimation : MonoBehaviour
    {
        [SerializeField]
        private Transform _hips;

        [SerializeField]
        [Tooltip("Model-space forward offset the hips are held at (the rest pose's).")]
        private float _restForward;

        private void LateUpdate()
        {
            if (_hips == null)
            {
                return;
            }

            Vector3 local = transform.InverseTransformPoint(_hips.position);
            if (Mathf.Abs(local.z - _restForward) < 0.0001f)
            {
                return;
            }

            local.z = _restForward;
            _hips.position = transform.TransformPoint(local);
        }

        public void Configure(Transform hips, float restForward)
        {
            _hips = hips;
            _restForward = restForward;
        }
    }
}
