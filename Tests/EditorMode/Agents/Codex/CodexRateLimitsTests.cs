using System;
using DTech.Parley.Editor;
using DTech.Parley.Editor.Agents.Codex;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class CodexRateLimitsTests
    {
        [Test]
        public void WindowsAreMappedByDuration()
        {
            JObject snapshot = JObject.Parse("{\"primary\":{\"usedPercent\":70,\"windowDurationMins\":10080,\"resetsAt\":1792500000},"
                + "\"secondary\":{\"usedPercent\":12,\"windowDurationMins\":300,\"resetsAt\":1792000000}}");

            RateLimits limits = CodexRateLimits.FromSnapshot(snapshot);

            Assert.AreEqual(12.0, limits.Session.Value.UsedPercent, 0.001);
            Assert.AreEqual(DateTimeOffset.FromUnixTimeSeconds(1792000000), limits.Session.Value.ResetsAt.Value);
            Assert.AreEqual(70.0, limits.Weekly.Value.UsedPercent, 0.001);
        }

        [Test]
        public void MissingDurationsFallBackToPrimaryAndSecondary()
        {
            JObject snapshot = JObject.Parse("{\"primary\":{\"usedPercent\":5},\"secondary\":{\"usedPercent\":40,\"windowDurationMins\":null}}");

            RateLimits limits = CodexRateLimits.FromSnapshot(snapshot);

            Assert.AreEqual(5.0, limits.Session.Value.UsedPercent, 0.001);
            Assert.IsFalse(limits.Session.Value.ResetsAt.HasValue);
            Assert.AreEqual(40.0, limits.Weekly.Value.UsedPercent, 0.001);
        }

        [Test]
        public void NullSecondaryLeavesWeeklyEmpty()
        {
            RateLimits limits = CodexRateLimits.FromSnapshot(JObject.Parse("{\"primary\":{\"usedPercent\":5,\"windowDurationMins\":300},\"secondary\":null}"));

            Assert.IsTrue(limits.Session.HasValue);
            Assert.IsFalse(limits.Weekly.HasValue);
            Assert.IsNull(CodexRateLimits.FromSnapshot(JObject.Parse("{\"primary\":null,\"secondary\":null}")));
        }

        [Test]
        public void UpdatedNotificationReachesSink()
        {
            RecordingSink sink = new RecordingSink();
            CodexEventMapper mapper = new CodexEventMapper(sink);
            mapper.Handle(CodexRateLimits.UpdatedNotification, JObject.Parse("{\"rateLimits\":{\"primary\":{\"usedPercent\":33,\"windowDurationMins\":300,\"resetsAt\":1792000000}}}"));

            Assert.AreEqual(1, sink.RateLimitUpdates.Count);
            Assert.AreEqual(33.0, sink.RateLimitUpdates[0].Session.Value.UsedPercent, 0.001);
        }
    }
}
