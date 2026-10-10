using UnityEngine.UIElements;

namespace DTech.Parley.Editor.UI
{
    internal sealed class ThinkingBlockView : BlockView
    {
        private readonly Foldout _foldout = new () { value = false };
        private readonly Label _text;

        public ThinkingBlockView(TranscriptBlock block) : base(block)
        {
            AddToClassList("pl-block--thinking");
            _text = ParleyStyles.Text(string.Empty, "pl-thinking__text");
            _text.selection.isSelectable = true;
            _foldout.Add(_text);
            Add(_foldout);
            Refresh();
        }

        public override void Refresh()
        {
            _foldout.text = Block.IsFinished ? "Thinking" : "Thinking…";
            if (_foldout.value || Block.IsFinished)
            {
                _text.text = Block.Text;
            }
        }
    }
}