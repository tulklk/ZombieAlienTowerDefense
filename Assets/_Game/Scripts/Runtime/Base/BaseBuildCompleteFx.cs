using UnityEngine;

namespace AlienDefense.Base
{
    /// <summary>The "construction complete" burst (golden flash, dust puff, sparkles), shared by every plot.
    ///
    /// One instance lives in the base world. Instead of instantiating an effect per completion it moves to the
    /// building and emits each child system's burst count - several buildings finishing in the same frame (offline
    /// completion) each get their own burst, because the systems simulate in world space.
    ///
    /// Systems flagged "raised" (flash, sparkles) are lifted to the roof so the building itself does not hide them;
    /// the others (dust) puff out around the base.</summary>
    public sealed class BaseBuildCompleteFx : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Systems to emit from. Leave empty to use every ParticleSystem under this object.")]
        private ParticleSystem[] _systems = System.Array.Empty<ParticleSystem>();

        [SerializeField]
        [Tooltip("Particles emitted per system, same order as Systems.")]
        private int[] _counts = System.Array.Empty<int>();

        [SerializeField]
        [Tooltip("Per system: emit at roof height (true) or at the ground (false).")]
        private bool[] _raised = System.Array.Empty<bool>();

        /// <param name="groundPosition">The plot's centre on the ground.</param>
        /// <param name="roofHeight">Height above the ground for the raised systems (roughly the roof).</param>
        public void Play(Vector3 groundPosition, float roofHeight)
        {
            if (_systems == null || _systems.Length == 0)
            {
                _systems = GetComponentsInChildren<ParticleSystem>(true);
            }

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            transform.position = groundPosition;
            for (int i = 0; i < _systems.Length; i++)
            {
                ParticleSystem system = _systems[i];
                if (system == null)
                {
                    continue;
                }

                bool raised = _raised != null && i < _raised.Length && _raised[i];
                system.transform.localPosition = raised ? new Vector3(0f, roofHeight, 0f) : new Vector3(0f, 0.3f, 0f);

                // The systems have no emission of their own (rate 0, no bursts); they only need to be running so
                // the particles emitted here are simulated.
                if (!system.isPlaying)
                {
                    system.Play(false);
                }

                int count = _counts != null && i < _counts.Length ? _counts[i] : 10;
                system.Emit(count);
            }
        }
    }
}
