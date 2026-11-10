using System.Collections.Generic;
using DTech.Parley.Editor;
using DTech.Parley.Editor.UI;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class AgentGraphTests
    {
        [Test]
        public void EmptyChatHasOnlyMainAgent()
        {
            AgentGraph graph = AgentGraph.Build(new List<TranscriptEntry>(), null, false);

            Assert.AreEqual(1, graph.Nodes.Count);
            Assert.AreEqual(0, graph.SubagentCount);
            Assert.IsTrue(graph.Main.IsMain);
            Assert.AreEqual(AgentNodeStatus.Done, graph.Main.Status);
        }

        [Test]
        public void SubagentsNestUnderTheirParents()
        {
            TranscriptEntry entry = Assistant(
                Agent("a", null, "Explore", "Map code", "Find classes"),
                Agent("b", null, "code-reviewer", "Review", "Check math"),
                Agent("c", "a", "Explore", "Trace hits", "Follow projectiles"));
            AgentGraph graph = AgentGraph.Build(new List<TranscriptEntry> { entry }, null, true);

            Assert.AreEqual(3, graph.SubagentCount);
            Assert.AreEqual(AgentNodeStatus.Running, graph.Main.Status);
            Assert.AreEqual(2, graph.Main.Children.Count);
            Assert.AreEqual("a", graph.Find("c").ParentId);
            Assert.AreEqual(2, graph.Find("c").Depth);
            Assert.AreEqual("Find classes", graph.Find("a").Prompt);
            Assert.AreEqual("Explore: Map code", graph.Find("a").Title);
        }

        [Test]
        public void ParentBlocksBecomeAgentActivity()
        {
            TranscriptBlock read = Tool("t1", "a", "Read");
            TranscriptBlock text = new TranscriptBlock { Kind = BlockKind.Text, ParentToolUseId = "a", Text = "found it" };
            TranscriptBlock mainRead = Tool("t2", null, "Grep");
            AgentGraph graph = AgentGraph.Build(new List<TranscriptEntry> { Assistant(Agent("a", null, "Explore", "Map", "p"), read, text, mainRead) },
                null, false);

            AgentNodeInfo agent = graph.Find("a");
            CollectionAssert.AreEqual(new[] { read, text }, agent.Activity);
            Assert.AreEqual(1, agent.ToolCount);
            Assert.AreEqual("Read", agent.LastTool);
            Assert.AreEqual(2, graph.Main.ToolCount);
        }

        [Test]
        public void StatusFollowsToolResult()
        {
            TranscriptBlock done = Agent("done", null, "Explore", "d", "p");
            done.Result = "ok";
            TranscriptBlock failed = Agent("failed", null, "Explore", "f", "p");
            failed.Result = "boom";
            failed.IsError = true;
            TranscriptBlock stopped = Agent("stopped", null, "Explore", "s", "p");
            stopped.IsFinished = true;
            TranscriptBlock running = Agent("running", null, "Explore", "r", "p");
            AgentGraph graph = AgentGraph.Build(new List<TranscriptEntry> { Assistant(done, failed, stopped, running) }, null, false);

            Assert.AreEqual(AgentNodeStatus.Done, graph.Find("done").Status);
            Assert.AreEqual(AgentNodeStatus.Failed, graph.Find("failed").Status);
            Assert.AreEqual(AgentNodeStatus.Stopped, graph.Find("stopped").Status);
            Assert.AreEqual(AgentNodeStatus.Running, graph.Find("running").Status);
        }

        [Test]
        public void BackgroundTaskStatusWinsOverImmediateResult()
        {
            TranscriptBlock agent = Agent("bg", null, "Explore", "Background", "p");
            agent.Result = "Async agent launched";
            BackgroundTaskInfo task = new BackgroundTaskInfo { TaskId = "t", ToolUseId = "bg", Status = "running" };
            AgentGraph graph = AgentGraph.Build(new List<TranscriptEntry> { Assistant(agent) }, new[] { task }, false);
            Assert.AreEqual(AgentNodeStatus.Running, graph.Find("bg").Status);

            task.Status = "completed";
            task.Summary = "All done";
            graph = AgentGraph.Build(new List<TranscriptEntry> { Assistant(agent) }, new[] { task }, false);
            Assert.AreEqual(AgentNodeStatus.Done, graph.Find("bg").Status);
            Assert.AreEqual("All done", graph.Find("bg").Result);
        }

        [Test]
        public void UnknownParentFallsBackToMain()
        {
            AgentGraph graph = AgentGraph.Build(new List<TranscriptEntry> { Assistant(Agent("a", "missing", "Explore", "x", "p")) }, null, false);

            Assert.AreEqual(AgentNodeInfo.MainId, graph.Find("a").ParentId);
            Assert.AreEqual(1, graph.Find("a").Depth);
        }

        [Test]
        public void SignatureChangesWithStatus()
        {
            TranscriptBlock agent = Agent("a", null, "Explore", "x", "p");
            List<TranscriptEntry> entries = new () { Assistant(agent) };
            string before = AgentGraph.Build(entries, null, false).Signature;
            Assert.AreEqual(before, AgentGraph.Build(entries, null, false).Signature);

            agent.Result = "done";
            Assert.AreNotEqual(before, AgentGraph.Build(entries, null, false).Signature);
        }

        private static TranscriptEntry Assistant(params TranscriptBlock[] blocks)
        {
            TranscriptEntry entry = new TranscriptEntry { Role = EntryRole.Assistant };
            entry.Blocks.AddRange(blocks);
            return entry;
        }

        private static TranscriptBlock Agent(string id, string parent, string type, string description, string prompt)
        {
            TranscriptBlock block = Tool(id, parent, "Agent");
            block.Input = new JObject { ["subagent_type"] = type, ["description"] = description, ["prompt"] = prompt };
            return block;
        }

        private static TranscriptBlock Tool(string id, string parent, string name)
        {
            return new TranscriptBlock { Kind = BlockKind.ToolUse, ToolUseId = id, ParentToolUseId = parent, ToolName = name };
        }
    }
}
