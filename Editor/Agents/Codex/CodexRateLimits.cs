using System;
using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Agents.Codex
{
    internal static class CodexRateLimits
    {
        public const string ReadMethod = "account/rateLimits/read";
        public const string UpdatedNotification = "account/rateLimits/updated";

        private const long SessionWindowMaxMinutes = 24 * 60;

        public static RateLimits FromSnapshot(JObject snapshot)
        {
            if (snapshot == null)
            {
                return null;
            }

            RateLimitWindow? session = null;
            RateLimitWindow? weekly = null;
            RateLimitWindow? primary = ParseWindow(snapshot["primary"] as JObject, true, out bool primaryIsSession);
            RateLimitWindow? secondary = ParseWindow(snapshot["secondary"] as JObject, false, out bool secondaryIsSession);
            if (primary.HasValue)
            {
                if (primaryIsSession)
                {
                    session = primary;
                }
                else
                {
                    weekly = primary;
                }
            }

            if (secondary.HasValue)
            {
                if (secondaryIsSession)
                {
                    session ??= secondary;
                }
                else
                {
                    weekly ??= secondary;
                }
            }

            return session.HasValue || weekly.HasValue ? new RateLimits(session, weekly, false, DateTimeOffset.Now) : null;
        }

        private static RateLimitWindow? ParseWindow(JObject window, bool primary, out bool isSession)
        {
            isSession = primary;
            double? used = (double?)window?["usedPercent"];
            if (!used.HasValue)
            {
                return null;
            }

            long? minutes = (long?)window["windowDurationMins"];
            if (minutes.HasValue)
            {
                isSession = minutes.Value <= SessionWindowMaxMinutes;
            }

            return new RateLimitWindow(used.Value, RateLimitTimes.Parse(window["resetsAt"]));
        }
    }
}
