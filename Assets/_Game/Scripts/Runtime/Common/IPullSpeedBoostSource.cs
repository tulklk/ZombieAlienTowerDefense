namespace AlienDefense.Common
{
    /// <summary>Optional live multiplier stacked on top of a tractor-beam Pull/Lift request's base PullSpeed/
    /// LiftSpeed. Read fresh every frame by the pulled object's own Tick (see TractorPullLiftMotion callers and
    /// EnemyCaptureController) rather than baked into the request once at admission time — so it can react
    /// immediately to something that changes mid-pull, such as the Player currently moving, without needing the
    /// object to be re-admitted. A request with no source assigned (null) behaves exactly as before every caller
    /// reads it as `SpeedBoost?.PullSpeedMultiplier ?? 1f`.</summary>
    public interface IPullSpeedBoostSource
    {
        float PullSpeedMultiplier { get; }
    }
}
