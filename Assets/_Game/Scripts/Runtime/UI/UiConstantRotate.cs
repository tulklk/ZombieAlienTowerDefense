using UnityEngine;

namespace AlienDefense.UI
{
    /// <summary>Spins a UI element at a fixed rate. Used for the two halo rings behind the level-up popup, which
    /// turn at different speeds so they never line up and the backdrop keeps moving.
    ///
    /// Runs on unscaled time on purpose: SkillChoicePresenter pauses GameSpeed while the popup is open, so a
    /// scaled deltaTime would be 0 and the rings would freeze exactly when they are on screen.</summary>
    public sealed class UiConstantRotate : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Degrees per second around Z. Negative spins the other way.")]
        private float _degreesPerSecond = 12f;

        [SerializeField]
        [Tooltip("Starting angle, so two rings sharing this component do not begin aligned.")]
        private float _startAngle;

        private RectTransform _rectTransform;

        private void Awake()
        {
            _rectTransform = (RectTransform)transform;
        }

        private void OnEnable()
        {
            if (_rectTransform == null)
            {
                _rectTransform = (RectTransform)transform;
            }

            _rectTransform.localRotation = Quaternion.Euler(0f, 0f, _startAngle);
        }

        private void Update()
        {
            _rectTransform.Rotate(0f, 0f, _degreesPerSecond * Time.unscaledDeltaTime);
        }

        /// <summary>Lets a builder or another script set both values at once.</summary>
        public void Configure(float degreesPerSecond, float startAngle)
        {
            _degreesPerSecond = degreesPerSecond;
            _startAngle = startAngle;
        }
    }
}
