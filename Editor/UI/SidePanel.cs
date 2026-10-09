using System.Collections.Generic;
using DTech.Parley.Editor.Sessions;
using UnityEngine.UIElements;

namespace DTech.Parley.Editor.UI
{
    internal sealed class SidePanel : VisualElement
    {
        private static readonly SidePanelTab[] _order = { SidePanelTab.Todos, SidePanelTab.Plan, SidePanelTab.Tasks };

        private readonly Dictionary<SidePanelTab, Button> _tabs = new ();
        private readonly ScrollView _content;
        private readonly MarkdownView _plan = new ();

        private ChatSession _session;
        private SidePanelTab _tab = SidePanelTab.Todos;
        private string _renderedPlan;

        public SidePanel()
        {
            AddToClassList("pl-side");
            VisualElement header = new VisualElement();
            header.AddToClassList("pl-side__tabs");
            foreach (SidePanelTab tab in _order)
            {
                SidePanelTab captured = tab;
                Button button = ParleyStyles.Button(tab.ToString(), () => ShowTab(captured), "pl-side__tab");
                _tabs[tab] = button;
                header.Add(button);
            }

            Add(header);
            _content = new ScrollView(ScrollViewMode.Vertical);
            _content.AddToClassList("pl-side__content");
            Add(_content);
        }

        public void Bind(ChatSession session)
        {
            _session = session;
            _renderedPlan = null;
            if (session != null && !string.IsNullOrEmpty(session.Record.LatestPlan) && session.Todos.Count == 0)
            {
                _tab = SidePanelTab.Plan;
            }

            Refresh();
        }

        public void ShowTab(SidePanelTab tab)
        {
            _tab = tab;
            _renderedPlan = null;
            Refresh();
        }

        public void Refresh()
        {
            foreach (KeyValuePair<SidePanelTab, Button> pair in _tabs)
            {
                pair.Value.EnableInClassList("pl-side__tab--active", pair.Key == _tab);
            }

            if (_session == null)
            {
                _content.Clear();
                return;
            }

            _tabs[SidePanelTab.Todos].text = _session.Todos.Count > 0 ? "Todos " + CompletedCount() + "/" + _session.Todos.Count : "Todos";
            _tabs[SidePanelTab.Tasks].text = _session.Tasks.Count > 0 ? "Tasks " + _session.Tasks.Count : "Tasks";
            switch (_tab)
            {
                case SidePanelTab.Plan:
                    RenderPlan();
                    break;
                case SidePanelTab.Todos:
                    RenderTodos();
                    break;
                default:
                    RenderTasks();
                    break;
            }
        }

        private void RenderPlan()
        {
            string plan = _session.Record.LatestPlan;
            if (plan == _renderedPlan && _content.Contains(_plan))
            {
                return;
            }

            _renderedPlan = plan;
            _content.Clear();
            if (string.IsNullOrEmpty(plan))
            {
                _content.Add(ParleyStyles.Text("No plan yet. Switch the mode to Plan to have the agent explore and propose one.", ParleyStyles.Muted));
                return;
            }

            _plan.SetMarkdown(plan);
            _content.Add(_plan);
        }

        private void RenderTodos()
        {
            _content.Clear();
            if (_session.Todos.Count == 0)
            {
                _content.Add(ParleyStyles.Text("No todos yet. The agent creates them for multi-step work.", ParleyStyles.Muted));
                return;
            }

            foreach (TodoItem todo in _session.Todos)
            {
                bool active = todo.Status == "in_progress" && !string.IsNullOrEmpty(todo.ActiveForm);
                _content.Add(ToolPresenter.TodoLabel(todo.Status, active ? todo.ActiveForm : todo.Content));
            }
        }

        private void RenderTasks()
        {
            _content.Clear();
            if (_session.Tasks.Count == 0)
            {
                _content.Add(ParleyStyles.Text("No background tasks or subagents.", ParleyStyles.Muted));
                return;
            }

            foreach (BackgroundTaskInfo task in _session.Tasks)
            {
                _content.Add(BuildTask(task));
            }
        }

        private VisualElement BuildTask(BackgroundTaskInfo task)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("pl-task");
            bool running = task.Status == "running" || task.Status == "pending";
            string icon = running ? "● " : task.Status == "completed" ? "✓ " : "✕ ";
            row.Add(ParleyStyles.Text(icon + (task.Description ?? task.TaskId), "pl-task__title"));
            string detail = task.TaskType + (string.IsNullOrEmpty(task.LastToolName) ? string.Empty : " · " + task.LastToolName) + " · " + task.Status;
            row.Add(ParleyStyles.Text(detail, ParleyStyles.Muted));
            if (!string.IsNullOrEmpty(task.Summary))
            {
                row.Add(ParleyStyles.Text(task.Summary, "pl-task__summary"));
            }

            if (running)
            {
                string id = task.TaskId;
                row.Add(ParleyStyles.Button("Stop", () => _session?.StopTask(id), "pl-button--small"));
            }

            return row;
        }

        private int CompletedCount()
        {
            int count = 0;
            foreach (TodoItem todo in _session.Todos)
            {
                if (todo.Status == "completed")
                {
                    count++;
                }
            }

            return count;
        }
    }
}