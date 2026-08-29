namespace AlienDefense.Enemies
{
    /// <summary>Lets non-wave gameplay (e.g. BossController minions) spawn an enemy that still counts toward
    /// the current wave's active-enemy tracking, without ever touching WaveRuntimeTracker directly. Declared here
    /// (not in Waves) so Enemies never depends on Waves; WaveController implements it instead.</summary>
    public interface IEnemySpawnCoordinator
    {
        EnemyController SpawnTrackedEnemy(EnemyDefinition definition);
    }
}
