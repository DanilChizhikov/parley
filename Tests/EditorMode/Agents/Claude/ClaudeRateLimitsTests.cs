using System;
using DTech.Parley.Editor;
using DTech.Parley.Editor.Agents.Claude;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class ClaudeRateLimitsTests
    {
        [Test]
        public void GetUsageReadsBothWindows()
        {
            JObject response = JObject.Parse("{\"subscription_type\":\"max\",\"rate_limits_available\":true,\"rate_limits\":{"
                + "\"five_hour\":{\"utilization\":38.5,\"resets_at\":\"2026-10-22T14:00:00+00:00\"},"
                + "\"seven_day\":{\"utilization\":61,\"resets_at\":\"2026-10-25T09:00:00Z\"}}}");

            RateLimits limits = ClaudeRateLimits.FromUsage(response);

            Assert.IsFalse(limits.Unavailable);
            Assert.AreEqual(38.5, limits.Session.Value.UsedPercent, 0.001);
            Assert.AreEqual(new DateTimeOffset(2026, 10, 22, 14, 0, 0, TimeSpan.Zero), limits.Session.Value.ResetsAt.Value);
            Assert.AreEqual(61.0, limits.Weekly.Value.UsedPercent, 0.001);
            Assert.AreEqual(new DateTimeOffset(2026, 10, 25, 9, 0, 0, TimeSpan.Zero), limits.Weekly.Value.ResetsAt.Value);
        }

        [Test]
        public void GetUsageWithoutPlanLimitsIsUnavailable()
        {
            RateLimits limits = ClaudeRateLimits.FromUsage(JObject.Parse("{\"subscription_type\":null,\"rate_limits_available\":false,\"rate_limits\":null}"));

            Assert.IsTrue(limits.Unavailable);
            Assert.IsFalse(limits.HasWindows);
        }

        [Test]
        public void GetUsageWithNullWindowsGivesNothing()
        {
            Assert.IsNull(ClaudeRateLimits.FromUsage(JObject.Parse("{\"rate_limits_available\":true,\"rate_limits\":null}")));
            Assert.IsNull(ClaudeRateLimits.FromUsage(JObject.Parse("{\"rate_limits_available\":true,\"rate_limits\":{\"five_hour\":null,\"seven_day\":{\"utilization\":null,\"resets_at\":null}}}")));
        }

        [Test]
        public void UnifiedWindowsAreFractionsOfOne()
        {
            JObject info = JObject.Parse("{\"status\":\"allowed\",\"unifiedWindows\":{"
                + "\"five_hour\":{\"utilization\":0.25,\"resetsAt\":1792000000},"
                + "\"seven_day\":{\"utilization\":1.2,\"resetsAt\":1792500000}}}");

            RateLimits limits = ClaudeRateLimits.FromRateLimitInfo(info);
            DateTimeOffset before = DateTimeOffset.FromUnixTimeSeconds(1791000000);

            Assert.AreEqual(25.0, limits.Session.Value.UsedPercent, 0.001);
            Assert.AreEqual(75.0, limits.Session.Value.RemainingPercent(before), 0.001);
            Assert.AreEqual(DateTimeOffset.FromUnixTimeSeconds(1792000000), limits.Session.Value.ResetsAt.Value);
            Assert.AreEqual(0.0, limits.Weekly.Value.RemainingPercent(before), 0.001);
        }

        [Test]
        public void TopLevelFallbackFillsOnlyItsWindow()
        {
            JObject info = JObject.Parse("{\"status\":\"allowed_warning\",\"rateLimitType\":\"seven_day\",\"utilization\":0.9,\"resetsAt\":1792500000}");

            RateLimits limits = ClaudeRateLimits.FromRateLimitInfo(info);

            Assert.IsFalse(limits.Session.HasValue);
            Assert.AreEqual(90.0, limits.Weekly.Value.UsedPercent, 0.001);
        }

        [Test]
        public void RateLimitEventReachesSinkWithoutNoticeWhenAllowed()
        {
            RecordingSink sink = new RecordingSink();
            ClaudeStreamMapper mapper = new ClaudeStreamMapper(sink);
            mapper.Handle(JObject.Parse("{\"type\":\"rate_limit_event\",\"rate_limit_info\":{\"status\":\"allowed\",\"unifiedWindows\":{\"five_hour\":{\"utilization\":0.1,\"resetsAt\":1792000000}}}}"));

            Assert.AreEqual(1, sink.RateLimitUpdates.Count);
            Assert.AreEqual(10.0, sink.RateLimitUpdates[0].Session.Value.UsedPercent, 0.001);
            Assert.AreEqual(0, sink.Notices.Count);
        }

        [Test]
        public void MergeKeepsWindowsMissingFromUpdate()
        {
            DateTimeOffset now = DateTimeOffset.Now;
            RateLimits first = new RateLimits(new RateLimitWindow(10.0, null), new RateLimitWindow(20.0, null), false, now);
            RateLimits merged = first.MergedWith(new RateLimits(new RateLimitWindow(30.0, null), null, false, now));

            Assert.AreEqual(30.0, merged.Session.Value.UsedPercent, 0.001);
            Assert.AreEqual(20.0, merged.Weekly.Value.UsedPercent, 0.001);
            Assert.IsFalse(merged.MergedWith(RateLimits.NotAvailable()).Unavailable);
        }

        [Test]
        public void PassedResetMeansFullWindow()
        {
            DateTimeOffset now = DateTimeOffset.Now;
            RateLimitWindow window = new RateLimitWindow(80.0, now.AddMinutes(-1));

            Assert.IsTrue(window.IsReset(now));
            Assert.AreEqual(100.0, window.RemainingPercent(now), 0.001);
        }
    }
}
