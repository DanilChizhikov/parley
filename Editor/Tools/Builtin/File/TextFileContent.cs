namespace DTech.Parley.Editor.Tools.Builtin
{
    internal readonly struct TextFileContent
    {
        public string Text { get; }
        public bool HasBom { get; }

        public TextFileContent(string text, bool hasBom)
        {
            Text = text;
            HasBom = hasBom;
        }
    }
}
