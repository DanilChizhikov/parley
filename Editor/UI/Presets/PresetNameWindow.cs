using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DTech.Parley.Editor.UI
{
    internal sealed class PresetNameWindow : EditorWindow
    {
        private static readonly Vector2 WindowSize = new Vector2(280.0f, 112.0f);

        private string _title;
        private string _initialName;
        private string _presetId;
        private Action<string> _onSave;
        private TextField _name;
        private Label _error;

        public static void Show(Rect screenRect, string title, string initialName, string presetId, Action<string> onSave)
        {
            PresetNameWindow window = CreateInstance<PresetNameWindow>();
            window._title = title;
            window._initialName = initialName ?? string.Empty;
            window._presetId = presetId;
            window._onSave = onSave;
            window.ShowAsDropDown(screenRect, WindowSize);
        }

        private void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            ParleyStyles.Apply(root);
            root.AddToClassList("pl-mcp__form");
            root.Add(ParleyStyles.Text(_title, "pl-mcp__subtitle"));
            _name = new TextField { value = _initialName };
            _name.textEdition.placeholder = "Preset name";
            _name.RegisterCallback<KeyDownEvent>(KeyDownHandler, TrickleDown.TrickleDown);
            root.Add(_name);
            _error = ParleyStyles.Text(string.Empty, "pl-mcp__error");
            ParleyStyles.SetVisible(_error, false);
            root.Add(_error);
            VisualElement buttons = new VisualElement();
            buttons.AddToClassList("pl-mcp__form-buttons");
            buttons.Add(ParleyStyles.Spacer());
            buttons.Add(ParleyStyles.Button("Cancel", Close, "pl-button--small"));
            buttons.Add(ParleyStyles.Button("Save", Submit, "pl-button--small"));
            root.Add(buttons);
            _name.schedule.Execute(() => _name.Focus());
        }

        private void KeyDownHandler(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            {
                evt.StopPropagation();
                Submit();
            }
            else if (evt.keyCode == KeyCode.Escape)
            {
                evt.StopPropagation();
                Close();
            }
        }

        private void Submit()
        {
            string name = (_name.value ?? string.Empty).Trim();
            string error = name.Length == 0
                ? "Enter a name."
                : ParleyUserSettings.instance.IsPresetNameTaken(name, _presetId) ? "A preset with this name already exists." : null;

            if (error != null)
            {
                _error.text = error;
                ParleyStyles.SetVisible(_error, true);
                return;
            }

            Action<string> onSave = _onSave;
            Close();
            onSave?.Invoke(name);
        }
    }
}
