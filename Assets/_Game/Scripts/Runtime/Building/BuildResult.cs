namespace AlienDefense.Building
{
    /// <summary>Outcome of one BuildService.TryBuild call.</summary>
    public enum BuildResult
    {
        Success = 0,
        NoTowerSelected = 1,
        InvalidNode = 2,
        NodeUnavailable = 3,
        NotEnoughResource = 4,
        GameNotPlaying = 5,
        FactoryUnavailable = 6,
        SpawnFailed = 7,
        PaymentFailed = 8
    }
}
