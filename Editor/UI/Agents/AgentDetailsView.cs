using UnityEngine;
using UnityEngine.UIElements;

namespace DTech.Parley.Editor.UI
{
    internal sealed class AgentDetailsView : ScrollView
    {
        private const int MaxActivity = 100;
        private const int MaxTextLength = 600;

        private string _shownId;

        public AgentDetailsView() : base(ScrollViewMode.Vertical)
        {
            AddToClassList("pl-agent-details");
        }

        public void Show(AgentNodeInfo info, int subagentCount)
        {
            bool sameAgent = info != null && info.Id == _shownId;
            Vector2 offset = scrollOffset;
            _shownId = info?.Id;
            Clear();
            if (sameAgent)
            {
                schedule.Execute(() => scrollOffset = offset);
            }

            if (info == null)
            {
                Add(ParleyStyles.Text("Select an agent to see its prompt and what it does.", ParleyStyles.Muted));
                return;
            }

            VisualElement header = new VisualElement();
            header.AddToClassList("pl-agent-details__header");
            Label glyph = ParleyStyles.Text(AgentGraphNode.Glyph(info.Status), "pl-agent-details__status");
            glyph.AddToClassList("pl-agent-details__status--" + info.Status.ToString().ToLowerInvariant());
            glyph.tooltip = info.Status.ToString();
            header.Add(glyph);
            Label title = ParleyStyles.Text(info.Title, "pl-agent-details__title");
            title.selection.isSelectable = true;
            header.Add(title);
            Add(header);
            if (info.IsMain)
            {
                Add(ParleyStyles.Text(subagentCount == 0 ? "No subagents in this chat yet." : Plural(subagentCount, "subagent") + " in this chat.",
                    ParleyStyles.Muted));
            }
            else
            {
                AddMarkdown("Prompt", info.Prompt);
            }

            AddActivity(info);
            if (!info.IsMain)
            {
                AddMarkdown("Result", info.Result);
            }
        }

        private static string Plural(int count, string noun)
        {
            return count == 1 ? "1 " + noun : count + " " + noun + "s";
        }

        private void AddMarkdown(string caption, string markdown)
        {
            if (string.IsNullOrWhiteSpace(markdown))
            {
                return;
            }

            Add(ParleyStyles.Text(caption, "pl-agent-details__caption"));
            MarkdownView view = new MarkdownView();
            view.SetMarkdown(markdown);
            Add(view);
        }

        private void AddActivity(AgentNodeInfo info)
        {
            if (info.Activity.Count == 0)
            {
                return;
            }

            Add(ParleyStyles.Text("Activity · " + Plural(info.ToolCount, "tool call"), "pl-agent-details__caption"));
            int start = info.Activity.Count > MaxActivity ? info.Activity.Count - MaxActivity : 0;
            if (start > 0)
            {
                Add(ParleyStyles.Text(start + " earlier steps hidden", ParleyStyles.Muted));
            }

            for (int i = start; i < info.Activity.Count; i++)
            {
                TranscriptBlock block = info.Activity[i];
                if (block.Kind == BlockKind.ToolUse || !string.IsNullOrWhiteSpace(block.Text))
                {
                    Add(BuildStep(block));
                }
            }
        }

        private static VisualElement BuildStep(TranscriptBlock block)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("pl-agent-step");
            if (block.Kind != BlockKind.ToolUse)
            {
                Label text = ParleyStyles.Text(ToolPresenter.Truncate(block.Text?.Trim(), MaxTextLength), "pl-agent-step__text");
                text.selection.isSelectable = true;
                row.Add(text);
                return row;
            }

            bool done = !block.IsError && block.Result != null;
            bool running = !block.IsError && block.Result == null && !block.IsFinished;
            Label status = ParleyStyles.Text(block.IsError ? "✕" : done ? "✓" : running ? "●" : "○", "pl-agent-step__status");
            status.EnableInClassList("pl-agent-step__status--running", running);
            status.EnableInClassList("pl-agent-step__status--done", done);
            status.EnableInClassList("pl-agent-step__status--error", block.IsError);
            row.Add(status);
            row.Add(ParleyStyles.Text(ToolPresenter.DisplayName(block.ToolName), "pl-agent-step__tool"));
            string summary = ToolPresenter.Summary(block);
            if (!string.IsNullOrEmpty(summary))
            {
                Label label = ParleyStyles.Text(summary, "pl-agent-step__summary");
                label.tooltip = summary;
                row.Add(label);
            }

            return row;
        }
    }
}
