using System;
using System.Collections.Generic;
using System.Text;
using DTech.Parley.Editor.Mcp;
using DTech.Parley.Editor.Sessions;
using UnityEditor;
using UnityEngine.UIElements;

namespace DTech.Parley.Editor.UI
{
    internal sealed class McpPanel : VisualElement
    {
        private readonly VisualElement _formHost;
        private readonly VisualElement _list;
        private readonly StringBuilder _key = new ();
        private readonly List<McpServerStatus> _external = new ();

        private ChatSession _session;
        private McpServerForm _form;
        private string _renderedKey;

        public McpPanel()
        {
            AddToClassList("pl-mcp");
            VisualElement header = new VisualElement();
            header.AddToClassList("pl-mcp__header");
            header.Add(ParleyStyles.Text("MCP servers in this chat", "pl-mcp__title"));
            header.Add(ParleyStyles.Spacer());
            header.Add(ParleyStyles.Button("Refresh", RefreshStatus, "pl-button--small"));
            header.Add(ParleyStyles.Button("+ Add", () => OpenForm(null), "pl-button--small"));
            Add(header);
            _formHost = new VisualElement();
            Add(_formHost);
            _list = new VisualElement();
            Add(_list);
        }

        public static int EnabledCount(ChatSession session)
        {
            int count = session.Record.EnabledMcpServerIds.Count;
            foreach (McpServerStatus status in session.McpStatuses)
            {
                if (status.IsExternal && status.State != McpConnectionState.Disabled)
                {
                    count++;
                }
            }

            return count;
        }

        public void Bind(ChatSession session)
        {
            _session = session;
            _renderedKey = null;
        }

        public void Refresh()
        {
            if (_session == null)
            {
                _list.Clear();
                _renderedKey = null;
                return;
            }

            string key = BuildKey();
            if (key == _renderedKey)
            {
                return;
            }

            _renderedKey = key;
            Render();
        }

        private static string Describe(McpServerDefinition definition)
        {
            return definition.Transport == McpTransport.Stdio
                ? "stdio · " + (definition.Command + " " + definition.Arguments).Trim()
                : "http · " + definition.Url;
        }

        private static string StateClass(McpConnectionState state)
        {
            return state switch
            {
                McpConnectionState.Connected => "pl-mcp__dot--connected",
                McpConnectionState.Failed => "pl-mcp__dot--failed",
                McpConnectionState.NeedsAuth => "pl-mcp__dot--failed",
                McpConnectionState.Disabled => "pl-mcp__dot--off",
                _ => "pl-mcp__dot--pending",
            };
        }

        private string StateText(McpServerStatus status)
        {
            switch (status.State)
            {
                case McpConnectionState.Connected:
                    return status.ToolCount >= 0 ? "Connected · " + status.ToolCount + (status.ToolCount == 1 ? " tool" : " tools") : "Connected";
                case McpConnectionState.Failed:
                    return "Failed";
                case McpConnectionState.NeedsAuth:
                    return _session.Profile.Kind == ProfileKind.Codex ? "Needs sign-in (codex mcp login " + status.Name + ")" : "Needs sign-in (run /mcp in Claude Code)";
                case McpConnectionState.Disabled:
                    return "Off in this chat";
                default:
                    return "Starting…";
            }
        }

        private string BuildKey()
        {
            _key.Clear();
            _key.Append(_session.Record.Id).Append('|').Append(_session.Profile.Kind).Append('|').Append(_session.Backend.HasPendingRestart)
                .Append('|').Append(_session.Backend.IsRunning).Append('|');
            foreach (McpServerDefinition definition in ParleyUserSettings.instance.McpServers)
            {
                _key.Append(definition.Id).Append(':').Append(definition.Name).Append(':').Append(Describe(definition)).Append(':')
                    .Append(_session.IsMcpServerEnabled(definition.Id)).Append(';');
            }

            _key.Append('|');
            foreach (McpServerStatus status in _session.McpStatuses)
            {
                _key.Append(status.Name).Append(':').Append(status.IsExternal).Append(':').Append(status.State).Append(':')
                    .Append(status.ToolCount).Append(':').Append(status.Error).Append(';');
            }

            return _key.ToString();
        }

        private void Render()
        {
            _list.Clear();
            _list.Add(ParleyStyles.Text("Parley servers", "pl-mcp__section"));
            IReadOnlyList<McpServerDefinition> definitions = ParleyUserSettings.instance.McpServers;
            if (definitions.Count == 0)
            {
                _list.Add(ParleyStyles.Text("No servers yet. Add one, then switch it on for this chat.", ParleyStyles.Muted));
            }

            foreach (McpServerDefinition definition in definitions)
            {
                _list.Add(BuildServerRow(definition));
            }

            if (_session.Profile.Kind != ProfileKind.Local)
            {
                RenderExternal();
            }

            if (_session.Backend.HasPendingRestart)
            {
                _list.Add(ParleyStyles.Text("Some changes apply with your next message: the agent restarts and resumes this chat.", "pl-mcp__hint"));
            }
        }

        private void RenderExternal()
        {
            string source = _session.Profile.Kind == ProfileKind.Codex ? "Codex" : "Claude Code";
            _list.Add(ParleyStyles.Text("From " + source + " config", "pl-mcp__section"));
            _external.Clear();
            foreach (McpServerStatus status in _session.McpStatuses)
            {
                if (status.IsExternal)
                {
                    _external.Add(status);
                }
            }

            if (_external.Count == 0)
            {
                string hint = _session.Backend.IsRunning ? "None reported." : "Shown once the chat has started.";
                _list.Add(ParleyStyles.Text(hint, ParleyStyles.Muted));
                return;
            }

            _external.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase));
            foreach (McpServerStatus status in _external)
            {
                _list.Add(BuildExternalRow(status));
            }
        }

        private VisualElement BuildServerRow(McpServerDefinition definition)
        {
            string id = definition.Id;
            bool enabled = _session.IsMcpServerEnabled(id);
            VisualElement row = new VisualElement();
            row.AddToClassList("pl-mcp__row");
            Toggle toggle = new Toggle { value = enabled, tooltip = "Use this server in this chat" };
            toggle.AddToClassList("pl-mcp__toggle");
            toggle.RegisterValueChangedCallback(evt => _session?.SetMcpServerEnabled(id, evt.newValue));
            row.Add(toggle);
            VisualElement main = new VisualElement();
            main.AddToClassList("pl-mcp__main");
            main.Add(ParleyStyles.Text(definition.Name, "pl-mcp__name"));
            Label detail = ParleyStyles.Text(Describe(definition), ParleyStyles.Muted);
            detail.AddToClassList("pl-mcp__detail");
            main.Add(detail);
            if (enabled)
            {
                McpServerStatus status = _session.FindMcpStatus(definition.Name, false)
                    ?? new McpServerStatus { Name = definition.Name, State = McpConnectionState.Pending };
                AddState(main, status);
            }

            VisualElement actions = new VisualElement();
            actions.AddToClassList("pl-mcp__actions");
            actions.Add(ParleyStyles.Button("Edit", () => OpenForm(ParleyUserSettings.instance.FindMcpServer(id)), "pl-button--small"));
            actions.Add(ParleyStyles.Button("Delete", () => Delete(id), "pl-button--small"));
            main.Add(actions);
            row.Add(main);
            return row;
        }

        private VisualElement BuildExternalRow(McpServerStatus status)
        {
            string name = status.Name;
            VisualElement row = new VisualElement();
            row.AddToClassList("pl-mcp__row");
            Toggle toggle = new Toggle { value = _session.IsExternalMcpServerEnabled(name), tooltip = "Use this server in this chat" };
            toggle.AddToClassList("pl-mcp__toggle");
            toggle.RegisterValueChangedCallback(evt => _session?.SetExternalMcpServerEnabled(name, evt.newValue));
            row.Add(toggle);
            VisualElement main = new VisualElement();
            main.AddToClassList("pl-mcp__main");
            main.Add(ParleyStyles.Text(name, "pl-mcp__name"));
            AddState(main, status);
            row.Add(main);
            return row;
        }

        private void AddState(VisualElement parent, McpServerStatus status)
        {
            VisualElement line = new VisualElement();
            line.AddToClassList("pl-mcp__state");
            VisualElement dot = new VisualElement();
            dot.AddToClassList("pl-mcp__dot");
            dot.AddToClassList(StateClass(status.State));
            line.Add(dot);
            line.Add(ParleyStyles.Text(StateText(status), "pl-mcp__state-text"));
            parent.Add(line);
            if (!string.IsNullOrEmpty(status.Error))
            {
                Label error = ParleyStyles.Text(status.Error, "pl-mcp__error");
                error.selection.isSelectable = true;
                parent.Add(error);
            }
        }

        private void OpenForm(McpServerDefinition definition)
        {
            if (_form != null)
            {
                return;
            }

            _form = new McpServerForm(definition, SaveForm, CloseForm);
            _formHost.Add(_form);
        }

        private void SaveForm(McpServerDefinition definition, bool isNew)
        {
            ParleyUserSettings.instance.SaveMcpServer(definition);
            CloseForm();
            if (isNew)
            {
                _session?.SetMcpServerEnabled(definition.Id, true);
            }
            else
            {
                _session?.ReapplyMcp();
            }
        }

        private void CloseForm()
        {
            _formHost.Clear();
            _form = null;
            _renderedKey = null;
            Refresh();
        }

        private void Delete(string id)
        {
            ParleyUserSettings settings = ParleyUserSettings.instance;
            McpServerDefinition definition = settings.FindMcpServer(id);
            if (definition == null)
            {
                return;
            }

            string message = "Delete MCP server '" + definition.Name + "' and its stored secrets? It is removed from every chat.";
            if (!EditorUtility.DisplayDialog("Delete MCP server", message, "Delete", "Cancel"))
            {
                return;
            }

            McpSecrets.DeleteAll(definition);
            settings.RemoveMcpServer(definition);
            _session?.ReapplyMcp();
            _renderedKey = null;
            Refresh();
        }

        private void RefreshStatus()
        {
            _session?.RefreshMcpStatus();
            _renderedKey = null;
            Refresh();
        }
    }
}
