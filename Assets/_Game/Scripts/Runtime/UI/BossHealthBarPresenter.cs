using AlienDefense.Enemies;
using AlienDefense.Waves;
using UnityEngine;

namespace AlienDefense.UI
{
    /// <summary>Binds BossHealthBarView to whichever Boss is currently alive, listening to WaveController.BossSpawned.</summary>
    public sealed class BossHealthBarPresenter : MonoBehaviour
    {
        [SerializeField]
        private BossHealthBarView _view;

        private WaveController _waveController;
        private EnemyController _activeBoss;

        public void Initialize(WaveController waveController)
        {
            Unsubscribe();

            _waveController = waveController;
            if (_waveController != null)
            {
                _waveController.BossSpawned += HandleBossSpawned;
            }

            _view?.Hide();
        }

        private void HandleBossSpawned(EnemyController boss, BossController bossController)
        {
            _activeBoss = boss;
            boss.Health.HealthChanged += HandleHealthChanged;
            boss.Resolved += HandleBossResolved;

            _view?.Show(boss.Definition != null ? boss.Definition.DisplayName : "Boss");
        }

        private void HandleHealthChanged(float current, float max)
        {
            _view?.SetFill(max > 0f ? current / max : 0f);
        }

        private void HandleBossResolved(EnemyController resolvedEnemy, EnemyResolveReason reason)
        {
            UnsubscribeFromActiveBoss();
            _view?.Hide();
        }

        private void UnsubscribeFromActiveBoss()
        {
            if (_activeBoss == null)
            {
                return;
            }

            _activeBoss.Health.HealthChanged -= HandleHealthChanged;
            _activeBoss.Resolved -= HandleBossResolved;
            _activeBoss = null;
        }

        private void Unsubscribe()
        {
            if (_waveController != null)
            {
                _waveController.BossSpawned -= HandleBossSpawned;
            }

            UnsubscribeFromActiveBoss();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }
    }
}
