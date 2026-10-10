namespace DTech.Parley.Editor.UI
{
    internal sealed class TextBlockView : BlockView
    {
        private readonly MarkdownView _markdown = new ();

        public TextBlockView(TranscriptBlock block, bool isUser) : base(block)
        {
            AddToClassList(isUser ? "pl-block--user-text" : "pl-block--text");
            Add(_markdown);
            Refresh();
        }

        public override void Refresh()
        {
            _markdown.SetMarkdown(Block.Text);
        }
    }
}