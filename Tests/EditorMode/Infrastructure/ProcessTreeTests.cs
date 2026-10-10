using DTech.Parley.Editor;
using NUnit.Framework;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class ProcessTreeTests
    {
        [Test]
        public void FindsDescendantsFromPsOutput()
        {
            const string ps = "    1     0\n  100     1\n  200   100\n  201   100\r\n  300   200\n  400     1\nnot a process line\n";
            CollectionAssert.AreEquivalent(new[] { 200, 201, 300 }, ProcessTree.Descendants(ps, 100));
            CollectionAssert.IsEmpty(ProcessTree.Descendants(ps, 400));
            CollectionAssert.IsEmpty(ProcessTree.Descendants(null, 100));
        }
    }
}
