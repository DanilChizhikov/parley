using DTech.Parley.Editor.Agents.Local;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class ToolCallAccumulatorTests
    {
        [Test]
        public void MergesFragments()
        {
            ToolCallAccumulator accumulator = new ToolCallAccumulator();
            accumulator.Feed(JArray.Parse("[{\"index\":0,\"id\":\"call_1\",\"function\":{\"name\":\"Read\",\"arguments\":\"{\\\"file_\"}}]"));
            accumulator.Feed(JArray.Parse("[{\"index\":0,\"function\":{\"arguments\":\"path\\\":\\\"a.cs\\\"}\"}}]"));
            accumulator.Feed(JArray.Parse("[{\"index\":1,\"id\":\"call_2\",\"function\":{\"name\":\"Glob\",\"arguments\":{\"pattern\":\"*.cs\"}}}]"));
            Assert.AreEqual(2, accumulator.Calls.Count);
            Assert.AreEqual("Read", accumulator.Calls[0].Name);
            Assert.AreEqual("{\"file_path\":\"a.cs\"}", accumulator.Calls[0].Arguments.ToString());
            Assert.AreEqual("{\"pattern\":\"*.cs\"}", accumulator.Calls[1].Arguments.ToString());
        }

        [Test]
        public void EnsureIdsFillsMissingIds()
        {
            ToolCallAccumulator accumulator = new ToolCallAccumulator();
            accumulator.Feed(JArray.Parse("[{\"index\":0,\"function\":{\"name\":\"Read\",\"arguments\":\"{}\"}}]"));
            Assert.IsNull(accumulator.Calls[0].Id);
            accumulator.EnsureIds();
            StringAssert.StartsWith("call_", accumulator.Calls[0].Id);
        }
    }
}