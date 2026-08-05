using UnityEngine;

namespace AlienDefense.Towers
{
    /// <summary>Creates the ITargetingStrategy instance matching a TargetingMode.</summary>
    public static class TargetingStrategyFactory
    {
        public static ITargetingStrategy Create(TargetingMode mode)
        {
            switch (mode)
            {
                case TargetingMode.First:
                    return new FirstTargetStrategy();
                case TargetingMode.Closest:
                    return new ClosestTargetStrategy();
                case TargetingMode.Strongest:
                    return new StrongestTargetStrategy();
                default:
                    Debug.LogError($"[TargetingStrategyFactory] Unhandled TargetingMode '{mode}'; defaulting to FirstTargetStrategy.");
                    return new FirstTargetStrategy();
            }
        }
    }
}
