using UnityEngine.UIElements;

namespace DTech.Parley.Editor.UI
{
    internal sealed class ToolCardView : BlockView
    {
        private readonly Label _status;
        private readonly Label _title;
        private readonly Label _summary;
        private readonly Label _chevron;
        private readonly VisualElement _body;
        private readonly VisualElement _nested;

        private bool _expanded;
        private bool _errorExpanded;
        private string _renderedSignature;

        public ToolCardView(TranscriptBlock block) : base(block)
        {
            AddToClassList("pl-tool");
            VisualElement header = new VisualElement();
            header.AddToClassList("pl-tool__header");
            _status = ParleyStyles.Text("●", "pl-tool__status");
            _title = ParleyStyles.Text(string.Empty, "pl-tool__title");
            _summary = ParleyStyles.Text(string.Empty, "pl-tool__summary");
            _chevron = ParleyStyles.Text("▸", "pl-tool__chevron");
            header.Add(_status);
            header.Add(_title);
            header.Add(_summary);
            header.Add(_chevron);
            header.RegisterCallback<ClickEvent>(HeaderClickedHandler);
            header.tooltip = "Show or hide details";
            Add(header);
            _body = new VisualElement();
            _body.AddToClassList("pl-tool__body");
            Add(_body);
            _nested = new VisualElement();
            _nested.AddToClassList("pl-tool__children");
            ParleyStyles.SetVisible(_nested, false);
            Add(_nested);
            _expanded = ToolPresenter.ExpandedByDefault(block);
            Refresh();
        }

        public override void Refresh()
        {
            bool done = !Block.IsError && Block.Result != null;
            bool stopped = !Block.IsError && Block.Result == null && Block.IsFinished;
            bool running = !Block.IsError && Block.Result == null && !Block.IsFinished;
            _status.text = Block.IsError ? "✕" : done ? "✓" : stopped ? "○" : "●";
            _status.tooltip = Block.IsError ? "Failed" : done ? "Done" : stopped ? "Stopped before it finished" : "Running";
            _status.EnableInClassList("pl-tool__status--running", running);
            _status.EnableInClassList("pl-tool__status--stopped", stopped);
            _status.EnableInClassList("pl-tool__status--done", done);
            _status.EnableInClassList("pl-tool__status--error", Block.IsError);
            _title.text = ToolPresenter.DisplayName(Block.ToolName);
            _title.tooltip = Block.ToolName;
            _summary.text = ToolPresenter.Summary(Block);
            _summary.tooltip = _summary.text;
            if (Block.IsError && !_errorExpanded)
            {
                _errorExpanded = true;
                _expanded = true;
            }

            _chevron.text = _expanded ? "▾" : "▸";
            ParleyStyles.SetVisible(_body, _expanded);
            if (!_expanded)
            {
                return;
            }

            string signature = Block.IsFinished + "|" + (Block.Result?.Length ?? -1) + "|" + (Block.Input?.Count ?? -1) + "|" + Block.IsError
                + "|" + (Block.PartialInputJson?.Length ?? -1);
            if (signature == _renderedSignature)
            {
                return;
            }

            _renderedSignature = signature;
            _body.Clear();
            if (Block.Input == null)
            {
                _body.Add(ParleyStyles.Text(Block.IsFinished ? "No parameters." : "Preparing…", ParleyStyles.Muted));
                return;
            }

            _body.Add(ToolPresenter.BuildBody(Block));
        }

        public void AddNested(VisualElement view)
        {
            _nested.Add(view);
            ParleyStyles.SetVisible(_nested, true);
        }

        private void HeaderClickedHandler(ClickEvent evt)
        {
            _expanded = !_expanded;
            _renderedSignature = null;
            Refresh();
        }
    }
}