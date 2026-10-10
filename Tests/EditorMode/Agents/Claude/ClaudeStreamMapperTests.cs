using DTech.Parley.Editor;
using DTech.Parley.Editor.Agents.Claude;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class ClaudeStreamMapperTests
    {
        [Test]
        public void StreamEventsAndFinalMessageShareBlockKeys()
        {
            RecordingSink sink = new RecordingSink();
            ClaudeStreamMapper mapper = new ClaudeStreamMapper(sink);
            mapper.Handle(JObject.Parse("{\"type\":\"system\",\"subtype\":\"init\",\"session_id\":\"s1\",\"model\":\"opus\",\"permissionMode\":\"plan\",\"tools\":[\"Bash\"],\"slash_commands\":[\"compact\"]}"));
            mapper.Handle(JObject.Parse("{\"type\":\"stream_event\",\"session_id\":\"s1\",\"parent_tool_use_id\":null,\"event\":{\"type\":\"message_start\",\"message\":{\"id\":\"m1\"}}}"));
            mapper.Handle(JObject.Parse("{\"type\":\"stream_event\",\"event\":{\"type\":\"content_block_start\",\"index\":0,\"content_block\":{\"type\":\"text\",\"text\":\"\"}}}"));
            mapper.Handle(JObject.Parse("{\"type\":\"stream_event\",\"event\":{\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"text_delta\",\"text\":\"Hel\"}}}"));
            mapper.Handle(JObject.Parse("{\"type\":\"stream_event\",\"event\":{\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"text_delta\",\"text\":\"lo\"}}}"));
            mapper.Handle(JObject.Parse("{\"type\":\"assistant\",\"message\":{\"id\":\"m1\",\"content\":[{\"type\":\"text\",\"text\":\"Hello\"}]}}"));

            string key = ClaudeStreamMapper.Key(string.Empty, "m1", 0);
            Assert.AreEqual("s1", sink.Session.SessionId);
            Assert.AreEqual(PermissionMode.Plan, sink.Session.Mode);
            CollectionAssert.Contains(sink.Session.SlashCommands, "compact");
            CollectionAssert.Contains(sink.Events, "start:Text:" + key);
            CollectionAssert.Contains(sink.Events, "final:Text:" + key);
            Assert.AreEqual("Hello", sink.Texts[key]);
        }

        [Test]
        public void ToolUseAndResultAreMapped()
        {
            RecordingSink sink = new RecordingSink();
            ClaudeStreamMapper mapper = new ClaudeStreamMapper(sink);
            mapper.Handle(JObject.Parse("{\"type\":\"assistant\",\"message\":{\"id\":\"m2\",\"content\":[{\"type\":\"tool_use\",\"id\":\"t1\",\"name\":\"Bash\",\"input\":{\"command\":\"ls\"}}]}}"));
            mapper.Handle(JObject.Parse("{\"type\":\"user\",\"message\":{\"content\":[{\"type\":\"tool_result\",\"tool_use_id\":\"t1\",\"content\":[{\"type\":\"text\",\"text\":\"a.cs\"}],\"is_error\":false}]}}"));
            mapper.Handle(JObject.Parse("{\"type\":\"result\",\"subtype\":\"success\",\"is_error\":false,\"duration_ms\":10,\"num_turns\":1,\"total_cost_usd\":0.01,\"usage\":{\"input_tokens\":5,\"output_tokens\":7},\"result\":\"done\"}"));

            Assert.AreEqual(1, sink.ToolUses.Count);
            Assert.AreEqual("Bash", sink.ToolUses[0].name);
            Assert.AreEqual("ls", (string)sink.ToolUses[0].input["command"]);
            Assert.AreEqual(("t1", "a.cs", false), sink.ToolResults[0]);
            Assert.AreEqual(1, sink.Turns.Count);
            Assert.AreEqual(7, sink.Turns[0].OutputTokens);
            Assert.AreEqual(0.01, sink.Turns[0].CostUsd.Value, 1e-9);
        }

        [Test]
        public void SessionIdIsConfirmedOnlyByResult()
        {
            RecordingSink sink = new RecordingSink();
            ClaudeStreamMapper mapper = new ClaudeStreamMapper(sink);
            mapper.Handle(JObject.Parse("{\"type\":\"system\",\"subtype\":\"hook_started\",\"session_id\":\"early\"}"));
            Assert.AreEqual("early", mapper.SessionId);
            Assert.IsNull(mapper.ConfirmedSessionId);

            mapper.Handle(JObject.Parse("{\"type\":\"result\",\"subtype\":\"error_during_execution\",\"is_error\":true,\"num_turns\":0,\"session_id\":\"early\"}"));
            Assert.IsNull(mapper.ConfirmedSessionId);

            mapper.Handle(JObject.Parse("{\"type\":\"result\",\"subtype\":\"success\",\"is_error\":false,\"num_turns\":1,\"session_id\":\"real\"}"));
            Assert.AreEqual("real", mapper.ConfirmedSessionId);
        }

        [Test]
        public void ResultTextIsShownWhenTurnHadNoAssistantText()
        {
            RecordingSink sink = new RecordingSink();
            ClaudeStreamMapper mapper = new ClaudeStreamMapper(sink);
            mapper.Handle(JObject.Parse("{\"type\":\"result\",\"subtype\":\"success\",\"is_error\":false,\"result\":\"<local-command-stdout>Total cost: $0</local-command-stdout>\"}"));
            Assert.AreEqual("Info:Total cost: $0", sink.Notices[0]);
        }
    }
}