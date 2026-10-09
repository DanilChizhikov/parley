using System;
using System.Collections.Generic;

namespace DTech.Parley.Editor.UI
{
    internal static class LineDiff
    {
        private const int MaxEditDistance = 1000;

        public static string[] SplitLines(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return Array.Empty<string>();
            }

            return text.Replace("\r\n", "\n").Split('\n');
        }

        public static List<DiffLine> Compute(string[] oldLines, string[] newLines)
        {
            int prefix = 0;
            while (prefix < oldLines.Length && prefix < newLines.Length && oldLines[prefix] == newLines[prefix])
            {
                prefix++;
            }

            int suffix = 0;
            while (suffix < oldLines.Length - prefix && suffix < newLines.Length - prefix
                && oldLines[oldLines.Length - 1 - suffix] == newLines[newLines.Length - 1 - suffix])
            {
                suffix++;
            }

            List<DiffLine> result = new List<DiffLine>();
            for (int i = 0; i < prefix; i++)
            {
                result.Add(new DiffLine(DiffOperation.Equal, oldLines[i], i + 1, i + 1));
            }

            LineSpan left = new LineSpan(oldLines, prefix, oldLines.Length - prefix - suffix);
            LineSpan right = new LineSpan(newLines, prefix, newLines.Length - prefix - suffix);
            int oldIndex = prefix;
            int newIndex = prefix;
            foreach (DiffOperation operation in Myers(left, right))
            {
                switch (operation)
                {
                    case DiffOperation.Equal:
                        result.Add(new DiffLine(DiffOperation.Equal, oldLines[oldIndex], ++oldIndex, ++newIndex));
                        break;
                    case DiffOperation.Delete:
                        result.Add(new DiffLine(DiffOperation.Delete, oldLines[oldIndex], ++oldIndex, 0));
                        break;
                    default:
                        result.Add(new DiffLine(DiffOperation.Insert, newLines[newIndex], 0, ++newIndex));
                        break;
                }
            }

            for (int i = 0; i < suffix; i++)
            {
                result.Add(new DiffLine(DiffOperation.Equal, oldLines[oldIndex], ++oldIndex, ++newIndex));
            }

            return result;
        }

        public static List<DiffHunk> Hunks(List<DiffLine> lines, int context = 3)
        {
            List<DiffHunk> hunks = new List<DiffHunk>();
            DiffHunk current = null;
            int lastChange = int.MinValue;
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Operation == DiffOperation.Equal)
                {
                    continue;
                }

                int start = Math.Max(0, i - context);
                if (current == null || start > lastChange + context + 1)
                {
                    current?.AddRange(lines, lastChange + 1, Math.Min(lines.Count - 1, lastChange + context));
                    current = new DiffHunk();
                    hunks.Add(current);
                    current.AddRange(lines, start, i - 1);
                }
                else
                {
                    current.AddRange(lines, lastChange + 1, i - 1);
                }

                current.AddRange(lines, i, i);
                lastChange = i;
            }

            current?.AddRange(lines, lastChange + 1, Math.Min(lines.Count - 1, lastChange + context));
            return hunks;
        }

        private static List<DiffOperation> Replace(int deleted, int inserted)
        {
            List<DiffOperation> operations = new List<DiffOperation>(deleted + inserted);
            for (int i = 0; i < deleted; i++)
            {
                operations.Add(DiffOperation.Delete);
            }

            for (int i = 0; i < inserted; i++)
            {
                operations.Add(DiffOperation.Insert);
            }

            return operations;
        }

        private static List<DiffOperation> Myers(LineSpan left, LineSpan right)
        {
            int n = left.Count;
            int m = right.Count;
            if (n == 0 || m == 0 || n + m > MaxEditDistance * 16)
            {
                return Replace(n, m);
            }

            int max = Math.Min(n + m, MaxEditDistance);
            int offset = max;
            int[] v = new int[2 * max + 2];
            List<int[]> trace = new List<int[]>();
            int finalD = -1;
            for (int d = 0; d <= max && finalD < 0; d++)
            {
                trace.Add((int[])v.Clone());
                for (int k = -d; k <= d; k += 2)
                {
                    int x = k == -d || (k != d && v[offset + k - 1] < v[offset + k + 1]) ? v[offset + k + 1] : v[offset + k - 1] + 1;
                    int y = x - k;
                    while (x < n && y < m && left[x] == right[y])
                    {
                        x++;
                        y++;
                    }

                    v[offset + k] = x;
                    if (x >= n && y >= m)
                    {
                        finalD = d;
                        break;
                    }
                }
            }

            if (finalD < 0)
            {
                return Replace(n, m);
            }

            List<DiffOperation> operations = new List<DiffOperation>();
            int cx = n;
            int cy = m;
            for (int d = finalD; d >= 0; d--)
            {
                int[] previous = trace[d];
                int k = cx - cy;
                int previousK = k == -d || (k != d && previous[offset + k - 1] < previous[offset + k + 1]) ? k + 1 : k - 1;
                int previousX = d == 0 ? 0 : previous[offset + previousK];
                int previousY = d == 0 ? 0 : previousX - previousK;
                while (cx > previousX && cy > previousY)
                {
                    operations.Add(DiffOperation.Equal);
                    cx--;
                    cy--;
                }

                if (d > 0)
                {
                    if (cx == previousX)
                    {
                        operations.Add(DiffOperation.Insert);
                        cy--;
                    }
                    else
                    {
                        operations.Add(DiffOperation.Delete);
                        cx--;
                    }
                }
            }

            operations.Reverse();
            return operations;
        }

        private readonly struct LineSpan
        {
            private readonly string[] _lines;
            private readonly int _start;

            public int Count { get; }

            public string this[int index] => _lines[_start + index];

            public LineSpan(string[] lines, int start, int count)
            {
                _lines = lines;
                _start = start;
                Count = count;
            }
        }
    }
}