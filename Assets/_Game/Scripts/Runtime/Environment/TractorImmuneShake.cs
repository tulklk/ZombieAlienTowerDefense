using DG.Tweening;
using UnityEngine;

namespace AlienDefense.Environment
{
    /// <summary>Marks a solid object the tractor beam can never swallow - the farm buildings, silos, barns and
    /// the like - and gives it the one bit of feedback that says so: a short, soft rocking motion whenever the
    /// beam passes over it. This is the deliberate counterpart to TractorAbsorbableProp: absence of BOTH
    /// components means an object simply ignores the beam entirely (ground, roads, bridges), which is why this
    /// is opt-in rather than "anything without TractorAbsorbableProp".
    ///
    /// Purely cosmetic. Never blocks the UFO, never changes its hover height (the UFO is meant to sail straight
    /// over these at a constant height, not climb onto their roofs), and never touches gameplay state.
    ///
    /// The wobble is a decaying sine driven by a single DOTween value tween rather than DOShakeRotation, whose
    /// randomised per-vibrato jitter reads as a rattle; a building being tugged at by a tractor beam wants a
    /// smooth rock that settles, and a sine that fades to exactly zero also guarantees the object returns to its
    /// authored rotation instead of drifting a fraction of a degree per shake.</summary>
    [DisallowMultipleComponent]
    public sealed class TractorImmuneShake : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Rocked instead of this Transform when assigned. Falls back to this Transform, which is " +
            "normally what you want - these models pivot at their base, so rocking the root looks like the " +
            "building is being tugged out of the ground.")]
        private Transform _visualRoot;

        [SerializeField, Range(0.5f, 15f)]
        [Tooltip("Peak lean, in degrees, at the very start of the wobble. Decays to zero across Duration.")]
        private float _shakeAngle = 3.5f;

        [SerializeField, Min(0.1f)]
        [Tooltip("Seconds the wobble takes to play out and settle.")]
        private float _shakeDuration = 0.7f;

        [SerializeField, Range(0.5f, 6f)]
        [Tooltip("How many full left-right rocks fit into Duration.")]
        private float _shakeCycles = 2.5f;

        [SerializeField, Min(0f)]
        [Tooltip("Side-to-side slide, in metres, added on top of the lean and decaying with it. Leave at 0 for " +
            "buildings and trees. Round things that pivot at their centre - the energy balls - need it: rocking " +
            "a sphere about its own centre looks exactly like standing still.")]
        private float _swayDistance;

        [SerializeField, Min(0f)]
        [Tooltip("Extra seconds after a wobble finishes before another may start. Without this the beam's scan " +
            "would retrigger the same building several times a second while the player parks on top of it.")]
        private float _cooldown = 0.8f;

        private Tweener _shakeTween;
        private Quaternion _baseRotation;
        private Vector3 _basePosition;
        private Vector3 _rockAxis = Vector3.right;
        private Vector3 _swayDirection = Vector3.forward;
        private float _cooldownRemaining;
        private float _phase;

        /// <summary>True while the wobble is playing or still cooling down - the beam skips these.</summary>
        public bool IsBusy => _cooldownRemaining > 0f;

        private void Awake()
        {
            if (_visualRoot == null)
            {
                _visualRoot = transform;
            }

            _baseRotation = _visualRoot.localRotation;
            _basePosition = _visualRoot.localPosition;

            // Fixed per instance rather than per shake, so a given building always rocks the same way instead of
            // picking a new direction each time the player flies back over it.
            Vector2 horizontal = Random.insideUnitCircle.normalized;
            if (horizontal.sqrMagnitude < 0.0001f)
            {
                horizontal = Vector2.right;
            }

            _rockAxis = new Vector3(horizontal.x, 0f, horizontal.y);

            // Perpendicular to the rock axis, i.e. the direction the top leans - the slide follows the lean.
            _swayDirection = new Vector3(-horizontal.y, 0f, horizontal.x);
        }

        private void Update()
        {
            if (_cooldownRemaining > 0f)
            {
                _cooldownRemaining -= Time.deltaTime;
            }
        }

        /// <summary>Called by UFOTractorBeamController while this object sits inside the beam. Safe to call every
        /// scan - it self-throttles via the cooldown.</summary>
        public void Nudge()
        {
            if (IsBusy || _visualRoot == null)
            {
                return;
            }

            _cooldownRemaining = _shakeDuration + _cooldown;
            _phase = 0f;

            _shakeTween?.Kill();
            _shakeTween = DOTween.To(() => _phase, p => { _phase = p; ApplyWobble(p); }, 1f, _shakeDuration)
                .SetEase(Ease.Linear) // the decay envelope below already shapes it; easing twice flattens the rock
                .OnComplete(RestorePose);
        }

        /// <summary>Cuts a wobble short and puts the visual back at rest - for when something else is about to
        /// animate it (the beam starting to lift an energy ball) or it goes back into a pool.</summary>
        public void StopShake()
        {
            if (_shakeTween == null)
            {
                return;
            }

            _shakeTween.Kill();
            _shakeTween = null;
            RestorePose();
        }

        private void RestorePose()
        {
            if (_visualRoot == null)
            {
                return;
            }

            _visualRoot.localRotation = _baseRotation;
            if (_swayDistance > 0f)
            {
                _visualRoot.localPosition = _basePosition;
            }
        }

        /// <summary>Decaying sine: full amplitude at t=0, exactly zero at t=1.</summary>
        private void ApplyWobble(float t)
        {
            float decay = 1f - t;
            float angle = _shakeAngle * decay * Mathf.Sin(t * Mathf.PI * 2f * _shakeCycles);

            // Pre-multiplied so the lean is measured around a horizontal axis in the parent's space rather than
            // the model's own - imported models bake wildly different local axes.
            _visualRoot.localRotation = Quaternion.AngleAxis(angle, _rockAxis) * _baseRotation;

            if (_swayDistance > 0f)
            {
                float sway = _swayDistance * decay * Mathf.Sin(t * Mathf.PI * 2f * _shakeCycles);
                _visualRoot.localPosition = _basePosition + _swayDirection * sway;
            }
        }

        private void OnDisable()
        {
            // Pooled energy balls are deactivated mid-shake; without this they would come back off-centre.
            StopShake();
            _cooldownRemaining = 0f;
        }

        private void OnDestroy()
        {
            _shakeTween?.Kill();
            _shakeTween = null;
        }
    }
}
