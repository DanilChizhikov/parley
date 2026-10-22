using System;
using System.Collections.Generic;
using DTech.Parley.Editor.Secrets;
using UnityEngine.UIElements;

namespace DTech.Parley.Editor.UI
{
    internal sealed class McpVariableList : VisualElement
    {
        private readonly List<McpVariable> _variables;
        private readonly string _secretOwner;
        private readonly string _keyPlaceholder;
        private readonly Action<McpVariable> _onSecretDiscarded;
        private readonly Action<string> _onError;
        private readonly VisualElement _rows;
        private readonly List<SecretField> _secretFields = new ();

        public McpVariableList(
            string title,
            string keyPlaceholder,
            List<McpVariable> variables,
            string secretOwner,
            Action<McpVariable> onSecretDiscarded,
            Action<string> onError)
        {
            _onError = onError;
            _variables = variables;
            _secretOwner = secretOwner;
            _keyPlaceholder = keyPlaceholder;
            _onSecretDiscarded = onSecretDiscarded;
            AddToClassList("pl-mcp__vars");
            VisualElement header = new VisualElement();
            header.AddToClassList("pl-mcp__vars-header");
            header.Add(ParleyStyles.Text(title, "pl-mcp__vars-title"));
            header.Add(ParleyStyles.Spacer());
            Button add = ParleyStyles.Button("+ Add", Add, "pl-button--small");
            add.tooltip = "Add a row";
            header.Add(add);
            Add(header);
            _rows = new VisualElement();
            Add(_rows);
            Rebuild();
        }

        public string CommitSecrets()
        {
            foreach (SecretField field in _secretFields)
            {
                if (!field.CommitPending(out string error))
                {
                    return "Could not store a secret: " + error;
                }
            }

            return null;
        }

        private void Add()
        {
            _variables.Add(new McpVariable());
            Rebuild();
        }

        private void Rebuild()
        {
            _rows.Clear();
            _secretFields.Clear();
            if (_variables.Count == 0)
            {
                _rows.Add(ParleyStyles.Text("None", ParleyStyles.Muted));
            }

            foreach (McpVariable variable in _variables)
            {
                _rows.Add(BuildRow(variable));
            }
        }

        private VisualElement BuildRow(McpVariable variable)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("pl-mcp__var");
            VisualElement fields = new VisualElement();
            fields.AddToClassList("pl-mcp__var-fields");
            TextField key = new TextField { value = variable.Key };
            key.textEdition.placeholder = _keyPlaceholder;
            key.AddToClassList("pl-mcp__var-key");
            key.RegisterValueChangedCallback(evt => variable.Key = evt.newValue);
            fields.Add(key);
            if (variable.IsSecret)
            {
                SecretField secret = new SecretField(string.Empty, _secretOwner, variable.SecretField, "value", true);
                secret.AddToClassList("pl-mcp__var-value");
                _secretFields.Add(secret);
                fields.Add(secret);
            }
            else
            {
                TextField value = new TextField { value = variable.Value };
                value.textEdition.placeholder = "value";
                value.AddToClassList("pl-mcp__var-value");
                value.RegisterValueChangedCallback(evt => variable.Value = evt.newValue);
                fields.Add(value);
            }

            Toggle secretToggle = new Toggle { text = "Secret", value = variable.IsSecret, tooltip = "Keep the value in the OS keychain instead of the preferences file" };
            secretToggle.AddToClassList("pl-mcp__var-secret");
            secretToggle.RegisterValueChangedCallback(evt => SetSecret(variable, evt.newValue));
            fields.Add(secretToggle);
            Button remove = ParleyStyles.Button("✕", () => Remove(variable), "pl-button--small");
            remove.AddToClassList("pl-button--ghost");
            remove.tooltip = "Remove this row";
            fields.Add(remove);
            row.Add(fields);
            return row;
        }

        private void SetSecret(McpVariable variable, bool secret)
        {
            if (variable.IsSecret == secret)
            {
                return;
            }

            if (secret && !string.IsNullOrEmpty(variable.Value))
            {
                if (!SecretStores.Default.Set(SecretStores.Key(_secretOwner, variable.SecretField), variable.Value, out string error))
                {
                    _onError("Could not store the secret: " + error);
                    Rebuild();
                    return;
                }

                variable.Value = string.Empty;
            }
            else if (!secret)
            {
                _onSecretDiscarded(variable);
            }

            variable.IsSecret = secret;
            Rebuild();
        }

        private void Remove(McpVariable variable)
        {
            if (variable.IsSecret)
            {
                _onSecretDiscarded(variable);
            }

            _variables.Remove(variable);
            Rebuild();
        }
    }
}
