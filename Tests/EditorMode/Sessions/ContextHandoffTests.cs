using System;
using System.Threading;
using System.Threading.Tasks;
using DTech.Parley.Editor;
using DTech.Parley.Editor.Sessions;
using DTech.Parley.Editor.Agents.Claude;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class ContextHandoffTests
    {
        [Test]
        public void LastMessagesExcludeTransportStateAndRemainFrozen()
        {
            SessionRecord source = new();
            source.Entries.Add(Entry(EntryRole.User, "old"));
            source.Entries.Add(Entry(EntryRole.User, "goal"));
            TranscriptEntry reply = Entry(EntryRole.Assistant, "decision");
            reply.Blocks.Add(new TranscriptBlock { Kind = BlockKind.Thinking, Text = "reasoning", IsFinished = true });
            reply.Blocks.Add(new TranscriptBlock { Kind = BlockKind.ToolUse, Text = "protocol", IsFinished = true });
            reply.Blocks.Add(new TranscriptBlock { Kind = BlockKind.Request, Text = "approval", IsFinished = true });
            reply.Blocks.Add(new TranscriptBlock { Kind = BlockKind.Text, Text = "unfinished" });
            reply.Blocks.Add(new TranscriptBlock
                { Kind = BlockKind.Text, Text = "subagent", IsFinished = true, ParentToolUseId = "tool" });
            source.Entries.Add(reply);
            ContextSnapshot snapshot = ContextHandoffService.Capture(source, 2);
            reply.Blocks[0].Text = "changed";
            source.Entries.Clear();
            StringAssert.Contains("goal", snapshot.Markdown);
            StringAssert.Contains("decision", snapshot.Markdown);
            foreach (string omitted in new[] { "old", "reasoning", "protocol", "approval\n", "unfinished", "subagent", "changed" })
                StringAssert.DoesNotContain(omitted, snapshot.Markdown);
            Assert.AreEqual(source.Id, snapshot.SourceSessionId);
        }

        [Test]
        public void SnapshotRoundTripsAndCarriesInheritedContext()
        {
            SessionRecord source = new();
            source.Entries.Add(Entry(EntryRole.User, "initial goal"));
            ContextSnapshot first = ContextHandoffService.Capture(source, 1);
            SessionRecord destination = new() { ContextSnapshot = first };
            destination.Entries.Add(Entry(EntryRole.User, "follow-up"));
            SessionRecord restored = JsonConvert.DeserializeObject<SessionRecord>(JsonConvert.SerializeObject(destination));
            ContextSnapshot next = ContextHandoffService.Capture(restored, 1);
            Assert.AreEqual(first.Markdown, restored.ContextSnapshot.Markdown);
            Assert.AreEqual(first.CapturedUtc, restored.ContextSnapshot.CapturedUtc);
            StringAssert.Contains("initial goal", next.Markdown);
            StringAssert.Contains("follow-up", next.Markdown);
            Assert.AreEqual((first.Characters + 3) / 4, first.EstimatedTokens);
        }

        [Test]
        public void CompletedEditsTransferPathsWithoutToolPayloadOrFailedChanges()
        {
            SessionRecord source = new();
            TranscriptEntry entry = new() { Role = EntryRole.Assistant };
            entry.Blocks.Add(new TranscriptBlock
            {
                Kind = BlockKind.ToolUse, ToolName = "Edit", IsFinished = true, Result = "edited",
                Input = new JObject { ["file_path"] = "Assets/Changed.cs", ["new_string"] = "private payload" },
            });
            entry.Blocks.Add(new TranscriptBlock
            {
                Kind = BlockKind.ToolUse, ToolName = "ApplyPatch", IsFinished = true, Result = "applied",
                Input = new JObject { ["changes"] = new JArray(new JObject { ["path"] = "Assets/Codex.cs" }) },
            });
            entry.Blocks.Add(new TranscriptBlock
            {
                Kind = BlockKind.ToolUse, ToolName = "Write", IsFinished = true, IsError = true, Result = "failed",
                Input = new JObject { ["file_path"] = "Assets/Failed.cs" },
            });
            source.Entries.Add(entry);
            string markdown = ContextHandoffService.Capture(source, 1).Markdown;
            StringAssert.Contains("Assets/Changed.cs", markdown);
            StringAssert.Contains("Assets/Codex.cs", markdown);
            StringAssert.DoesNotContain("private payload", markdown);
            StringAssert.DoesNotContain("Assets/Failed.cs", markdown);
        }

        [Test]
        public async Task SummaryUsesCapturedInputWithoutChangingSource()
        {
            SessionRecord source = new();
            source.Entries.Add(Entry(EntryRole.User, "goal"));
            string before = JsonConvert.SerializeObject(source);
            ContextSnapshot captured = ContextHandoffService.Capture(source, 10);
            SummaryGenerator generator = new();
            ContextHandoffService service = new(generator);
            Task<ContextSnapshot> pending = service.SummarizeAsync(captured, new ParleyProfile(), CancellationToken.None);
            StringAssert.Contains("goal", generator.Prompt);
            StringAssert.Contains("Modified files", generator.Prompt);
            generator.Result.SetResult("## Goal\nshort summary");
            ContextSnapshot summary = await pending;
            Assert.AreEqual(captured.CapturedUtc, summary.CapturedUtc);
            Assert.AreEqual(before, JsonConvert.SerializeObject(source));
            StringAssert.Contains("short summary", summary.Markdown);
        }

        [Test]
        public void InvalidCountAndCanceledSummaryDoNotGenerate()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ContextHandoffService.Capture(new SessionRecord(), 0));
            SummaryGenerator generator = new();
            ContextHandoffService service = new(generator);
            using CancellationTokenSource cancellation = new();
            cancellation.Cancel();
            Assert.CatchAsync<OperationCanceledException>(() => service.SummarizeAsync(
                ContextHandoffService.Capture(new SessionRecord(), 1), new ParleyProfile(), cancellation.Token));
            Assert.IsNull(generator.Prompt);
        }

        [Test]
        public void LocalSummaryRequestHasNoToolsOrSourceProtocol()
        {
            var request = ContextSummaryGenerator.BuildLocalRequest(new ParleyProfile { Model = "summary-model" }, "snapshot");
            Assert.IsNull(request["tools"]);
            Assert.IsNull(request["tool_choice"]);
            Assert.AreEqual("summary-model", (string)request["model"]);
            Assert.AreEqual("snapshot", (string)request["messages"][0]["content"]);
        }

        [Test]
        public void ClaudeSummaryDisablesToolsMcpHooksAndSessionPersistence()
        {
            string[] args = ClaudeCodeBackend.SummaryArguments(null);
            Assert.AreEqual(string.Empty, args[Array.IndexOf(args, "--tools") + 1]);
            CollectionAssert.Contains(args, "--strict-mcp-config");
            CollectionAssert.Contains(args, "{\"mcpServers\":{}}");
            CollectionAssert.Contains(args, "--no-session-persistence");
            CollectionAssert.Contains(args, "--disable-slash-commands");
            StringAssert.Contains("\"disableAllHooks\":true", args[Array.IndexOf(args, "--settings") + 1]);
        }

        private static TranscriptEntry Entry(EntryRole role, string text)
        {
            return new TranscriptEntry
            {
                Role = role,
                Blocks = { new TranscriptBlock { Kind = BlockKind.Text, Text = text, IsFinished = true } },
            };
        }

        private sealed class SummaryGenerator : IContextSummaryGenerator
        {
            public string Prompt;
            public readonly TaskCompletionSource<string> Result = new();

            public Task<string> GenerateAsync(ParleyProfile profile, string prompt, CancellationToken cancellationToken)
            {
                Prompt = prompt;
                return Result.Task;
            }
        }
    }
}
