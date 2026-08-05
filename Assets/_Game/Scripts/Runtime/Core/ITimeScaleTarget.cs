namespace AlienDefense.Core
{
    /// <summary>Abstraction over the engine's time scale.</summary>
    public interface ITimeScaleTarget
    {
        float TimeScale { get; set; }
    }
}
