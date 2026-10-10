using System.Text;
using DTech.Parley.Editor.Agents.Local;
using NUnit.Framework;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class ThinkTagSplitterTests
    {
        private readonly StringBuilder _thinking = new ();
        private readonly StringBuilder _text = new ();

        [SetUp]
        public void SetUp()
        {
            _thinking.Clear();
            _text.Clear();
        }

        [Test]
        public void HandlesTagsAcrossChunks()
        {
            ThinkTagSplitter splitter = new ThinkTagSplitter();
            splitter.Feed("<thi", Emit);
            splitter.Feed("nk>plan it</th", Emit);
            splitter.Feed("ink>Answer <b", Emit);
            splitter.Flush(Emit);
            Assert.AreEqual("plan it", _thinking.ToString());
            Assert.AreEqual("Answer <b", _text.ToString());
        }

        private void Emit(bool isThinking, string piece)
        {
            (isThinking ? _thinking : _text).Append(piece);
        }
    }
}