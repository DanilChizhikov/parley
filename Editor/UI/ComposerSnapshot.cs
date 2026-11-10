namespace DTech.Parley.Editor.UI
{
    internal readonly struct ComposerSnapshot
    {
        public readonly string Text;
        public readonly int Cursor;
        public readonly int Select;

        public ComposerSnapshot(string text, int cursor, int select)
        {
            Text = text ?? string.Empty;
            Cursor = cursor;
            Select = select;
        }
    }
}
