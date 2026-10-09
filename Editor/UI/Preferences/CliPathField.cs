using System;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine.UIElements;

namespace DTech.Parley.Editor.UI
{
    internal sealed class CliPathField : VisualElement
    {
        private readonly TextField _path;
        private readonly Label _detected;
        private readonly Func<Task<string>> _detect;

        public CliPathField(
            string label,
            string value,
            Action<string> apply,
            Func<Task<string>> detect)
        {
            _detect = detect;
            VisualElement row = new VisualElement();
            row.AddToClassList("pl-profile__row");
            _path = new TextField(label) { value = value ?? string.Empty, isDelayed = true };
            _path.textEdition.placeholder = "Empty = detect automatically";
            _path.style.flexGrow = 1.0f;
            _path.RegisterValueChangedCallback(evt => apply(evt.newValue?.Trim()));
            row.Add(_path);
            row.Add(ParleyStyles.Button("Browse…", Browse, "pl-button--small"));
            row.Add(ParleyStyles.Button("Detect", Detect, "pl-button--small"));
            Add(row);
            _detected = ParleyStyles.Text(string.Empty, ParleyStyles.Muted);
            Add(_detected);
        }

        private void Browse()
        {
            string path = EditorUtility.OpenFilePanel(_path.label, "/", string.Empty);
            if (!string.IsNullOrEmpty(path))
            {
                _path.value = path;
            }
        }

        private async void Detect()
        {
            _detected.text = "Detecting…";
            _detected.text = await _detect();
        }
    }
}