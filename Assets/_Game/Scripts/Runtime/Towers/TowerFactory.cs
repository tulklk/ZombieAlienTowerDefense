using AlienDefense.Combat;
using AlienDefense.Core;
using AlienDefense.Enemies;
using AlienDefense.Vfx;
using UnityEngine;

namespace AlienDefense.Towers
{
    /// <summary>Instantiates and initializes one tower. Not pooled; payment is the caller's responsibility.</summary>
    public sealed class TowerFactory
    {
        private readonly Transform _towerRuntimeParent;
        private readonly EnemyRegistry _enemyRegistry;
        private readonly ProjectileFactory _projectileFactory;
        private readonly AreaDamageResolver _areaDamageResolver;
        private readonly GameFlowController _gameFlow;
        private readonly VfxService _vfxService;

        public TowerFactory(
            Transform towerRuntimeParent,
            EnemyRegistry enemyRegistry,
            ProjectileFactory projectileFactory,
            AreaDamageResolver areaDamageResolver,
            GameFlowController gameFlow,
            VfxService vfxService = null)
        {
            _towerRuntimeParent = towerRuntimeParent;
            _enemyRegistry = enemyRegistry;
            _projectileFactory = projectileFactory;
            _areaDamageResolver = areaDamageResolver;
            _gameFlow = gameFlow;
            _vfxService = vfxService;
        }

        public TowerController Create(TowerDefinition definition, Vector3 position, Quaternion rotation)
        {
            if (definition == null || definition.Prefab == null)
            {
                Debug.LogError("[TowerFactory] Cannot create a tower with a null TowerDefinition or Prefab.");
                return null;
            }

            TowerController tower = Object.Instantiate(definition.Prefab, position, rotation, _towerRuntimeParent);
            tower.Initialize(definition, _enemyRegistry, _projectileFactory, _areaDamageResolver, _gameFlow);
            tower.SetMuzzleVfx(_vfxService, definition.MuzzleVfxDefinition);

            return tower;
        }

        /// <summary>Destroys a tower instance. Used for build-transaction rollback and (later) selling.</summary>
        public void Destroy(TowerController tower)
        {
            if (tower == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(tower.gameObject);
            }
            else
            {
                Object.DestroyImmediate(tower.gameObject);
            }
        }
    }
}
