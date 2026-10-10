using System;
using System.Collections.Generic;
using DTech.Parley.Editor.Mcp;
using DTech.Parley.Editor.Secrets;
using UnityEngine.UIElements;

namespace DTech.Parley.Editor.UI
{
    internal sealed class McpServerForm : VisualElement
    {
        private readonly McpServerDefinition _original;
        private readonly McpServerDefinition _draft;
        private readonly Action<McpServerDefinition, bool> _onSave;
        private readonly Action _onClose;
        private readonly VisualElement _stdio;
        private readonly VisualElement _http;
        private readonly McpVariableList _environment;
        private readonly McpVariableList _headers;
        private readonly Label _error;

        public McpServerForm(McpServerDefinition original, Action<McpServerDefinition, bool> onSave, Action onClose)
        {
            _original = original;
            _draft = original?.Clone() ?? new McpServerDefinition();
            _onSave = onSave;
            _onClose = onClose;
            AddToClassList("pl-mcp__form");
            Add(ParleyStyles.Text(original == null ? "New MCP server" : "Edit " + original.Name, "pl-mcp__subtitle"));
            Add(CreateTextField(new TextFieldRequest("Name", _draft.Name, value => _draft.Name = value.Trim(), "my-server")));
            EnumField transport = new EnumField("Transport", _draft.Transport);
            transport.RegisterValueChangedCallback(TransportChangedHandler);
            Add(transport);
            _stdio = new VisualElement();
            _stdio.Add(CreateTextField(new TextFieldRequest("Command", _draft.Command, value => _draft.Command = value, "npx")));
            _stdio.Add(CreateTextField(new TextFieldRequest("Arguments", _draft.Arguments, value => _draft.Arguments = value, "-y @modelcontextprotocol/server-everything")));
            _environment = new McpVariableList("Environment variables", "NAME", _draft.Environment, _draft.SecretOwner, DiscardSecret);
            _stdio.Add(_environment);
            Add(_stdio);
            _http = new VisualElement();
            _http.Add(CreateTextField(new TextFieldRequest("URL", _draft.Url, value => _draft.Url = value, "https://example.com/mcp")));
            _headers = new McpVariableList("Headers", "Header-Name", _draft.Headers, _draft.SecretOwner, DiscardSecret);
            _http.Add(_headers);
            Add(_http);
            Toggle byDefault = new Toggle("Enable in new chats") { value = _draft.EnabledByDefault };
            byDefault.RegisterValueChangedCallback(evt => _draft.EnabledByDefault = evt.newValue);
            Add(byDefault);
            _error = ParleyStyles.Text(string.Empty, "pl-mcp__error");
            ParleyStyles.SetVisible(_error, false);
            Add(_error);
            VisualElement buttons = new VisualElement();
            buttons.AddToClassList("pl-mcp__form-buttons");
            buttons.Add(ParleyStyles.Button("Save", Save, "pl-button--primary"));
            buttons.Add(ParleyStyles.Button("Cancel", Cancel));
            Add(buttons);
            UpdateTransport();
        }

        private static TextField CreateTextField(TextFieldRequest request)
        {
            TextField field = new TextField(request.Label) { value = request.Value ?? string.Empty };
            field.textEdition.placeholder = request.Placeholder;
            Action<string> apply = request.Apply;
            field.RegisterValueChangedCallback(evt => apply(evt.newValue ?? string.Empty));
            return field;
        }

        private static string ValidateVariables(List<McpVariable> variables)
        {
            HashSet<string> keys = new (StringComparer.OrdinalIgnoreCase);
            foreach (McpVariable variable in variables)
            {
                string key = (variable.Key ?? string.Empty).Trim();
                if (key.Length == 0)
                {
                    return "Every variable and header needs a name.";
                }

                if (!keys.Add(key))
                {
                    return "'" + key + "' is listed twice.";
                }
            }

            return null;
        }

        private void Save()
        {
            string error = Validate() ?? (_draft.Transport == McpTransport.Stdio ? _environment : _headers).CommitSecrets();
            ParleyStyles.SetVisible(_error, error != null);
            _error.text = error ?? string.Empty;
            if (error != null)
            {
                return;
            }

            if (_original != null)
            {
                McpSecrets.DeleteMissing(_original, _draft);
            }

            _onSave(_draft, _original == null);
        }

        private void Cancel()
        {
            McpSecrets.DeleteMissing(_draft, _original);
            _onClose();
        }

        private string Validate()
        {
            if (!McpServerResolver.IsValidName(_draft.Name))
            {
                return "Name: use letters, digits, '-' and '_' (up to 48 characters); 'unity' is reserved.";
            }

            if (ParleyUserSettings.instance.IsMcpServerNameTaken(_draft.Name, _draft.Id))
            {
                return "Another server is already called '" + _draft.Name + "'.";
            }

            if (_draft.Transport == McpTransport.Stdio)
            {
                return string.IsNullOrWhiteSpace(_draft.Command) ? "Enter the command that starts the server." : ValidateVariables(_draft.Environment);
            }

            bool validUrl = Uri.TryCreate((_draft.Url ?? string.Empty).Trim(), UriKind.Absolute, out Uri uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
            return validUrl ? ValidateVariables(_draft.Headers) : "Enter an http(s) URL.";
        }

        private void DiscardSecret(McpVariable variable)
        {
            if (!IsOriginalSecret(variable.Id))
            {
                SecretStores.Default.Delete(SecretStores.Key(_draft.SecretOwner, variable.SecretField));
            }
        }

        private bool IsOriginalSecret(string variableId)
        {
            if (_original == null)
            {
                return false;
            }

            foreach (McpVariable variable in _original.Environment)
            {
                if (variable.Id == variableId && variable.IsSecret)
                {
                    return true;
                }
            }

            foreach (McpVariable variable in _original.Headers)
            {
                if (variable.Id == variableId && variable.IsSecret)
                {
                    return true;
                }
            }

            return false;
        }

        private void UpdateTransport()
        {
            ParleyStyles.SetVisible(_stdio, _draft.Transport == McpTransport.Stdio);
            ParleyStyles.SetVisible(_http, _draft.Transport == McpTransport.Http);
        }

        private void TransportChangedHandler(ChangeEvent<Enum> evt)
        {
            _draft.Transport = (McpTransport)evt.newValue;
            UpdateTransport();
        }
    }
}
