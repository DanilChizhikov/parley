using System;
using System.Collections.Generic;
using System.Text;
using DTech.Parley.Editor.Sessions;
using UnityEngine.UIElements;

namespace DTech.Parley.Editor.UI
{
    internal sealed class SidePanel : VisualElement
    {
        private static readonly SidePanelTab[] _order = { SidePanelTab.Todos, SidePanelTab.Plan, SidePanelTab.Tasks, SidePanelTab.Mcp, SidePanelTab.Skills };

        private readonly Dictionary<SidePanelTab, Button> _tabs = new ();
        private readonly ScrollView _content;
        private readonly MarkdownView _plan = new ();
        private readonly McpPanel _mcp = new ();
        private readonly SkillsPanel _skills = new ();

        private ChatSession _session;
        private SidePanelTab _tab = SidePanelTab.Todos;
        private string _renderedPlan;
        private string _renderedList;

        public SidePanel()
        {
            AddToClassList("pl-side");
            VisualElement header = new VisualElement();
            header.AddToClassList("pl-side__tabs");
            foreach (SidePanelTab tab in _order)
            {
                SidePanelTab captured = tab;
                Button button = ParleyStyles.Button(tab == SidePanelTab.Mcp ? "MCP" : tab.ToString(), () => ShowTab(captured), "pl-side__tab");
                button.tooltip = TabTooltip(tab);
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
            _renderedList = null;
            _mcp.Bind(session);
            _skills.Bind(session);
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
            _renderedList = null;
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
            int mcpCount = McpPanel.EnabledCount(_session);
            _tabs[SidePanelTab.Mcp].text = mcpCount > 0 ? "MCP " + mcpCount : "MCP";
            int skillCount = SkillsPanel.EnabledCount(_session);
            _tabs[SidePanelTab.Skills].text = skillCount > 0 ? "Skills " + skillCount : "Skills";
            switch (_tab)
            {
                case SidePanelTab.Plan:
                {
                    RenderPlan();  
                } break;

                case SidePanelTab.Todos:
                {
                    RenderTodos();  
                } break;

                case SidePanelTab.Tasks:
                {
                    RenderTasks();
                } break;

                case SidePanelTab.Mcp:
                {
                    RenderMcp();
                } break;

                case SidePanelTab.Skills:
                {
                    RenderSkills();
                } break;
                
                default:
                    throw new ArgumentOutOfRangeException();
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
                _content.Add(ParleyStyles.Text("No plan yet. In Plan mode the agent explores the project and proposes a plan before changing anything.", ParleyStyles.Muted));
                if (_session.Mode != PermissionMode.Plan)
                {
                    Button switchMode = ParleyStyles.Button("Switch to Plan mode", () => _session?.SetMode(PermissionMode.Plan), "pl-button--small");
                    switchMode.AddToClassList("pl-side__action");
                    _content.Add(switchMode);
                }

                return;
            }

            _plan.SetMarkdown(plan);
            _content.Add(_plan);
        }

        private void RenderMcp()
        {
            if (!_content.Contains(_mcp))
            {
                _content.Clear();
                _content.Add(_mcp);
            }

            _mcp.Refresh();
        }

        private void RenderSkills()
        {
            if (!_content.Contains(_skills))
            {
                _content.Clear();
                _content.Add(_skills);
            }

            _skills.Refresh();
        }

        private static string TabTooltip(SidePanelTab tab)
        {
            return tab switch
            {
                SidePanelTab.Todos => "The agent's checklist for the current work",
                SidePanelTab.Plan => "The latest plan from Plan mode",
                SidePanelTab.Tasks => "Background commands and subagents",
                SidePanelTab.Mcp => "MCP servers available in this chat",
                _ => "Skills available in this chat",
            };
        }

        private static string DescribeStatus(string status)
        {
            return status switch
            {
                "running" => "Running",
                "pending" => "Waiting",
                "completed" => "Done",
                "failed" => "Failed",
                "killed" or "stopped" => "Stopped",
                null => string.Empty,
                _ => status,
            };
        }

        private bool IsRendered(string signature)
        {
            if (signature == _renderedList)
            {
                return true;
            }

            _renderedList = signature;
            return false;
        }

        private void RenderTodos()
        {
            StringBuilder signature = new StringBuilder("todos");
            foreach (TodoItem todo in _session.Todos)
            {
                signature.Append('|').Append(todo.Status).Append(':').Append(todo.Content).Append(':').Append(todo.ActiveForm);
            }

            if (IsRendered(signature.ToString()))
            {
                return;
            }

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
            StringBuilder signature = new StringBuilder("tasks");
            foreach (BackgroundTaskInfo task in _session.Tasks)
            {
                signature.Append('|').Append(task.TaskId).Append(':').Append(task.Status).Append(':').Append(task.LastToolName).Append(':').Append(task.Summary);
            }

            if (IsRendered(signature.ToString()))
            {
                return;
            }

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
            bool completed = task.Status == "completed";
            VisualElement title = new VisualElement();
            title.AddToClassList("pl-task__header");
            Label icon = ParleyStyles.Text(running ? "●" : completed ? "✓" : "✕", "pl-task__icon");
            icon.AddToClassList(running ? "pl-task__icon--running" : completed ? "pl-task__icon--done" : "pl-task__icon--failed");
            icon.tooltip = DescribeStatus(task.Status);
            title.Add(icon);
            Label name = ParleyStyles.Text(task.Description ?? task.TaskId, "pl-task__title");
            name.selection.isSelectable = true;
            title.Add(name);
            row.Add(title);
            string detail = task.TaskType + (string.IsNullOrEmpty(task.LastToolName) ? string.Empty : " · " + task.LastToolName) + " · " + DescribeStatus(task.Status);
            row.Add(ParleyStyles.Text(detail, ParleyStyles.Muted));
            if (!string.IsNullOrEmpty(task.Summary))
            {
                Label summary = ParleyStyles.Text(task.Summary, "pl-task__summary");
                summary.selection.isSelectable = true;
                row.Add(summary);
            }

            if (running)
            {
                string id = task.TaskId;
                Button stop = ParleyStyles.Button("Stop", () => _session?.StopTask(id), "pl-button--small");
                stop.tooltip = "Stop this task";
                stop.AddToClassList("pl-side__action");
                row.Add(stop);
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