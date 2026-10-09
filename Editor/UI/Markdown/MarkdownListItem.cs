namespace DTech.Parley.Editor.UI
{
    internal sealed class MarkdownListItem
    {
        public string Text { get; set; }
        public int Indent { get; set; }
        public string Marker { get; set; }
        public bool? Checked { get; set; }
    }
}