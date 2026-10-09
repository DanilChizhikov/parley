using System.Collections.Generic;
using DTech.Parley.Editor.Agents.Local;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class TextToolCallParserTests
    {
        [Test]
        public void ExtractsCalls()
        {
            List<(string name, string arguments)> calls = new ();
            string remaining = TextToolCallParser.Extract("Let me look.\n<tool_call>{\"name\":\"Read\",\"arguments\":{\"file_path\":\"a.cs\"}}</tool_call>", calls);
            Assert.AreEqual("Let me look.", remaining);
            Assert.AreEqual(1, calls.Count);
            Assert.AreEqual("Read", calls[0].name);
            Assert.AreEqual("a.cs", (string)JObject.Parse(calls[0].arguments)["file_path"]);
        }

        [Test]
        public void KeepsInvalidBlocksAsText()
        {
            List<(string name, string arguments)> calls = new ();
            string remaining = TextToolCallParser.Extract("<tool_call>not json</tool_call>", calls);
            Assert.AreEqual(0, calls.Count);
            Assert.AreEqual("<tool_call>not json</tool_call>", remaining);
        }
    }
}