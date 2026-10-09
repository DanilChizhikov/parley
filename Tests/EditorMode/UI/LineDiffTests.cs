using System.Collections.Generic;
using DTech.Parley.Editor.UI;
using NUnit.Framework;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class LineDiffTests
    {
        [Test]
        public void DiffFindsMinimalChanges()
        {
            string[] before = { "a", "b", "c", "d" };
            string[] after = { "a", "x", "c", "d", "e" };
            List<DiffLine> lines = LineDiff.Compute(before, after);
            int inserts = 0;
            int deletes = 0;
            foreach (DiffLine line in lines)
            {
                inserts += line.Operation == DiffOperation.Insert ? 1 : 0;
                deletes += line.Operation == DiffOperation.Delete ? 1 : 0;
            }

            Assert.AreEqual(2, inserts);
            Assert.AreEqual(1, deletes);
            List<DiffHunk> hunks = LineDiff.Hunks(lines, 1);
            Assert.AreEqual(1, hunks.Count);
            Assert.AreEqual(1, hunks[0].OldStart);
        }

        [Test]
        public void DiffOfIdenticalTextHasNoHunks()
        {
            List<DiffLine> lines = LineDiff.Compute(new[] { "a", "b" }, new[] { "a", "b" });
            Assert.AreEqual(0, LineDiff.Hunks(lines).Count);
        }

        [Test]
        public void HunkHeaderCountsBothSides()
        {
            List<DiffLine> lines = LineDiff.Compute(new[] { "a", "b", "c" }, new[] { "a", "c", "d" });
            List<DiffHunk> hunks = LineDiff.Hunks(lines);
            Assert.AreEqual(1, hunks.Count);
            Assert.AreEqual("@@ -1,3 +1,3 @@", hunks[0].Header);
        }
    }
}
