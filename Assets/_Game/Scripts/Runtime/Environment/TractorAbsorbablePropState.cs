namespace AlienDefense.Environment
{
    /// <summary>Lifecycle of one TractorAbsorbableProp while it may be tractor-beamed.</summary>
    public enum TractorAbsorbablePropState
    {
        Idle = 0,
        Pulling = 1,
        Lifting = 2,
        Absorbed = 3
    }
}
