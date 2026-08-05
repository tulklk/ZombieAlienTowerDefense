using AlienDefense.Towers;
using AlienDefense.Vfx;

namespace AlienDefense.Building
{
    /// <summary>Plays build/upgrade/sell VFX in response to BuildService/TowerUpgradeService/TowerSellService events. No gameplay logic.</summary>
    public sealed class BuildLifecycleVfxController
    {
        private readonly VfxService _vfxService;
        private readonly VfxDefinition _buildVfx;
        private readonly VfxDefinition _upgradeVfx;
        private readonly VfxDefinition _sellVfx;

        private BuildService _buildService;
        private TowerUpgradeService _upgradeService;
        private TowerSellService _sellService;

        public BuildLifecycleVfxController(VfxService vfxService, VfxDefinition buildVfx, VfxDefinition upgradeVfx, VfxDefinition sellVfx)
        {
            _vfxService = vfxService;
            _buildVfx = buildVfx;
            _upgradeVfx = upgradeVfx;
            _sellVfx = sellVfx;
        }

        public void Initialize(BuildService buildService, TowerUpgradeService upgradeService, TowerSellService sellService)
        {
            Unsubscribe();

            _buildService = buildService;
            _upgradeService = upgradeService;
            _sellService = sellService;

            if (_buildService != null)
            {
                _buildService.BuildCompleted += HandleBuildCompleted;
            }

            if (_upgradeService != null)
            {
                _upgradeService.TowerUpgraded += HandleTowerUpgraded;
            }

            if (_sellService != null)
            {
                _sellService.TowerSold += HandleTowerSold;
            }
        }

        private void HandleBuildCompleted(BuildNode node, TowerController tower)
        {
            _vfxService?.Play(_buildVfx, tower.transform.position, tower.transform.rotation);
        }

        private void HandleTowerUpgraded(TowerController tower)
        {
            _vfxService?.Play(_upgradeVfx, tower.transform.position, tower.transform.rotation);
        }

        private void HandleTowerSold(TowerController tower)
        {
            _vfxService?.Play(_sellVfx, tower.transform.position, tower.transform.rotation);
        }

        public void Unsubscribe()
        {
            if (_buildService != null)
            {
                _buildService.BuildCompleted -= HandleBuildCompleted;
            }

            if (_upgradeService != null)
            {
                _upgradeService.TowerUpgraded -= HandleTowerUpgraded;
            }

            if (_sellService != null)
            {
                _sellService.TowerSold -= HandleTowerSold;
            }
        }
    }
}
