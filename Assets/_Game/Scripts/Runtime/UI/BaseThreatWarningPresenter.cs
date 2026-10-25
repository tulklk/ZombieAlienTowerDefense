using AlienDefense.Base;
using AlienDefense.Enemies;
using UnityEngine;

namespace AlienDefense.UI
{
    /// <summary>Shows the "Base under attack!" warning while any enemy is within Warning Radius of the base, and
    /// flashes it when the base actually takes damage.
    ///
    /// Why a poll and not a trigger collider: enemies leave play in several ways that never raise OnTriggerExit
    /// (returned to the pool / deactivated, captured by the beam, reaching the base), which is exactly how a trigger
    /// count gets stuck ON. The EnemyRegistry is the one authoritative list of enemies in play (an enemy is removed
    /// the moment it is killed, captured, pooled or reaches the base), so the threat state is recomputed from it a
    /// few times a second: a squared XZ distance per live enemy (capped by Max Alive, ~30), no allocations, no
    /// physics. Hysteresis (Off Delay) keeps the warning from flickering when enemies stream in and out.</summary>
    public sealed class BaseThreatWarningPresenter : MonoBehaviour
    {
        [SerializeField]
        private BaseThreatWarningView _view;

        [SerializeField, Min(1f)]
        [Tooltip("Metres from the base target (the end of the enemy route) at which an enemy counts as a threat.")]
        private float _warningRadius = 12f;

        [SerializeField, Min(0.02f)]
        [Tooltip("Seconds between threat checks.")]
        private float _checkInterval = 0.1f;

        [SerializeField, Min(0f)]
        [Tooltip("The warning only goes off after this long with no enemy in range (prevents flicker).")]
        private float _offDelay = 0.35f;

        private EnemyRegistry _enemies;
        private BaseHealthService _baseHealth;
        private Vector3 _basePoint;
        private float _nextCheck;
        private float _clearSince = -1f;
        private bool _threatened;
        private int _lastHealth;
        private bool _initialized;

        /// <summary>Number of enemies inside the warning radius at the last check (for tests / debugging).</summary>
        public int ThreatCount { get; private set; }

        public bool IsWarningShown => _threatened;

        public void Initialize(EnemyRegistry enemies, Vector3 basePoint, BaseHealthService baseHealth)
        {
            Unbind();
            _enemies = enemies;
            _basePoint = basePoint;
            _baseHealth = baseHealth;
            if (_baseHealth != null)
            {
                _lastHealth = _baseHealth.CurrentHealth;
                _baseHealth.HealthChanged += HandleBaseHealthChanged;
            }

            _threatened = false;
            _clearSince = -1f;
            _view?.HideImmediate();
            _initialized = enemies != null;
        }

        private void Update()
        {
            if (!_initialized || Time.time < _nextCheck)
            {
                return;
            }

            _nextCheck = Time.time + _checkInterval;
            ThreatCount = CountThreats();
            if (ThreatCount > 0)
            {
                _clearSince = -1f;
                if (!_threatened)
                {
                    _threatened = true;
                    _view?.Show();
                }

                return;
            }

            if (!_threatened)
            {
                return;
            }

            if (_clearSince < 0f)
            {
                _clearSince = Time.time;
            }
            else if (Time.time - _clearSince >= _offDelay)
            {
                _threatened = false;
                _view?.Hide();
            }
        }

        private int CountThreats()
        {
            float radiusSqr = _warningRadius * _warningRadius;
            int count = 0;
            for (int i = 0; i < _enemies.Count; i++)
            {
                EnemyController enemy = _enemies.GetAt(i);
                if (enemy == null || !enemy.IsCombatActive)
                {
                    continue;
                }

                Vector3 d = enemy.transform.position - _basePoint;
                if (d.x * d.x + d.z * d.z <= radiusSqr)
                {
                    count++;
                }
            }

            return count;
        }

        private void HandleBaseHealthChanged(int current, int max)
        {
            if (current < _lastHealth)
            {
                _view?.FlashDamage();
            }

            _lastHealth = current;
        }

        private void Unbind()
        {
            if (_baseHealth != null)
            {
                _baseHealth.HealthChanged -= HandleBaseHealthChanged;
                _baseHealth = null;
            }
        }

        private void OnDestroy()
        {
            Unbind();
        }
    }
}
