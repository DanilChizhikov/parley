using System;
using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Agents.Claude
{
	internal static class ClaudeRateLimits
	{
		private const string SessionWindow = "five_hour";
		private const string WeeklyWindow = "seven_day";
		private const double FractionToPercent = 100.0;

		public static RateLimits FromUsage(JObject response)
		{
			if (response == null)
			{
				return null;
			}

			if ((bool?)response["rate_limits_available"] == false)
			{
				return RateLimits.NotAvailable();
			}

			if (!(response["rate_limits"] is JObject limits))
			{
				return null;
			}

			RateLimitWindow? session = UsageWindow(limits[SessionWindow]);
			RateLimitWindow? weekly = UsageWindow(limits[WeeklyWindow]);
			return session.HasValue || weekly.HasValue ? new RateLimits(session, weekly, false, DateTimeOffset.Now) : null;
		}

		public static RateLimits FromRateLimitInfo(JObject info)
		{
			if (info == null)
			{
				return null;
			}

			RateLimitWindow? session = null;
			RateLimitWindow? weekly = null;
			if (info["unifiedWindows"] is JObject windows)
			{
				session = EventWindow(windows[SessionWindow] as JObject);
				weekly = EventWindow(windows[WeeklyWindow] as JObject);
			}

			if (!session.HasValue && !weekly.HasValue)
			{
				RateLimitWindow? current = EventWindow(info);
				switch ((string)info["rateLimitType"])
				{
					case SessionWindow:
						session = current;
						break;
					case WeeklyWindow:
						weekly = current;
						break;
				}
			}

			return session.HasValue || weekly.HasValue ? new RateLimits(session, weekly, false, DateTimeOffset.Now) : null;
		}

		private static RateLimitWindow? UsageWindow(JToken token)
		{
			if (!(token is JObject window))
			{
				return null;
			}

			double? utilization = (double?)window["utilization"];
			return utilization.HasValue ? new RateLimitWindow(utilization.Value, RateLimitTimes.Parse(window["resets_at"])) : (RateLimitWindow?)null;
		}

		private static RateLimitWindow? EventWindow(JObject window)
		{
			double? utilization = (double?)window?["utilization"];
			if (!utilization.HasValue)
			{
				return null;
			}

			return new RateLimitWindow(utilization.Value * FractionToPercent, RateLimitTimes.Parse(window["resetsAt"]));
		}
	}
}
