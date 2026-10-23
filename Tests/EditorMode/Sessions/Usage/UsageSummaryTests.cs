using System;
using System.Linq;
using System.Xml.Linq;
using DTech.Parley.Editor;
using DTech.Parley.Editor.Sessions;
using DTech.Parley.Editor.Settings;
using NUnit.Framework;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class UsageSummaryTests
    {
        private static readonly DateTime Start = new (2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);

        [Test]
        public void TotalsGroupsAndCacheRebuilds()
        {
            UsageLog log = SampleLog();
            UsageSummary summary = UsageSummary.Build(log, 1);

            Assert.AreEqual(3, summary.Total.Calls);
            Assert.AreEqual(100000 + 2000 + 1000, summary.Total.CacheWrite);
            Assert.AreEqual(2, summary.Models.Count);
            Assert.AreEqual(2, summary.Agents.Count);
            Assert.AreEqual("Explore: look", summary.Agents.Single(group => group.Key == "agent1").Label);
            Assert.AreEqual(101000, summary.PeakContext);
            Assert.AreEqual(1, summary.CacheRebuilds);
            Assert.IsTrue(summary.IsCacheRebuild(log.Turns[1].Calls[0]));
            Assert.AreEqual(3600.0, summary.GapSeconds(log.Turns[1].Calls[0]).Value, 1e-6);
            Assert.IsNull(summary.GapSeconds(log.Turns[0].Calls[1]));
            Assert.AreEqual(1, summary.TopTools.Count);
            Assert.AreEqual("big", summary.TopTools[0].Id);
            Assert.AreEqual(0.75, summary.CostUsd, 1e-9);
        }

        [Test]
        public void XmlContainsSummaryTurnsCallsAndTools()
        {
            SessionRecord record = new SessionRecord { Title = "Fix bug", Kind = ProfileKind.ClaudeCode, Usage = SampleLog() };
            XDocument document = UsageXmlExporter.Build(record, "opus", Start);
            XElement root = document.Root;

            Assert.AreEqual("parleyUsage", root.Name.LocalName);
            Assert.AreEqual("Fix bug", (string)root.Attribute("chat"));
            Assert.AreEqual("3", (string)root.Element("summary").Attribute("calls"));
            Assert.AreEqual("0.7500", (string)root.Element("summary").Attribute("costUsd"));
            Assert.AreEqual("1", (string)root.Element("summary").Attribute("cacheRebuilds"));
            XElement[] turns = root.Element("turns").Elements("turn").ToArray();
            Assert.AreEqual(2, turns.Length);
            Assert.AreEqual("first", turns[0].Element("prompt").Value);
            Assert.AreEqual(2, turns[0].Elements("call").Count());
            Assert.AreEqual(2, turns[0].Elements("tool").Count());
            Assert.AreEqual("true", (string)turns[1].Element("call").Attribute("cacheRebuild"));
            Assert.AreEqual("3600.0", (string)turns[1].Element("call").Attribute("gapSec"));
        }

        [TestCase("")]
        [TestCase(null)]
        public void XmlModelFallsBackToLastMainCall(string model)
        {
            SessionRecord record = new SessionRecord { Usage = SampleLog() };
            XDocument document = UsageXmlExporter.Build(record, model, Start);

            Assert.AreEqual("opus", (string)document.Root.Attribute("model"));
        }

        [Test]
        public void EmptyLogExports()
        {
            XDocument document = UsageXmlExporter.Build(new SessionRecord(), null, Start);
            Assert.AreEqual("0", (string)document.Root.Element("summary").Attribute("calls"));
            Assert.AreEqual(0, document.Root.Element("turns").Elements().Count());
        }

        private static UsageLog SampleLog()
        {
            UsageLog log = new UsageLog();
            log.AgentLabels["agent1"] = "Explore: look";
            log.Turns.Add(new UsageTurn
            {
                Index = 1,
                StartedUtc = Start,
                Prompt = "first",
                CostUsd = 0.5,
                Calls =
                {
                    new UsageCall { Id = "m1", TimeUtc = Start, Model = "opus", Agent = UsageTracker.MainAgent, CacheWrite = 1000, CacheRead = 20000, Output = 10 },
                    new UsageCall { Id = "m2", TimeUtc = Start.AddSeconds(5), Model = "haiku", Agent = "agent1", CacheWrite = 2000, Output = 5 },
                },
                Tools =
                {
                    new UsageTool { Id = "big", Name = "Read", Agent = UsageTracker.MainAgent, CallId = "m1", ResultChars = 9000 },
                    new UsageTool { Id = "small", Name = "Grep", Agent = "agent1", CallId = "m2", ResultChars = 40 },
                },
            });

            log.Turns.Add(new UsageTurn
            {
                Index = 2,
                StartedUtc = Start.AddHours(1),
                Prompt = "continue",
                CostUsd = 0.25,
                Calls =
                {
                    new UsageCall { Id = "m3", TimeUtc = Start.AddHours(1), Model = "opus", Agent = UsageTracker.MainAgent, CacheWrite = 100000, CacheRead = 1000, Output = 50 },
                },
            });

            return log;
        }
    }
}
