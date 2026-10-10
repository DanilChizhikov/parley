using System.Threading.Tasks;
using DTech.Parley.Editor.Secrets;
using UnityEngine.UIElements;

namespace DTech.Parley.Editor.UI
{
    internal sealed class SecretField : VisualElement
    {
        private const int VisibleSuffixLength = 4;

        private readonly string _key;
        private readonly TextField _field;
        private readonly Label _status;

        private int _generation;

        public SecretField(string label, string profileId, string field, string hint = null)
        {
            _key = SecretStores.Key(profileId, field);
            AddToClassList("pl-secret");
            VisualElement row = new VisualElement();
            row.AddToClassList("pl-secret__row");
            _field = new TextField(label) { isPasswordField = true };
            _field.AddToClassList("pl-secret__field");
            if (!string.IsNullOrEmpty(hint))
            {
                _field.textEdition.placeholder = hint;
            }

            row.Add(_field);
            row.Add(ParleyStyles.Button("Save", Save, "pl-button--small"));
            row.Add(ParleyStyles.Button("Clear", Clear, "pl-button--small"));
            Add(row);
            _status = ParleyStyles.Text(string.Empty, "pl-secret__status");
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

            _field.value = string.Empty;
            RefreshStatus();
            return true;
        }

        private void Save()
        {
            if (string.IsNullOrEmpty(_field.value?.Trim()))
            {
                _status.text = "Enter a value first.";
                return;
            }

            if (!CommitPending(out string error))
            {
                _status.text = "Could not save: " + error;
            }
        }

        private void Clear()
        {
            SecretStores.Default.Delete(_key);
            RefreshStatus();
        }

        private async void RefreshStatus()
        {
            int generation = ++_generation;
            ISecretStore store = SecretStores.Default;
            string key = _key;
            _status.text = "Checking…";
            string secret = await Task.Run(() => store.TryGet(key, out string value) ? value : null);
            if (generation != _generation)
            {
                return;
            }

            if (secret == null)
            {
                _status.text = "Not set";
                return;
            }

            string suffix = secret.Length > VisibleSuffixLength ? secret.Substring(secret.Length - VisibleSuffixLength) : string.Empty;
            _status.text = "Stored in " + store.Description + " (…" + suffix + ")";
        }
    }
}