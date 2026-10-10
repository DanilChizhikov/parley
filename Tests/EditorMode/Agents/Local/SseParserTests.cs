using System.Collections.Generic;
using DTech.Parley.Editor.Agents.Local;
using NUnit.Framework;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class SseParserTests
    {
        [Test]
        public void HandlesSplitChunksAndDone()
        {
            SseParser parser = new SseParser();
            List<string> data = new ();
            parser.OnData += data.Add;
            parser.Feed("data: {\"a\":1}\n\nda");
            parser.Feed("ta: {\"b\":2}\r\n\r\n: comment\n");
            parser.Feed("data: [DONE]\n\ndata: {\"c\":3}\n");
            CollectionAssert.AreEqual(new[] { "{\"a\":1}", "{\"b\":2}" }, data);
            Assert.IsTrue(parser.IsDone);
        }

        [Test]
        public void FlushProcessesTrailingLine()
        {
            SseParser parser = new SseParser();
            List<string> data = new ();
            parser.OnData += data.Add;
            parser.Feed("data: {\"tail\":true}");
            Assert.AreEqual(0, data.Count);
            parser.Flush();
            CollectionAssert.AreEqual(new[] { "{\"tail\":true}" }, data);
        }
    }
}