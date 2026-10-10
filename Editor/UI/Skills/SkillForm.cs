using System;
using DTech.Parley.Editor.Skills;
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
            AddToClassList("pl-mcp__form");
            Add(ParleyStyles.Text(original == null ? "New skill" : "Edit " + original.Name, "pl-mcp__subtitle"));
            Add(CreateTextField(new TextFieldRequest("Name", _draft.Name, value => _draft.Name = value.Trim(), "my-skill")));
            Add(CreateTextField(new TextFieldRequest("Description", _draft.Description, value => _draft.Description = value, "When the agent should use this skill")));
            Add(CreateTextField(new TextFieldRequest("Argument hint", _draft.ArgumentHint, value => _draft.ArgumentHint = value, "[level]")));
            TextField body = CreateTextField(new TextFieldRequest("Instructions", _draft.Body, value => _draft.Body = value, "Markdown instructions. $ARGUMENTS is replaced with the arguments."));
            body.multiline = true;
            body.AddToClassList("pl-skills__body");
            Add(body);
            Toggle byDefault = new Toggle("Enable in new chats") { value = _enabledByDefault };
            byDefault.RegisterValueChangedCallback(evt => _enabledByDefault = evt.newValue);
            Add(byDefault);
            _error = ParleyStyles.Text(string.Empty, "pl-mcp__error");
            ParleyStyles.SetVisible(_error, false);
            Add(_error);
            VisualElement buttons = new VisualElement();
            buttons.AddToClassList("pl-mcp__form-buttons");
            buttons.Add(ParleyStyles.Button("Save", Save, "pl-button--primary"));
            buttons.Add(ParleyStyles.Button("Cancel", () => _onClose()));
            Add(buttons);
        }

        public void ShowError(string message)
        {
            ParleyStyles.SetVisible(_error, !string.IsNullOrEmpty(message));
            _error.text = message ?? string.Empty;
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
            string error = Validate();
            ShowError(error);
            if (error == null)
            {
                _onSave(_draft, _original?.Name, _enabledByDefault);
            }
        }

        private string Validate()
        {
            if (!SkillLibrary.IsValidName(_draft.Name))
            {
                return "Name: lowercase letters, digits and '-' (up to 64 characters).";
            }

            if (_draft.Name != _original?.Name && SkillLibrary.Find(_draft.Name) != null)
            {
                return "Another skill is already called '" + _draft.Name + "'.";
            }

            if (string.IsNullOrWhiteSpace(_draft.Description))
            {
                return "Add a description: the agent reads it to decide when to load the skill.";
            }

            return string.IsNullOrWhiteSpace(_draft.Body) ? "Write the instructions the agent should follow." : null;
        }
    }
}
