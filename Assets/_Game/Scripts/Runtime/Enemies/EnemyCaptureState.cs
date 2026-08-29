namespace AlienDefense.Enemies
{
    /// <summary>Runtime phase of one enemy's tractor beam capture.</summary>
    public enum EnemyCaptureState
    {
        Inactive = 0,
        Pulling = 1,
        Lifting = 2,
        Completed = 3,
        Aborted = 4
    }
}
