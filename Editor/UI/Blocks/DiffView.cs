using System.Collections.Generic;
using System.Text;
using UnityEngine.UIElements;

namespace DTech.Parley.Editor.UI
{
    internal sealed class DiffView : VisualElement
    {
        private const int MaxRows = 600;

        public DiffView(string oldText, string newText, string title = null)
        {
            AddToClassList("pl-diff");
            List<DiffLine> lines = LineDiff.Compute(LineDiff.SplitLines(oldText), LineDiff.SplitLines(newText));
            Add(BuildHeader(lines, title));
            ScrollView scroll = new ScrollView(ScrollViewMode.Horizontal);
            scroll.AddToClassList("pl-diff__scroll");
            VisualElement body = new VisualElement();
            body.AddToClassList("pl-diff__body");
            scroll.Add(body);
            Add(scroll);
            AddRows(body, LineDiff.Hunks(lines));
        }

        private static VisualElement BuildHeader(List<DiffLine> lines, string title)
        {
            int added = 0;
            int removed = 0;
            foreach (DiffLine line in lines)
            {
                if (line.Operation == DiffOperation.Insert)
                {
                    added++;
                }
                else if (line.Operation == DiffOperation.Delete)
                {
                    removed++;
                }
            }

            VisualElement header = new VisualElement();
            header.AddToClassList("pl-diff__header");
            if (!string.IsNullOrEmpty(title))
            {
                header.Add(ParleyStyles.Text(title, "pl-diff__title"));
            }

            header.Add(ParleyStyles.Text("+" + added, "pl-diff__added"));
            header.Add(ParleyStyles.Text("-" + removed, "pl-diff__removed"));
            return header;
        }

        private static void AddRows(VisualElement body, List<DiffHunk> hunks)
        {
            if (hunks.Count == 0)
            {
                body.Add(Row("No changes", "pl-diff__row--hunk"));
                return;
            }

            int rows = 0;
            foreach (DiffHunk hunk in hunks)
            {
                if (rows >= MaxRows)
                {
                    body.Add(Row("… diff truncated", "pl-diff__row--hunk"));
                    return;
                }

                body.Add(Row(hunk.Header, "pl-diff__row--hunk"));
                foreach (DiffLine line in hunk.Lines)
                {
                    if (rows >= MaxRows)
                    {
                        body.Add(Row("… diff truncated", "pl-diff__row--hunk"));
                        return;
                    }

                    body.Add(Row(Format(line), RowClass(line.Operation)));
                    rows++;
                }
            }
        }

        private static string Format(DiffLine line)
        {
            StringBuilder text = new StringBuilder();
            text.Append(Gutter(line.OldNumber)).Append(' ').Append(Gutter(line.NewNumber)).Append(' ');
            text.Append(line.Operation == DiffOperation.Insert ? '+' : line.Operation == DiffOperation.Delete ? '-' : ' ').Append(' ').Append(line.Text);
            return text.ToString();
        }

        private static string RowClass(DiffOperation operation)
        {
            return operation switch
            {
                DiffOperation.Insert => "pl-diff__row--add",
                DiffOperation.Delete => "pl-diff__row--del",
                _ => "pl-diff__row--ctx",
            };
        }

        private static string Gutter(int number)
        {
            return number > 0 ? number.ToString().PadLeft(4) : "    ";
        }

        private static Label Row(string text, string className)
        {
            Label label = new Label(text) { enableRichText = false };
            label.selection.isSelectable = true;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            label.AddToClassList("pl-diff__row");
            label.AddToClassList(className);
            ParleyStyles.UseMonospace(label);
            return label;
        }
    }
}