using UnityEngine;

namespace AlienDefense.Base.River
{
    /// <summary>Every ripple and wake mark on the river comes from this one ParticleSystem: flat rings that grow and
    /// fade. Emitting into a single world-space system is the pool - Unity recycles the particles, nothing is
    /// instantiated or destroyed, and all ripples draw in one call.</summary>
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class WaterRipplePool : MonoBehaviour
    {
        private ParticleSystem _system;
        private ParticleSystem.EmitParams _params;

        private void Awake()
        {
            EnsureSystem();
        }

        private void EnsureSystem()
        {
            if (_system != null)
            {
                return;
            }

            _system = GetComponent<ParticleSystem>();
            _params = new ParticleSystem.EmitParams { applyShapeToPosition = false };
        }

        /// <param name="size">Start diameter in metres (the system's size-over-lifetime grows it).</param>
        /// <param name="lifetime">Seconds until fully faded.</param>
        public void Emit(Vector3 position, float size, float lifetime, float alpha = 1f)
        {
            EnsureSystem();
            if (!_system.isPlaying)
            {
                _system.Play(false);
            }

            _params.position = position;
            _params.startSize = size;
            _params.startLifetime = lifetime;
            _params.startColor = new Color(1f, 1f, 1f, alpha);
            _system.Emit(_params, 1);
        }
    }
}
