using UnityEngine;

namespace AlienDefense.Vfx
{
    /// <summary>Funnels the takeoff energy streaks into the rising UFO: each particle leaves the landing pad straight
    /// up, then is steered sideways just enough to reach the UFO's vertical axis at the moment it reaches the UFO's
    /// height - so the stream draws a "\ | /" funnel into the saucer instead of parallel vertical lines.
    ///
    /// A script rather than a Particle System module because the funnel's point is moving: the UFO climbs several
    /// metres during the effect, and Velocity-over-Lifetime's radial/orbital offset is a fixed point relative to the
    /// emitter. Steering is velocity-only (never position), so particles glide rather than snap, and the upward
    /// component is left entirely to the emitter.
    ///
    /// Only meant to run for the second or two the takeoff lasts - UFOTakeoffVFX enables it with the emitter and
    /// disables it once the last streak is gone, so it costs nothing for the rest of the level. Expects the
    /// Particle System to simulate in World space.</summary>
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class UFOTakeoffParticleAttractor : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("What the stream funnels into - the anchor under the UFO's saucer.")]
        private Transform _target;

        [SerializeField, Min(0f)]
        [Tooltip("How quickly a particle's sideways velocity turns towards the course that meets the target " +
            "(m/s per second). Higher = tighter, more obvious funnel.")]
        private float _steerAcceleration = 14f;

        [SerializeField, Min(0.1f)]
        [Tooltip("Cap on sideways speed, so particles spawned far out on the pad still curve in smoothly instead of " +
            "whipping across.")]
        private float _maxSidewaysSpeed = 3.5f;

        [SerializeField, Min(0f)]
        [Tooltip("A particle that has climbed to within this height below the target is retired, so streaks fade " +
            "into the saucer instead of shooting out of its top.")]
        private float _arrivalHeight = 0.15f;

        private ParticleSystem _particles;
        private ParticleSystem.Particle[] _buffer;

        public Transform Target
        {
            get => _target;
            set => _target = value;
        }

        private void Awake()
        {
            _particles = GetComponent<ParticleSystem>();
            _buffer = new ParticleSystem.Particle[_particles.main.maxParticles];
        }

        private void LateUpdate()
        {
            if (_target == null || _buffer == null)
            {
                return;
            }

            float dt = Time.deltaTime;
            if (dt <= 0f)
            {
                return;
            }

            int count = _particles.GetParticles(_buffer);
            if (count == 0)
            {
                return;
            }

            Vector3 target = _target.position;

            for (int i = 0; i < count; i++)
            {
                ParticleSystem.Particle particle = _buffer[i];
                Vector3 position = particle.position;
                float heightBelow = target.y - position.y;

                if (heightBelow <= _arrivalHeight)
                {
                    particle.remainingLifetime = 0f; // reached the saucer
                    _buffer[i] = particle;
                    continue;
                }

                // Sideways velocity that lands on the target's axis exactly when the particle reaches its height,
                // at the particle's current climb rate. Recomputed every frame, so it tracks the UFO as it rises.
                Vector3 velocity = particle.velocity;
                float climbRate = Mathf.Max(velocity.y, 0.5f);
                float secondsToTarget = heightBelow / climbRate;

                var toAxis = new Vector3(target.x - position.x, 0f, target.z - position.z);
                Vector3 desiredSideways = Vector3.ClampMagnitude(toAxis / Mathf.Max(secondsToTarget, 0.05f), _maxSidewaysSpeed);

                var sideways = new Vector3(velocity.x, 0f, velocity.z);
                sideways = Vector3.MoveTowards(sideways, desiredSideways, _steerAcceleration * dt);

                particle.velocity = new Vector3(sideways.x, velocity.y, sideways.z);
                _buffer[i] = particle;
            }

            _particles.SetParticles(_buffer, count);
        }
    }
}
