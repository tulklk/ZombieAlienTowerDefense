using UnityEngine;

namespace AlienDefense.Core
{
    /// <summary>Production <see cref="ITimeScaleTarget"/> backed by UnityEngine.Time.timeScale.</summary>
    public sealed class UnityTimeScaleTarget : ITimeScaleTarget
    {
        public float TimeScale
        {
            get => Time.timeScale;
            set => Time.timeScale = value;
        }
    }
}
