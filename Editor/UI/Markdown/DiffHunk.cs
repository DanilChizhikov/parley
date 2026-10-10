using System;
using System.Collections.Generic;

namespace DTech.Parley.Editor.UI
{
    internal sealed class DiffHunk
    {
        public List<DiffLine> Lines { get; } = new ();
        public int OldStart { get; private set; }

        public string Header => "@@ -" + OldStart + "," + OldCount + " +" + NewStart + "," + NewCount + " @@";
        
        private int NewStart { get; set; }
        private int OldCount { get; set; }
        private int NewCount { get; set; }

        public void AddRange(List<DiffLine> lines, int from, int to)
        {
            for (int i = Math.Max(0, from); i <= to && i < lines.Count; i++)
            {
                Add(lines[i]);
            }
        }

        private void Add(DiffLine line)
        {
            Lines.Add(line);
            if (line.Operation != DiffOperation.Insert)
            {
                OldCount++;
                if (OldStart == 0)
                {
                    OldStart = line.OldNumber;
                }
            }

            if (line.Operation != DiffOperation.Delete)
            {
                NewCount++;
                if (NewStart == 0)
                {
                    NewStart = line.NewNumber;
                }
            }
        }
    }
}