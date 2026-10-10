using DTech.Parley.Editor.Agents.Local;
using NUnit.Framework;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class AccumulatedToolCallTests
    {
        [Test]
        public void ParseArgumentsHandlesEmptyValidAndBrokenJson()
        {
            AccumulatedToolCall empty = new AccumulatedToolCall();
            Assert.AreEqual(0, empty.ParseArguments().Count);

            AccumulatedToolCall valid = new AccumulatedToolCall();
            valid.Arguments.Append("{\"file_path\":\"a.cs\"}");
            Assert.AreEqual("a.cs", (string)valid.ParseArguments()["file_path"]);

            AccumulatedToolCall broken = new AccumulatedToolCall();
            broken.Arguments.Append("{\"file_path\":");
            Assert.IsNull(broken.ParseArguments());
        }
    }
}