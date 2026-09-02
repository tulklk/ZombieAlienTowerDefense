namespace AlienDefense.UI.MainMenu
{
    /// <summary>Formats a level's display title from its 1-based catalog position. No localization system exists
    /// in this project yet (see MainMenu audit report) — this is the single swap point a future localization
    /// service would replace ("mainmenu.level_title" key) instead of "Màn chơi " + number being built inline in a View.</summary>
    public static class LevelTitleFormatter
    {
        public static string Format(int displayIndex)
        {
            return $"Màn chơi {displayIndex}";
        }
    }
}
