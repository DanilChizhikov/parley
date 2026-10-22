using System;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine.UIElements;

namespace DTech.Parley.Editor.UI
{
    internal sealed class CliPathField : VisualElement
    {
        private readonly TextField _path;
        private readonly Label _detected;
        private readonly Button _detect;
        private readonly Func<Task<string>> _detectAsync;

        private int _generation;

        public CliPathField(
            string label,
            string value,
            Action<string> apply,
            Func<Task<string>> detect)
        {
            _detectAsync = detect;
            VisualElement row = new VisualElement();
            row.AddToClassList("pl-profile__row");
            _path = new TextField(label) { value = value ?? string.Empty, isDelayed = true };
            _path.textEdition.placeholder = "Empty = detect automatically";
            _path.RegisterValueChangedCallback(evt =>
            {
                apply(evt.newValue?.Trim());
                Detect();
            });
            row.Add(_path);
            Button browse = ParleyStyles.Button("Browse…", Browse, "pl-button--small");
            browse.tooltip = "Pick the executable";
            row.Add(browse);
            _detect = ParleyStyles.Button("Detect", Detect, "pl-button--small");
            _detect.tooltip = "Find the executable and show its version";
            row.Add(_detect);
            Add(row);
            _detected = ParleyStyles.Text(string.Empty, ParleyStyles.Muted);
            _detected.selection.isSelectable = true;
            Add(_detected);
            RegisterCallback<AttachToPanelEvent>(AttachedHandler);
        }

        private void Browse()
        {
            string current = _path.value?.Trim();
            string folder = string.IsNullOrEmpty(current) ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) : Path.GetDirectoryName(current);
            string path = EditorUtility.OpenFilePanel(_path.label, folder ?? string.Empty, string.Empty);
            if (!string.IsNullOrEmpty(path))
            {
                _path.value = path;
            }
        }

        private async void Detect()
        {
            int generation = ++_generation;
            _detected.text = "Detecting…";
            _detect.SetEnabled(false);
            string result = await _detectAsync();
            if (generation != _generation)
            {
                return;
            }

            _detected.text = result;
            _detect.SetEnabled(true);
        }

        private void AttachedHandler(AttachToPanelEvent evt)
        {
            UnregisterCallback<AttachToPanelEvent>(AttachedHandler);
            Detect();
        }
    }
}
