using DTech.Parley.Editor;
using DTech.Parley.Editor.Agents.Local;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class RoundStreamTests
    {
        [Test]
        public void SplitsThinkingTextAndToolCalls()
        {
            RecordingSink sink = new RecordingSink();
            RoundStream round = new RoundStream(sink, 1);
            round.HandleChunk(JObject.Parse("{\"choices\":[{\"delta\":{\"reasoning_content\":\"think\"}}]}"));
            round.HandleChunk(JObject.Parse("{\"choices\":[{\"delta\":{\"content\":\"Hello\"}}]}"));
            round.HandleChunk(JObject.Parse("{\"choices\":[{\"delta\":{\"tool_calls\":[{\"index\":0,\"id\":\"c1\",\"function\":{\"name\":\"Read\",\"arguments\":\"{}\"}}]}}]}"));
            round.HandleChunk(JObject.Parse("{\"choices\":[],\"usage\":{\"prompt_tokens\":12,\"completion_tokens\":3}}"));
            Assert.AreEqual("Hello", round.PartialText);
            round.Complete(false);

            Assert.AreEqual("Hello", round.Text);
            Assert.AreEqual(12, round.PromptTokens);
            Assert.AreEqual(3, round.CompletionTokens);
            Assert.AreEqual(1, round.Calls.Count);
            Assert.AreEqual("think", sink.Texts["local:1:thinking"]);
            Assert.AreEqual("Hello", sink.Texts["local:1:text"]);
            CollectionAssert.Contains(sink.Events, "start:" + BlockKind.Thinking + ":local:1:thinking");
            CollectionAssert.Contains(sink.Events, "final:" + BlockKind.ToolUse + ":local:tool:c1");
            Assert.AreEqual(("c1", "Read"), (sink.ToolUses[0].id, sink.ToolUses[0].name));
        }

        [Test]
        public void TextToolCallsAreParsedOnComplete()
        {
            RecordingSink sink = new RecordingSink();
            RoundStream round = new RoundStream(sink, 2);
            round.HandleChunk(JObject.Parse("{\"choices\":[{\"delta\":{\"content\":\"<think>plan</think>Look <tool_call>{\\\"name\\\":\\\"Read\\\",\\\"arguments\\\":{\\\"file_path\\\":\\\"a.cs\\\"}}</tool_call>\"}}]}"));
            round.Complete(true);

            Assert.AreEqual("Look", round.Text);
            Assert.AreEqual(1, round.Calls.Count);
            Assert.AreEqual("Read", round.Calls[0].Name);
            StringAssert.StartsWith("call_", round.Calls[0].Id);
            Assert.AreEqual("plan", sink.Texts["local:2:thinking"]);
            Assert.AreEqual("a.cs", (string)sink.ToolUses[0].input["file_path"]);
        }
    }
}