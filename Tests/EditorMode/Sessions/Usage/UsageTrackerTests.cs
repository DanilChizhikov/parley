using System;
using DTech.Parley.Editor;
using DTech.Parley.Editor.Sessions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class UsageTrackerTests
    {
        private static readonly DateTime Start = new (2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);

        [Test]
        public void RepeatedReportsOfOneCallAreMerged()
        {
            UsageLog log = new UsageLog();
            UsageTracker tracker = Tracker(log);
            tracker.BeginTurn("fix the bug", Start);
            tracker.Report(new UsageReport("m1", null, "opus", 3, 500, 20000, 1, 0), "fallback", Start);
            tracker.Report(new UsageReport("m1", null, null, 0, 0, 0, 250, 0), "fallback", Start);

            UsageCall call = log.Turns[0].Calls[0];
            Assert.AreEqual(1, log.Turns[0].Calls.Count);
            Assert.AreEqual("opus", call.Model);
            Assert.AreEqual(3, call.Input);
            Assert.AreEqual(500, call.CacheWrite);
            Assert.AreEqual(20000, call.CacheRead);
            Assert.AreEqual(250, call.Output);
            Assert.AreEqual(UsageTracker.MainAgent, call.Agent);
        }

        [Test]
        public void SyntheticAndEmptyReportsAreIgnored()
        {
            UsageLog log = new UsageLog();
            UsageTracker tracker = Tracker(log);
            tracker.BeginTurn("hi", Start);

            Assert.IsFalse(tracker.Report(new UsageReport("s1", null, "<synthetic>", 5, 0, 0, 5, 0), null, Start));
            Assert.IsFalse(tracker.Report(new UsageReport("s2", null, "opus", 0, 0, 0, 0, 0), null, Start));
            Assert.AreEqual(0, log.Turns[0].Calls.Count);
        }

        [Test]
        public void MissingModelFallsBackToSessionModel()
        {
            UsageLog log = new UsageLog();
            UsageTracker tracker = Tracker(log);
            tracker.Report(new UsageReport(null, null, null, 10, 0, 0, 5, 0), "gpt-5", Start);

            Assert.AreEqual(1, log.Turns.Count);
            Assert.AreEqual("gpt-5", log.Turns[0].Calls[0].Model);
            StringAssert.StartsWith("call-", log.Turns[0].Calls[0].Id);
        }

        [Test]
        public void ToolsAndThinkingAttachToCurrentCallOfTheirAgent()
        {
            UsageLog log = new UsageLog();
            UsageTracker tracker = Tracker(log);
            tracker.BeginTurn("explore", Start);
            tracker.Report(new UsageReport("m1", null, "opus", 1, 1, 1, 1, 0), null, Start);
            tracker.BlockFinalized(new BlockFinalizeRequest("k1", BlockKind.ToolUse, null, null, "agent1", "Agent",
                JObject.Parse("{\"subagent_type\":\"Explore\",\"description\":\"Find skills\"}")));
            tracker.Report(new UsageReport("m2", "agent1", "haiku", 1, 1, 1, 1, 0), null, Start);
            tracker.BlockFinalized(new BlockFinalizeRequest("k2", BlockKind.Thinking, "agent1", "abcd", null, null, null));
            tracker.BlockFinalized(new BlockFinalizeRequest("k3", BlockKind.ToolUse, "agent1", null, "read1", "Read",
                JObject.Parse("{\"file_path\":\"/project/Assets/A.cs\",\"offset\":10,\"limit\":20}")));
            tracker.ToolResult(new ToolResultRequest("read1", "0123456789", false, null));

            UsageTurn turn = log.Turns[0];
            Assert.AreEqual(4, turn.Calls[1].ThinkingChars);
            Assert.AreEqual(0, turn.Calls[0].ThinkingChars);
            Assert.AreEqual("Explore: Find skills", log.AgentLabels["agent1"]);
            UsageTool read = turn.Tools[1];
            Assert.AreEqual("m2", read.CallId);
            Assert.AreEqual("agent1", read.Agent);
            Assert.AreEqual("Assets/A.cs [10+20]", read.Target);
            Assert.AreEqual(10, read.ResultChars);
            Assert.AreEqual("m1", turn.Tools[0].CallId);
        }

        [Test]
        public void TurnCostIsDeltaOfCumulativeCost()
        {
            UsageLog log = new UsageLog();
            UsageTracker tracker = Tracker(log);
            tracker.BeginTurn("one", Start);
            tracker.CompleteTurn(new TurnResult { CostUsd = 0.5, DurationMs = 100 });
            tracker.BeginTurn("two", Start);
            tracker.CompleteTurn(new TurnResult { CostUsd = 0.8 });
            tracker.BeginTurn("three after restart", Start);
            tracker.CompleteTurn(new TurnResult { CostUsd = 0.1 });

            Assert.AreEqual(0.5, log.Turns[0].CostUsd.Value, 1e-9);
            Assert.AreEqual(100, log.Turns[0].DurationMs);
            Assert.AreEqual(0.3, log.Turns[1].CostUsd.Value, 1e-9);
            Assert.AreEqual(0.1, log.Turns[2].CostUsd.Value, 1e-9);
        }

        private static UsageTracker Tracker(UsageLog log)
        {
            UsageTracker tracker = new UsageTracker("/project");
            tracker.Bind(log);
            return tracker;
        }
    }
}
