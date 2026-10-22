using System;
using DTech.Parley.Editor.Skills;
using UnityEditor;
using UnityEngine.UIElements;

namespace DTech.Parley.Editor.UI
{
    internal sealed class SkillForm : VisualElement
    {
        private readonly SkillDocument _original;
        private readonly SkillDocument _draft;
        private readonly Action<SkillDocument, string, bool> _onSave;
        private readonly Action _onClose;
        private readonly Label _error;
        private readonly TextField _name;
        private readonly TextField _description;
        private readonly TextField _body;
        private readonly bool _originalEnabledByDefault;

        private bool _enabledByDefault;

        public SkillForm(SkillDocument original, Action<SkillDocument, string, bool> onSave, Action onClose)
        {
            _original = original;
            _draft = new SkillDocument
            {
                Name = original?.Name ?? string.Empty,
                Description = original?.Description ?? string.Empty,
                ArgumentHint = original?.ArgumentHint ?? string.Empty,
                Body = original?.Body ?? string.Empty,
                ExtraFrontmatter = original?.ExtraFrontmatter ?? string.Empty,
            };

            _onSave = onSave;
            _onClose = onClose;
            _enabledByDefault = original != null && ParleyUserSettings.instance.IsLibrarySkillDefault(original.Name);
            _originalEnabledByDefault = _enabledByDefault;
            AddToClassList("pl-mcp__form");
            Add(ParleyStyles.Text(original == null ? "New skill" : "Edit " + original.Name, "pl-mcp__subtitle"));
            _name = CreateTextField(new TextFieldRequest("Name", _draft.Name, value => _draft.Name = value.Trim(), "my-skill"));
            Add(_name);
            Add(ParleyStyles.Text("Lowercase letters, digits and '-'. You run it as /name.", "pl-mcp__field-hint"));
            _description = CreateTextField(new TextFieldRequest("Description", _draft.Description, value => _draft.Description = value, "When the agent should use this skill"));
            Add(_description);
            TextField hint = CreateTextField(new TextFieldRequest("Argument hint", _draft.ArgumentHint, value => _draft.ArgumentHint = value, "[level]"));
            hint.tooltip = "Shown after the name in the / menu to say what arguments the skill takes, e.g. [level] or <file>";
            Add(hint);
            _body = CreateTextField(new TextFieldRequest("Instructions", _draft.Body, value => _draft.Body = value, "Markdown instructions. $ARGUMENTS is replaced with the arguments."));
            _body.multiline = true;
            _body.verticalScrollerVisibility = ScrollerVisibility.Auto;
            _body.AddToClassList("pl-skills__body");
            Add(_body);
            Toggle byDefault = new Toggle("Enable in new chats") { value = _enabledByDefault };
            byDefault.RegisterValueChangedCallback(evt => _enabledByDefault = evt.newValue);
            Add(byDefault);
            _error = ParleyStyles.Text(string.Empty, "pl-mcp__error");
            ParleyStyles.SetVisible(_error, false);
            Add(_error);
            VisualElement buttons = new VisualElement();
            buttons.AddToClassList("pl-mcp__form-buttons");
            buttons.Add(ParleyStyles.Button("Cancel", Cancel));
            buttons.Add(ParleyStyles.Button("Save", Save, "pl-button--primary"));
            Add(buttons);
        }

        public void ShowError(string message)
        {
            ParleyStyles.SetVisible(_error, !string.IsNullOrEmpty(message));
            _error.text = message ?? string.Empty;
        }

        public bool ConfirmDiscard()
        {
            if (!IsDirty())
            {
                return true;
            }

            return EditorUtility.DisplayDialog("Discard changes", "Discard your changes to this skill?", "Discard", "Keep editing");
        }

        private bool IsDirty()
        {
            return _draft.Name != (_original?.Name ?? string.Empty)
                || _draft.Description != (_original?.Description ?? string.Empty)
                || _draft.ArgumentHint != (_original?.ArgumentHint ?? string.Empty)
                || _draft.Body != (_original?.Body ?? string.Empty)
                || _enabledByDefault != _originalEnabledByDefault;
        }

        private void Cancel()
        {
            if (ConfirmDiscard())
            {
                _onClose();
            }
        }

        private void MarkInvalid(TextField field)
        {
            _name.EnableInClassList("pl-field--invalid", field == _name);
            _description.EnableInClassList("pl-field--invalid", field == _description);
            _body.EnableInClassList("pl-field--invalid", field == _body);
            field?.Focus();
        }

        private static TextField CreateTextField(TextFieldRequest request)
        {
            TextField field = new TextField(request.Label) { value = request.Value ?? string.Empty };
            field.textEdition.placeholder = request.Placeholder;
            Action<string> apply = request.Apply;
            field.RegisterValueChangedCallback(evt => apply(evt.newValue ?? string.Empty));
            return field;
        }

        private void Save()
        {
            string error = Validate(out TextField invalid);
            ShowError(error);
            MarkInvalid(invalid);
            if (error == null)
            {
                _onSave(_draft, _original?.Name, _enabledByDefault);
            }
        }

        private string Validate(out TextField invalid)
        {
            invalid = _name;
            if (!SkillLibrary.IsValidName(_draft.Name))
            {
                return "Name: lowercase letters, digits and '-' (up to 64 characters).";
            }

            if (_draft.Name != _original?.Name && SkillLibrary.Find(_draft.Name) != null)
            {
                return "Another skill is already called '" + _draft.Name + "'.";
            }

            invalid = _description;
            if (string.IsNullOrWhiteSpace(_draft.Description))
            {
                return "Add a description: the agent reads it to decide when to load the skill.";
            }

            invalid = _body;
            if (string.IsNullOrWhiteSpace(_draft.Body))
            {
                return "Write the instructions the agent should follow.";
            }

            invalid = null;
            return null;
        }
    }
}
