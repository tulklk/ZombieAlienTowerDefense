using System.Globalization;

namespace AlienDefense.UI.MainMenu
{
    /// <summary>Compact resource-count formatting shared by every MainMenu resource widget (Coin, and any future
    /// currency), so no widget formats numbers on its own. 8900 -> "8.9K", 27600 -> "27.6K", 1250000 -> "1.25M".</summary>
    public static class CurrencyFormatter
    {
        private const int ThousandThreshold = 1000;
        private const int MillionThreshold = 1_000_000;

        public static string Format(int amount)
        {
            return Format((long)(amount < 0 ? 0 : amount));
        }

        public static string Format(long amount)
        {
            long clamped = amount < 0 ? 0 : amount;

            if (clamped < ThousandThreshold)
            {
                return clamped.ToString(CultureInfo.InvariantCulture);
            }

            if (clamped < MillionThreshold)
            {
                return FormatScaled(clamped / 1000.0, 1) + "K";
            }

            return FormatScaled(clamped / 1_000_000.0, 2) + "M";
        }

        private static string FormatScaled(double value, int decimals)
        {
            string formatted = value.ToString("F" + decimals, CultureInfo.InvariantCulture);
            if (formatted.IndexOf('.') >= 0)
            {
                formatted = formatted.TrimEnd('0').TrimEnd('.');
            }

            return formatted;
        }
    }
}
