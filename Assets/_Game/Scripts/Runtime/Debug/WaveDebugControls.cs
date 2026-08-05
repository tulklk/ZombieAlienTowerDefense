using AlienDefense.Enemies;
using AlienDefense.Waves;
using UnityEngine;

namespace AlienDefense.DebugTools
{
    /// <summary>Manual wave controls for testing before a real Start Wave UI button exists.</summary>
    public sealed class WaveDebugControls : MonoBehaviour
    {
        [SerializeField]
        private WaveController _waveController;

        private EnemyRegistry _enemyRegistry;

        public void Initialize(WaveController waveController, EnemyRegistry enemyRegistry)
        {
            _waveController = waveController;
            _enemyRegistry = enemyRegistry;
        }

#if UNITY_EDITOR
        [ContextMenu("Start First Wave")]
#endif
        public void StartFirstWave()
        {
            if (!ValidateWaveController())
            {
                return;
            }

            _waveController.StartFirstWave();
        }

#if UNITY_EDITOR
        [ContextMenu("Start Next Prepared Wave")]
#endif
        public void StartNextPreparedWave()
        {
            if (!ValidateWaveController())
            {
                return;
            }

            _waveController.RequestStartPreparedWave();
        }

#if UNITY_EDITOR
        [ContextMenu("Resolve All Active Enemies As Removed")]
#endif
        public void ResolveAllActiveEnemiesAsRemoved()
        {
            if (_enemyRegistry == null)
            {
                Debug.LogWarning("[WaveDebugControls] Not initialized with an EnemyRegistry yet.", this);
                return;
            }

            while (_enemyRegistry.Count > 0)
            {
                EnemyController enemy = _enemyRegistry.GetAt(0);
                enemy.ForceResolve(EnemyResolveReason.Removed);
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Stop Waves")]
#endif
        public void StopWaves()
        {
            if (!ValidateWaveController())
            {
                return;
            }

            _waveController.StopWaves();
        }

#if UNITY_EDITOR
        [ContextMenu("Reset Waves")]
#endif
        public void ResetWaves()
        {
            if (!ValidateWaveController())
            {
                return;
            }

            _waveController.ResetWaves();
        }

        private bool ValidateWaveController()
        {
            if (_waveController != null)
            {
                return true;
            }

            Debug.LogWarning("[WaveDebugControls] Not initialized with a WaveController yet.", this);
            return false;
        }
    }
}
