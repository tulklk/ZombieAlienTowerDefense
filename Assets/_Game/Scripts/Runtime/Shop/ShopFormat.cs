using System;
using System.Globalization;

namespace AlienDefense.Shop
{
    /// <summary>Shop text formats, one place so every card agrees: prices "59,000 VND", countdowns "5d 00:35:27",
    /// privilege time "29d 12h". Compact counts reuse the MainMenu CurrencyFormatter ("1.28K").</summary>
    public static class ShopFormat
    {
        public static string Vnd(int amount)
        {
            return amount.ToString("#,0", CultureInfo.InvariantCulture) + " VND";
        }

        public static string Compact(int amount)
        {
            return UI.MainMenu.CurrencyFormatter.Format(amount);
        }

        /// <summary>"5d 00:35:27" (or "00:35:27" under a day).</summary>
        public static string Countdown(TimeSpan time)
        {
            if (time < TimeSpan.Zero)
            {
                time = TimeSpan.Zero;
            }

            string clock = time.Hours.ToString("00") + ":" + time.Minutes.ToString("00") + ":" + time.Seconds.ToString("00");
            return time.Days > 0 ? time.Days + "d " + clock : clock;
        }

        /// <summary>"29d 12h", "5h 20m", "45m".</summary>
        public static string Remaining(TimeSpan time)
        {
            if (time.TotalDays >= 1)
            {
                return time.Days + "d " + time.Hours + "h";
            }

            if (time.TotalHours >= 1)
            {
                return time.Hours + "h " + time.Minutes + "m";
            }

            return Math.Max(1, time.Minutes) + "m";
        }
    }
}
