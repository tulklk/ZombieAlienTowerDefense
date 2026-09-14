namespace AlienDefense.Waves
{
    /// <summary>Progress of a level's closing boss encounter (see BossEncounterDefinition / WaveController).</summary>
    public enum BossEncounterPhase
    {
        None = 0,      // level has no boss encounter
        Pending = 1,   // normal waves still running
        Spawning = 2,  // normal waves cleared, boss group about to appear
        Intro = 3,     // boss group on the map, frozen for the intro cinematic
        Fight = 4,     // boss group released
        Cleared = 5    // boss group resolved - level complete
    }
}
