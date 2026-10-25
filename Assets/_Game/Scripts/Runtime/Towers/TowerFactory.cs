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
        private readonly TowerMetaUpgradeService _metaUpgradeService;
        private float _critChance;
        private float _critDamage = 1f;

        public TowerFactory(
            Transform towerRuntimeParent,
            EnemyRegistry enemyRegistry,
            ProjectileFactory projectileFactory,
            AreaDamageResolver areaDamageResolver,
            GameFlowController gameFlow,
            VfxService vfxService = null,
            TowerMetaUpgradeService metaUpgradeService = null)
        {
            _towerRuntimeParent = towerRuntimeParent;
            _enemyRegistry = enemyRegistry;
            _projectileFactory = projectileFactory;
            _areaDamageResolver = areaDamageResolver;
            _gameFlow = gameFlow;
            _vfxService = vfxService;
            _metaUpgradeService = metaUpgradeService;
        }

        /// <summary>Optional. Critical-hit numbers every tower built from now on gets (DefenseMetaConfig + the
        /// collection's permanent upgrade levels, resolved once per level by LevelCompositionRoot).</summary>
        public void SetCritical(float chance, float damageMultiplier)
        {
            _critChance = chance;
            _critDamage = damageMultiplier;
        }

        public TowerController Create(TowerDefinition definition, Vector3 position, Quaternion rotation)
        {
            if (definition == null || definition.Prefab == null)
            {
                Debug.LogError("[TowerFactory] Cannot create a tower with a null TowerDefinition or Prefab.");
                return null;
            }

            // Always built at its lowest level. The permanent (Defense tab) level is a separate thing: it only
            // multiplies the tower's shot damage, at whatever in-level level it has.
            TowerController tower = Object.Instantiate(definition.Prefab, position, rotation, _towerRuntimeParent);
            tower.Initialize(definition, _enemyRegistry, _projectileFactory, _areaDamageResolver, _gameFlow);
            tower.SetPermanentDamageMultiplier(_metaUpgradeService != null ? _metaUpgradeService.GetDamageMultiplier(definition) : 1f);
            tower.SetMuzzleVfx(_vfxService, definition.MuzzleVfxDefinition);
            tower.SetCritical(_critChance, _critDamage);

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
