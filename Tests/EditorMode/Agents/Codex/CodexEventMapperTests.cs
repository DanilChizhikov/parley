using DTech.Parley.Editor;
using DTech.Parley.Editor.Agents.Codex;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class CodexEventMapperTests
    {
        [Test]
        public void AgentMessageStreamsIntoOneBlock()
        {
            RecordingSink sink = new RecordingSink();
            CodexEventMapper mapper = new CodexEventMapper(sink);
            mapper.Handle("item/started", Item("{\"type\":\"agentMessage\",\"id\":\"msg_1\",\"text\":\"\"}"));
            mapper.Handle("item/agentMessage/delta", JObject.Parse("{\"itemId\":\"msg_1\",\"delta\":\"do\"}"));
            mapper.Handle("item/agentMessage/delta", JObject.Parse("{\"itemId\":\"msg_1\",\"delta\":\"ne\"}"));
            Assert.AreEqual("done", sink.Texts["msg_1"]);

            mapper.Handle("item/completed", Item("{\"type\":\"agentMessage\",\"id\":\"msg_1\",\"text\":\"done\"}"));
            CollectionAssert.Contains(sink.Events, "start:Text:msg_1");
            CollectionAssert.Contains(sink.Events, "final:Text:msg_1");
            Assert.AreEqual("done", sink.Texts["msg_1"]);
        }

        [Test]
        public void ReasoningSummaryPartsAreSeparated()
        {
            RecordingSink sink = new RecordingSink();
            CodexEventMapper mapper = new CodexEventMapper(sink);
            mapper.Handle("item/started", Item("{\"type\":\"reasoning\",\"id\":\"r1\",\"summary\":[],\"content\":[]}"));
            mapper.Handle("item/reasoning/summaryTextDelta", JObject.Parse("{\"itemId\":\"r1\",\"summaryIndex\":0,\"delta\":\"a\"}"));
            mapper.Handle("item/reasoning/summaryTextDelta", JObject.Parse("{\"itemId\":\"r1\",\"summaryIndex\":1,\"delta\":\"b\"}"));
            Assert.AreEqual("a\n\nb", sink.Texts["r1"]);

            mapper.Handle("item/completed", Item("{\"type\":\"reasoning\",\"id\":\"r1\",\"summary\":[\"a\",\"b\"],\"content\":[]}"));
            CollectionAssert.Contains(sink.Events, "final:Thinking:r1");
            Assert.AreEqual("a\n\nb", sink.Texts["r1"]);
        }

        [Test]
        public void CommandExecutionBecomesBashWithExitCode()
        {
            RecordingSink sink = new RecordingSink();
            CodexEventMapper mapper = new CodexEventMapper(sink);
            mapper.Handle("item/started", Item("{\"type\":\"commandExecution\",\"id\":\"c1\",\"command\":\"ls\",\"cwd\":\"/p\",\"status\":\"inProgress\",\"commandActions\":[]}"));
            Assert.AreEqual("ls", (string)mapper.FindItem("c1")["command"]);

            mapper.Handle("item/completed", Item("{\"type\":\"commandExecution\",\"id\":\"c1\",\"command\":\"ls\",\"cwd\":\"/p\",\"status\":\"failed\",\"aggregatedOutput\":\"a.cs\\n\",\"exitCode\":1,\"commandActions\":[]}"));
            CollectionAssert.Contains(sink.Events, "start:ToolUse:c1");
            Assert.AreEqual(("c1", "Bash"), (sink.ToolUses[0].id, sink.ToolUses[0].name));
            Assert.AreEqual("ls", (string)sink.ToolUses[0].input["command"]);
            Assert.AreEqual(("c1", "a.cs\n[exit code 1]", true), sink.ToolResults[0]);
        }

        [Test]
        public void DeclinedCommandExplainsWhy()
        {
            RecordingSink sink = new RecordingSink();
            CodexEventMapper mapper = new CodexEventMapper(sink);
            mapper.Handle("item/completed", Item("{\"type\":\"commandExecution\",\"id\":\"c2\",\"command\":\"rm x\",\"cwd\":\"/p\",\"status\":\"declined\",\"aggregatedOutput\":null,\"exitCode\":null,\"commandActions\":[]}"));
            Assert.AreEqual(("c2", "The user declined this command.", true), sink.ToolResults[0]);
        }

        [Test]
        public void DynamicToolCallUsesUnityPrefix()
        {
            RecordingSink sink = new RecordingSink();
            CodexEventMapper mapper = new CodexEventMapper(sink);
            mapper.Handle("item/started", Item("{\"type\":\"dynamicToolCall\",\"id\":\"exec-1\",\"namespace\":\"unity\",\"tool\":\"console\",\"arguments\":{\"limit\":5},\"status\":\"inProgress\",\"success\":null}"));
            mapper.Handle("item/completed", Item("{\"type\":\"dynamicToolCall\",\"id\":\"exec-1\",\"namespace\":\"unity\",\"tool\":\"console\",\"arguments\":{\"limit\":5},\"status\":\"completed\",\"contentItems\":[{\"type\":\"inputText\",\"text\":\"Console is empty.\"}],\"success\":true}"));
            Assert.AreEqual("unity_console", sink.ToolUses[0].name);
            Assert.AreEqual(5, (int)sink.ToolUses[0].input["limit"]);
            Assert.AreEqual(("exec-1", "Console is empty.", false), sink.ToolResults[0]);
        }

        [Test]
        public void McpToolCallUsesMcpName()
        {
            RecordingSink sink = new RecordingSink();
            CodexEventMapper mapper = new CodexEventMapper(sink);
            mapper.Handle("item/completed", Item("{\"type\":\"mcpToolCall\",\"id\":\"m1\",\"server\":\"fs\",\"tool\":\"read\",\"arguments\":{\"path\":\"a\"},\"status\":\"completed\",\"result\":{\"content\":[{\"type\":\"text\",\"text\":\"hi\"}],\"structuredContent\":{\"a\":1}}}"));
            Assert.AreEqual("mcp__fs__read", sink.ToolUses[0].name);
            Assert.AreEqual(("m1", "hi", false), sink.ToolResults[0]);
        }

        [Test]
        public void PlanUpdateBecomesTodoWrite()
        {
            RecordingSink sink = new RecordingSink();
            CodexEventMapper mapper = new CodexEventMapper(sink);
            mapper.Handle("turn/plan/updated", JObject.Parse("{\"plan\":[{\"step\":\"Read\",\"status\":\"completed\"},{\"step\":\"Fix\",\"status\":\"inProgress\"},{\"step\":\"Test\",\"status\":\"pending\"}]}"));
            Assert.AreEqual("TodoWrite", sink.ToolUses[0].name);
            JArray todos = (JArray)sink.ToolUses[0].input["todos"];
            Assert.AreEqual(3, todos.Count);
            Assert.AreEqual("in_progress", (string)todos[1]["status"]);
            Assert.AreEqual("Fix", (string)todos[1]["activeForm"]);
            Assert.IsFalse(sink.ToolResults[0].error);
        }

        [Test]
        public void PlanItemIsRememberedUntilNextTurn()
        {
            RecordingSink sink = new RecordingSink();
            CodexEventMapper mapper = new CodexEventMapper(sink);
            mapper.Handle("item/completed", Item("{\"type\":\"plan\",\"id\":\"p1\",\"text\":\"# Plan\"}"));
            Assert.AreEqual(PendingRequest.ExitPlanModeTool, sink.ToolUses[0].name);
            Assert.AreEqual("# Plan", (string)sink.ToolUses[0].input["plan"]);
            Assert.AreEqual("# Plan", mapper.PlanText);
            Assert.AreEqual("p1", mapper.PlanItemId);

            mapper.Handle("turn/started", JObject.Parse("{\"turn\":{\"id\":\"t2\"}}"));
            Assert.IsNull(mapper.PlanText);
            Assert.IsNull(mapper.PlanItemId);
        }

        [Test]
        public void TurnTokensAreDeltaOfTotals()
        {
            RecordingSink sink = new RecordingSink();
            CodexEventMapper mapper = new CodexEventMapper(sink);
            mapper.Handle("thread/tokenUsage/updated", JObject.Parse("{\"tokenUsage\":{\"total\":{\"inputTokens\":100,\"outputTokens\":10,\"cachedInputTokens\":40,\"totalTokens\":110},\"last\":{\"totalTokens\":110},\"modelContextWindow\":1000}}"));
            mapper.Handle("turn/started", JObject.Parse("{\"turn\":{\"id\":\"t1\"}}"));
            mapper.Handle("thread/tokenUsage/updated", JObject.Parse("{\"tokenUsage\":{\"total\":{\"inputTokens\":300,\"outputTokens\":25,\"cachedInputTokens\":140,\"totalTokens\":325},\"last\":{\"totalTokens\":215},\"modelContextWindow\":1000}}"));
            mapper.Handle("turn/completed", JObject.Parse("{\"turn\":{\"id\":\"t1\",\"status\":\"completed\",\"durationMs\":42}}"));

            TurnResult result = sink.Turns[0];
            Assert.IsFalse(result.IsError);
            Assert.AreEqual("success", result.Subtype);
            Assert.AreEqual(200, result.InputTokens);
            Assert.AreEqual(15, result.OutputTokens);
            Assert.AreEqual(100, result.CacheReadTokens);
            Assert.AreEqual(42, result.DurationMs);
            Assert.AreEqual((215L, 1000L), sink.ContextUsages[sink.ContextUsages.Count - 1]);
        }

        [Test]
        public void FailedTurnShowsNestedErrorMessage()
        {
            RecordingSink sink = new RecordingSink();
            CodexEventMapper mapper = new CodexEventMapper(sink);
            string message = "{\\\"type\\\":\\\"error\\\",\\\"status\\\":400,\\\"error\\\":{\\\"type\\\":\\\"invalid_request_error\\\",\\\"message\\\":\\\"Model not supported.\\\"}}";
            mapper.Handle("turn/completed", JObject.Parse("{\"turn\":{\"id\":\"t1\",\"status\":\"failed\",\"error\":{\"message\":\"" + message + "\",\"codexErrorInfo\":\"other\"}}}"));

            Assert.IsTrue(sink.Turns[0].IsError);
            Assert.AreEqual("error", sink.Turns[0].Subtype);
            Assert.AreEqual("Model not supported.", sink.Turns[0].Errors[0]);
            CollectionAssert.Contains(sink.Notices, "Error:Turn ended with an error: Model not supported.");
        }

        [Test]
        public void InterruptedTurnIsReported()
        {
            RecordingSink sink = new RecordingSink();
            CodexEventMapper mapper = new CodexEventMapper(sink);
            mapper.Handle("turn/completed", JObject.Parse("{\"turn\":{\"id\":\"t1\",\"status\":\"interrupted\"}}"));
            Assert.AreEqual("interrupted", sink.Turns[0].Subtype);
            CollectionAssert.Contains(sink.Notices, "Info:Interrupted.");
        }

        [Test]
        public void TurnWithNullErrorCompletes()
        {
            RecordingSink sink = new RecordingSink();
            CodexEventMapper mapper = new CodexEventMapper(sink);
            mapper.Handle("turn/completed", JObject.Parse("{\"threadId\":\"th1\",\"turn\":{\"id\":\"t1\",\"items\":[],\"status\":\"completed\",\"error\":null}}"));
            mapper.Handle("turn/completed", JObject.Parse("{\"threadId\":\"th1\",\"turn\":{\"id\":\"t2\",\"items\":[],\"status\":\"interrupted\",\"error\":null}}"));

            Assert.AreEqual(2, sink.Turns.Count);
            Assert.AreEqual("success", sink.Turns[0].Subtype);
            Assert.IsEmpty(sink.Turns[0].Errors);
            Assert.AreEqual("interrupted", sink.Turns[1].Subtype);
        }

        [Test]
        public void McpCallWithNullErrorAndResultCompletes()
        {
            RecordingSink sink = new RecordingSink();
            CodexEventMapper mapper = new CodexEventMapper(sink);
            mapper.Handle("item/completed", Item("{\"type\":\"mcpToolCall\",\"id\":\"m1\",\"server\":\"unity\",\"tool\":\"ping\",\"arguments\":{},\"status\":\"completed\",\"error\":null,\"result\":null}"));
            CollectionAssert.Contains(sink.Events, "final:ToolUse:m1");
        }

        [Test]
        public void ErrorsRequestSignInOrWarnAboutRetries()
        {
            RecordingSink sink = new RecordingSink();
            CodexEventMapper mapper = new CodexEventMapper(sink);
            mapper.Handle("error", JObject.Parse("{\"error\":{\"message\":\"401 Unauthorized\",\"codexErrorInfo\":\"unauthorized\"},\"willRetry\":false}"));
            Assert.IsTrue(sink.Events.Exists(item => item.StartsWith("auth:")));

            mapper.Handle("error", JObject.Parse("{\"error\":{\"message\":\"stream lost\",\"codexErrorInfo\":{\"responseStreamDisconnected\":{\"httpStatusCode\":null}}},\"willRetry\":true}"));
            CollectionAssert.Contains(sink.Notices, "Warning:Retrying: stream lost");
        }

        [Test]
        public void ItemsAreForgottenAfterTurn()
        {
            RecordingSink sink = new RecordingSink();
            CodexEventMapper mapper = new CodexEventMapper(sink);
            mapper.Handle("item/started", Item("{\"type\":\"fileChange\",\"id\":\"f1\",\"changes\":[],\"status\":\"inProgress\"}"));
            Assert.IsNotNull(mapper.FindItem("f1"));
            mapper.Handle("turn/completed", JObject.Parse("{\"turn\":{\"id\":\"t1\",\"status\":\"completed\"}}"));
            Assert.IsNull(mapper.FindItem("f1"));
        }

        private static JObject Item(string json)
        {
            return new JObject { ["item"] = JObject.Parse(json) };
        }
    }
}