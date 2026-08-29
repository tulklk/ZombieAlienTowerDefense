using AlienDefense.Combat;

namespace AlienDefense.Enemies
{
    /// <summary>Adapts EnemyRegistry to ISplashTargetProvider so Combat/AreaDamageResolver never depends on Enemies.</summary>
    public sealed class EnemyRegistrySplashProvider : ISplashTargetProvider
    {
        private readonly EnemyRegistry _registry;

        public EnemyRegistrySplashProvider(EnemyRegistry registry)
        {
            _registry = registry;
        }

        public int Count => _registry.Count;
        public ICombatTarget GetAt(int index) => _registry.GetAt(index);
    }
}
