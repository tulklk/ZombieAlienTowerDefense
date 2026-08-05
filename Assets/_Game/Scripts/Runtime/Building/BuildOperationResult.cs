using AlienDefense.Towers;

namespace AlienDefense.Building
{
    /// <summary>Immutable outcome of a build attempt: status, the spawned tower if any, and resource left.</summary>
    public readonly struct BuildOperationResult
    {
        public readonly BuildResult Status;
        public readonly TowerController SpawnedTower;
        public readonly int RemainingResource;

        public BuildOperationResult(BuildResult status, TowerController spawnedTower, int remainingResource)
        {
            Status = status;
            SpawnedTower = spawnedTower;
            RemainingResource = remainingResource;
        }

        public bool IsSuccess => Status == BuildResult.Success;
    }
}
