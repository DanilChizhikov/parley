using UnityEngine.UIElements;

namespace DTech.Parley.Editor.UI
{
    internal sealed class NoticeBlockView : BlockView
    {
        public NoticeBlockView(TranscriptBlock block) : base(block)
        {
            AddToClassList("pl-notice");
            AddToClassList("pl-notice--" + block.Level.ToString().ToLowerInvariant());
            if (block.Level == NoticeLevel.Error)
            {
                Label label = ParleyStyles.Text(block.Text, "pl-notice__text");
                label.selection.isSelectable = true;
                ParleyStyles.UseMonospace(label);
                Add(label);
                return;
            }

            MarkdownView view = new MarkdownView();
            view.SetMarkdown(block.Text);
            Add(view);
        }

        public override void Refresh()
        {
        }
    }
}