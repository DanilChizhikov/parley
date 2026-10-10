using System.Collections.Generic;
using System.Text;

namespace DTech.Parley.Editor.UI
{
    internal sealed class MarkdownBlock
    {
        public List<MarkdownListItem> Items { get; } = new ();
        public List<string[]> Rows { get; } = new ();
        public MarkdownBlockType Type { get; set; }
        public int Level { get; set; }
        public string Text { get; set; } = string.Empty;
        public string Language { get; set; }
        public bool IsClosed { get; set; } = true;

        public string Signature
        {
            get
            {
                StringBuilder builder = new StringBuilder();
                builder.Append((int)Type).Append('|').Append(Level).Append('|').Append(Language).Append('|').Append(IsClosed).Append('|').Append(Text);
                foreach (MarkdownListItem item in Items)
                {
                    builder.Append('\u0001').Append(item.Indent).Append(item.Marker).Append(item.Checked).Append(item.Text);
                }

                foreach (string[] row in Rows)
                {
                    builder.Append('\u0002').Append(string.Join("\u0003", row));
                }

                return builder.ToString();
            }
        }
    }
}