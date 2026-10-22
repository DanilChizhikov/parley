using System.Threading.Tasks;
using DTech.Parley.Editor.Secrets;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DTech.Parley.Editor.UI
{
    internal sealed class SecretField : VisualElement
    {
        private const int VisibleSuffixLength = 4;
        private const string PendingClass = "pl-secret__status--pending";
        private const string MissingClass = "pl-secret__status--missing";

        private readonly string _key;
        private readonly string _label;
        private readonly string _hint;
        private readonly bool _inline;
        private readonly TextField _field;
        private readonly Label _status;

        private int _generation;

        public SecretField(string label, string profileId, string field, string hint = null, bool inline = false)
        {
            _key = SecretStores.Key(profileId, field);
            _label = label;
            _hint = hint;
            _inline = inline;
            AddToClassList("pl-secret");
            VisualElement row = new VisualElement();
            row.AddToClassList("pl-secret__row");
            _field = new TextField(label) { isPasswordField = true };
            _field.AddToClassList("pl-secret__field");
            SetPlaceholder(hint);
            row.Add(_field);
            Add(row);
            _status = ParleyStyles.Text(string.Empty, "pl-secret__status");
            if (inline)
            {
                ParleyStyles.SetVisible(_status, false);
            }
            else
            {
                Button save = ParleyStyles.Button("Save", Save, "pl-button--small");
                save.tooltip = "Store the value now (Enter or leaving the field also stores it)";
                row.Add(save);
                Button clear = ParleyStyles.Button("Clear", Clear, "pl-button--small");
                clear.tooltip = "Remove the stored value from " + SecretStores.Default.Description;
                row.Add(clear);
                _field.RegisterValueChangedCallback(_ => ShowPending());
                _field.RegisterCallback<FocusOutEvent>(_ => AutoCommit());
                _field.RegisterCallback<KeyDownEvent>(KeyDownHandler, TrickleDown.TrickleDown);
            }

            Add(_status);
            RefreshStatus();
        }

        public bool CommitPending(out string error)
        {
            error = null;
            string value = _field.value?.Trim();
            if (string.IsNullOrEmpty(value))
            {
                return true;
            }

            if (!SecretStores.Default.Set(_key, value, out error))
            {
                return false;
            }

            _field.SetValueWithoutNotify(string.Empty);
            RefreshStatus();
            return true;
        }

        private void Save()
        {
            if (string.IsNullOrEmpty(_field.value?.Trim()))
            {
                SetStatus("Enter a value first.", MissingClass);
                return;
            }

            AutoCommit();
        }

        private void AutoCommit()
        {
            if (!CommitPending(out string error))
            {
                SetStatus("Could not save: " + error, MissingClass);
            }
        }

        private void Clear()
        {
            string name = string.IsNullOrEmpty(_label) ? "this secret" : "'" + _label + "'";
            if (!EditorUtility.DisplayDialog("Clear secret", "Remove " + name + " from " + SecretStores.Default.Description + "?", "Clear", "Cancel"))
            {
                return;
            }

            SecretStores.Default.Delete(_key);
            _field.SetValueWithoutNotify(string.Empty);
            RefreshStatus();
        }

        private void ShowPending()
        {
            if (string.IsNullOrEmpty(_field.value?.Trim()))
            {
                RefreshStatus();
                return;
            }

            _generation++;
            SetStatus("Not saved yet: press Enter or Save.", PendingClass);
        }

        private void KeyDownHandler(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter)
            {
                return;
            }

            AutoCommit();
            evt.StopPropagation();
        }

        private void SetStatus(string text, string stateClass)
        {
            _status.text = text;
            _status.EnableInClassList(PendingClass, stateClass == PendingClass);
            _status.EnableInClassList(MissingClass, stateClass == MissingClass);
        }

        private void SetPlaceholder(string text)
        {
            _field.textEdition.placeholder = text ?? string.Empty;
        }

        private async void RefreshStatus()
        {
            int generation = ++_generation;
            ISecretStore store = SecretStores.Default;
            string key = _key;
            SetStatus("Checking…", null);
            string secret = await Task.Run(() => store.TryGet(key, out string value) ? value : null);
            if (generation != _generation)
            {
                return;
            }

            if (secret == null)
            {
                SetStatus("Not set", MissingClass);
                if (_inline)
                {
                    SetPlaceholder(_hint);
                }

                return;
            }

            string suffix = secret.Length > VisibleSuffixLength ? secret.Substring(secret.Length - VisibleSuffixLength) : string.Empty;
            SetStatus("Stored in " + store.Description + " (…" + suffix + ")", null);
            if (_inline)
            {
                SetPlaceholder("Stored (…" + suffix + ")");
            }
        }
    }
}
