namespace AlienDefense.Economy
{
    /// <summary>Reward for this project's one daily quest ("Hoàn thành 1 màn hôm nay"). A single fixed quest
    /// keeps the first real implementation correct and testable; more quest types are a natural later addition
    /// once this loop (reset/complete/claim, all in PlayerProfileService) is proven.</summary>
    public static class DailyQuestRewardTable
    {
        public const int CoinReward = 30;
    }
}
