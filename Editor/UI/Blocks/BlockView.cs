using UnityEngine.UIElements;

namespace DTech.Parley.Editor.UI
{
    internal abstract class BlockView : VisualElement
    {
        protected TranscriptBlock Block { get; }

        protected BlockView(TranscriptBlock block)
        {
            Block = block;
            AddToClassList("pl-block");
        }

        public abstract void Refresh();
    }
}