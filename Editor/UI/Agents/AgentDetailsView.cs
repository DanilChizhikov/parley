using System.Collections.Generic;
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

            Add(BuildHeader(info, subagentCount));
            if (!info.IsMain)
            {
                AddMarkdown("Prompt", info.Prompt, null);
                AddMarkdown("Result", info.Result, info.Status == AgentNodeStatus.Failed ? "pl-agent-section--failed" : "pl-agent-section--result");
            }

            AddActivity(info);
        }

        private static string Plural(int count, string noun)
        {
            return count == 1 ? "1 " + noun : count + " " + noun + "s";
        }

        private static VisualElement BuildHeader(AgentNodeInfo info, int subagentCount)
        {
            VisualElement header = new VisualElement();
            header.AddToClassList("pl-agent-details__header");
            VisualElement top = new VisualElement();
            top.AddToClassList("pl-agent-details__top");
            Label pill = ParleyStyles.Text(AgentGraphNode.Glyph(info.Status) + " " + info.Status, "pl-agent-pill");
            pill.AddToClassList("pl-agent-pill--" + info.Status.ToString().ToLowerInvariant());
            top.Add(pill);
            Label title = ParleyStyles.Text(info.IsMain || string.IsNullOrEmpty(info.Description) ? info.Title : info.Description,
                "pl-agent-details__title");
            title.selection.isSelectable = true;
            top.Add(title);
            header.Add(top);
            List<string> meta = new List<string>(3);
            if (!string.IsNullOrEmpty(info.Type))
            {
                meta.Add(info.Type);
            }

            meta.Add(Plural(info.ToolCount, "tool call"));
            if (info.IsMain)
            {
                meta.Add(subagentCount == 0 ? "no subagents yet" : Plural(subagentCount, "subagent"));
            }

            header.Add(ParleyStyles.Text(string.Join("  ·  ", meta), "pl-agent-details__meta"));
            return header;
        }

        private VisualElement AddSection(string caption, string badge, string modifier)
        {
            VisualElement section = new VisualElement();
            section.AddToClassList("pl-agent-section");
            if (modifier != null)
            {
                section.AddToClassList(modifier);
            }

            VisualElement header = new VisualElement();
            header.AddToClassList("pl-agent-section__header");
            header.Add(ParleyStyles.Text(caption, "pl-agent-section__caption"));
            if (badge != null)
            {
                header.Add(ParleyStyles.Spacer());
                header.Add(ParleyStyles.Text(badge, "pl-agent-section__badge"));
            }

            section.Add(header);
            VisualElement body = new VisualElement();
            body.AddToClassList("pl-agent-section__body");
            section.Add(body);
            Add(section);
            return body;
        }

        private void AddMarkdown(string caption, string markdown, string modifier)
        {
            if (string.IsNullOrWhiteSpace(markdown))
            {
                return;
            }

            MarkdownView view = new MarkdownView();
            view.SetMarkdown(markdown);
            AddSection(caption, null, modifier).Add(view);
        }

        private void AddActivity(AgentNodeInfo info)
        {
            if (info.Activity.Count == 0)
            {
                return;
            }

            VisualElement body = AddSection("Activity", Plural(info.ToolCount, "tool call"), null);
            int start = info.Activity.Count > MaxActivity ? info.Activity.Count - MaxActivity : 0;
            if (start > 0)
            {
                body.Add(ParleyStyles.Text(start + " earlier steps hidden", "pl-agent-section__hint"));
            }

            VisualElement timeline = new VisualElement();
            timeline.AddToClassList("pl-agent-timeline");
            for (int i = start; i < info.Activity.Count; i++)
            {
                TranscriptBlock block = info.Activity[i];
                if (block.Kind == BlockKind.ToolUse || !string.IsNullOrWhiteSpace(block.Text))
                {
                    timeline.Add(BuildStep(block));
                }
            }

            body.Add(timeline);
        }

        private static VisualElement BuildStep(TranscriptBlock block)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("pl-agent-step");
            if (block.Kind != BlockKind.ToolUse)
            {
                row.AddToClassList("pl-agent-step--text");
                Label text = ParleyStyles.Text(ToolPresenter.Truncate(block.Text?.Trim(), MaxTextLength), "pl-agent-step__text");
                text.selection.isSelectable = true;
                row.Add(text);
                return row;
            }

            row.AddToClassList("pl-agent-step--tool");
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
