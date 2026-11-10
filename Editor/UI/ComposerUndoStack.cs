using System.Collections.Generic;

namespace DTech.Parley.Editor.UI
{
    internal sealed class ComposerUndoStack
    {
        public const int MaxSteps = 100;
        public const double MergeSeconds = 1.0;

        private readonly List<ComposerSnapshot> _undo = new ();
        private readonly List<ComposerSnapshot> _redo = new ();

        private double _lastRecordTime = double.NegativeInfinity;

        public bool CanUndo => _undo.Count > 0;

        public bool CanRedo => _redo.Count > 0;

        public void Record(ComposerSnapshot before, double time, bool merge)
        {
            _redo.Clear();
            bool merged = merge && _undo.Count > 0 && time - _lastRecordTime < MergeSeconds;
            _lastRecordTime = merge ? time : double.NegativeInfinity;
            if (merged)
            {
                return;
            }

            _undo.Add(before);
            if (_undo.Count > MaxSteps)
            {
                _undo.RemoveAt(0);
            }
        }

        public bool TryUndo(ComposerSnapshot current, out ComposerSnapshot previous)
        {
            return Move(_undo, _redo, current, out previous);
        }

        public bool TryRedo(ComposerSnapshot current, out ComposerSnapshot next)
        {
            return Move(_redo, _undo, current, out next);
        }

        public void Clear()
        {
            _undo.Clear();
            _redo.Clear();
            _lastRecordTime = double.NegativeInfinity;
        }

        private bool Move(List<ComposerSnapshot> from, List<ComposerSnapshot> to, ComposerSnapshot current, out ComposerSnapshot target)
        {
            _lastRecordTime = double.NegativeInfinity;
            if (from.Count == 0)
            {
                target = default;
                return false;
            }

            target = from[from.Count - 1];
            from.RemoveAt(from.Count - 1);
            to.Add(current);
            return true;
        }
    }
}
