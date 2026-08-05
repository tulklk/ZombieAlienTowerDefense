namespace AlienDefense.Core
{
    /// <summary>
    /// Thin seam over the single piece of engine state <see cref="GameSpeedController"/> needs to touch.
    /// Exists so GameSpeedController's pause/resume/speed logic can be unit tested in EditMode
    /// without depending on UnityEngine.Time (which requires a running player loop).
    /// </summary>
    public interface ITimeScaleTarget
    {
        float TimeScale { get; set; }
    }
}
