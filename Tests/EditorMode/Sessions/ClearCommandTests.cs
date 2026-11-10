using DTech.Parley.Editor.Sessions;
using NUnit.Framework;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class ClearCommandTests
    {
        [TestCase("/clear")]
        [TestCase("  /clear\n")]
        [TestCase("/CLEAR")]
        public void RecognizesClear(string text)
        {
            Assert.IsTrue(ChatSession.IsClearCommand(text));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("/clear the cache")]
        [TestCase("please /clear")]
        [TestCase("/clearer")]
        public void IgnoresOtherText(string text)
        {
            Assert.IsFalse(ChatSession.IsClearCommand(text));
        }
    }
}
