using DTech.Parley.Editor.UI;
using NUnit.Framework;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class ComposerUndoStackTests
    {
        [Test]
        public void UndoReturnsRecordedStateAndRedoReturnsCurrent()
        {
            ComposerUndoStack stack = new ComposerUndoStack();
            stack.Record(new ComposerSnapshot("a", 1, 1), 0, false);

            Assert.IsTrue(stack.TryUndo(new ComposerSnapshot("ab", 2, 2), out ComposerSnapshot previous));
            Assert.AreEqual("a", previous.Text);
            Assert.AreEqual(1, previous.Cursor);
            Assert.IsTrue(stack.TryRedo(previous, out ComposerSnapshot next));
            Assert.AreEqual("ab", next.Text);
            Assert.IsFalse(stack.CanRedo);
        }

        [Test]
        public void QuickTypingMergesIntoOneStep()
        {
            ComposerUndoStack stack = new ComposerUndoStack();
            stack.Record(new ComposerSnapshot(string.Empty, 0, 0), 0, true);
            stack.Record(new ComposerSnapshot("a", 1, 1), 0.2, true);
            stack.Record(new ComposerSnapshot("ab", 2, 2), 0.4, true);

            Assert.IsTrue(stack.TryUndo(new ComposerSnapshot("abc", 3, 3), out ComposerSnapshot previous));
            Assert.AreEqual(string.Empty, previous.Text);
            Assert.IsFalse(stack.CanUndo);
        }

        [Test]
        public void PauseOrDirectEditStartsNewStep()
        {
            ComposerUndoStack stack = new ComposerUndoStack();
            stack.Record(new ComposerSnapshot(string.Empty, 0, 0), 0, true);
            stack.Record(new ComposerSnapshot("a", 1, 1), ComposerUndoStack.MergeSeconds + 1, true);
            stack.Record(new ComposerSnapshot("ab", 2, 2), ComposerUndoStack.MergeSeconds + 1.1, false);

            Assert.IsTrue(stack.TryUndo(new ComposerSnapshot("ab\n", 3, 3), out ComposerSnapshot first));
            Assert.AreEqual("ab", first.Text);
            Assert.IsTrue(stack.TryUndo(first, out ComposerSnapshot second));
            Assert.AreEqual("a", second.Text);
            Assert.IsTrue(stack.TryUndo(second, out ComposerSnapshot third));
            Assert.AreEqual(string.Empty, third.Text);
        }

        [Test]
        public void NewEditClearsRedo()
        {
            ComposerUndoStack stack = new ComposerUndoStack();
            stack.Record(new ComposerSnapshot("a", 1, 1), 0, false);
            stack.TryUndo(new ComposerSnapshot("ab", 2, 2), out _);
            stack.Record(new ComposerSnapshot("a", 1, 1), 5, true);

            Assert.IsFalse(stack.CanRedo);
        }

        [Test]
        public void HistoryIsLimited()
        {
            ComposerUndoStack stack = new ComposerUndoStack();
            for (int i = 0; i <= ComposerUndoStack.MaxSteps; i++)
            {
                stack.Record(new ComposerSnapshot(i.ToString(), 0, 0), i, false);
            }

            int steps = 0;
            ComposerSnapshot current = new ComposerSnapshot("end", 0, 0);
            while (stack.TryUndo(current, out ComposerSnapshot previous))
            {
                current = previous;
                steps++;
            }

            Assert.AreEqual(ComposerUndoStack.MaxSteps, steps);
            Assert.AreEqual("1", current.Text);
        }

        [Test]
        public void ClearDropsHistory()
        {
            ComposerUndoStack stack = new ComposerUndoStack();
            stack.Record(new ComposerSnapshot("a", 1, 1), 0, false);
            stack.Clear();

            Assert.IsFalse(stack.CanUndo);
            Assert.IsFalse(stack.TryUndo(new ComposerSnapshot("b", 1, 1), out _));
        }
    }
}
