using System;
using System.Globalization;

namespace DTech.Parley.Editor.UI
{
    internal static class LimitsFormat
    {
        public const string SessionTitle = "5-hour session";
        public const string WeeklyTitle = "Weekly";
        public const string NotLoadedText = "Limits load when the chat starts.";
        public const string UnavailableText = "Plan limits are not available for this sign-in (API key or cloud provider).";

        private const int DaysForWeekday = 6;

        public static string Remaining(RateLimitWindow? window, DateTimeOffset now)
        {
            return window.HasValue ? Math.Floor(window.Value.RemainingPercent(now)).ToString("0", CultureInfo.InvariantCulture) + "% left" : "—";
        }

        public static string Countdown(TimeSpan span)
        {
            if (span.TotalMinutes < 1.0)
            {
                return "<1m";
            }

            if (span.TotalDays >= 1.0)
            {
                return (int)span.TotalDays + "d " + span.Hours + "h";
            }

            if (span.TotalHours >= 1.0)
            {
                return (int)span.TotalHours + "h " + span.Minutes + "m";
            }

            return span.Minutes + "m";
        }

        public static string ResetTime(DateTimeOffset resetsAt, DateTimeOffset now)
        {
            DateTime local = resetsAt.ToLocalTime().DateTime;
            DateTime today = now.ToLocalTime().Date;
            if (local.Date == today)
            {
                return local.ToString("HH:mm", CultureInfo.InvariantCulture);
            }

            string format = (local.Date - today).TotalDays <= DaysForWeekday ? "ddd HH:mm" : "d MMM HH:mm";
            return local.ToString(format, CultureInfo.InvariantCulture);
        }

        public static string Detail(RateLimitWindow? window, DateTimeOffset now)
        {
            if (!window.HasValue)
            {
                return "No data";
            }

            DateTimeOffset? resetsAt = window.Value.ResetsAt;
            if (!resetsAt.HasValue)
            {
                return "Reset time unknown";
            }

            if (window.Value.IsReset(now))
            {
                return "Reset at " + ResetTime(resetsAt.Value, now);
            }

            return "Resets in " + Countdown(resetsAt.Value - now) + " · " + ResetTime(resetsAt.Value, now);
        }

        public static string Tooltip(RateLimits limits, DateTimeOffset now)
        {
            if (limits == null)
            {
                return "Plan limits. " + NotLoadedText;
            }

            if (limits.Unavailable)
            {
                return UnavailableText;
            }

            return "Plan limits (click for details)\n"
                + SessionTitle + ": " + Remaining(limits.Session, now) + " · " + Detail(limits.Session, now) + "\n"
                + WeeklyTitle + ": " + Remaining(limits.Weekly, now) + " · " + Detail(limits.Weekly, now);
        }
    }
}
