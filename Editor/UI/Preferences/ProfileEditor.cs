using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine.UIElements;

namespace DTech.Parley.Editor.UI
{
    internal abstract class ProfileEditor : VisualElement
    {
        private const string SuccessClass = "pl-profile__status--success";
        private const string ErrorClass = "pl-profile__status--error";
        private const string DefaultChoice = "Default";

        public event Action OnRenamed;

        protected ParleyProfile Profile { get; }
        protected Label Status { get; }

        protected ProfileEditor(ParleyProfile profile, string description)
        {
            Profile = profile;
            AddToClassList("pl-profile");
            Add(Text(new TextFieldRequest("Name", profile.Name, Rename)));
            Add(ParleyStyles.Text(description, ParleyStyles.Muted));
            Status = ParleyStyles.Text(string.Empty, "pl-profile__status");
            Status.selection.isSelectable = true;
        }

        public static ProfileEditor Create(ParleyProfile profile)
        {
            return profile.Kind switch
            {
                ProfileKind.ClaudeCode => new ClaudeProfileEditor(profile),
                ProfileKind.Codex => new CodexProfileEditor(profile),
                _ => new LocalProfileEditor(profile),
            };
        }

        protected static void Persist()
        {
            ParleyUserSettings.instance.MarkDirty();
        }

        protected static VisualElement ButtonRow(params (string label, Action action)[] buttons)
        {
            List<Button> created = new List<Button>();
            foreach ((string label, Action action) in buttons)
            {
                created.Add(SmallButton(label, action));
            }

            return ButtonRow(created.ToArray());
        }

        protected static VisualElement ButtonRow(params Button[] buttons)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("pl-profile__buttons");
            foreach (Button button in buttons)
            {
                row.Add(button);
            }

            return row;
        }

        protected static Button SmallButton(string label, Action action, string tooltip = null, bool primary = false)
        {
            Button button = ParleyStyles.Button(label, action, "pl-button--small");
            button.EnableInClassList("pl-button--primary", primary);
            if (tooltip != null)
            {
                button.tooltip = tooltip;
            }

            return button;
        }

        protected static TextField Text(TextFieldRequest request)
        {
            TextField field = new TextField(request.Label) { value = request.Value ?? string.Empty, isDelayed = true, multiline = request.Multiline };
            if (request.Multiline)
            {
                field.AddToClassList("pl-prefs__multiline");
            }

            if (!string.IsNullOrEmpty(request.Placeholder))
            {
                field.textEdition.placeholder = request.Placeholder;
            }

            if (!string.IsNullOrEmpty(request.Tooltip))
            {
                field.tooltip = request.Tooltip;
            }

            Action<string> apply = request.Apply;
            field.RegisterValueChangedCallback(evt =>
            {
                apply(evt.newValue?.Trim());
                Persist();
            });

            return field;
        }

        protected static DropdownField Choice(string label, string value, string[] options, Action<string> apply, string tooltip)
        {
            List<string> choices = new List<string> { string.Empty };
            choices.AddRange(options);
            string current = value ?? string.Empty;
            if (!choices.Contains(current))
            {
                choices.Add(current);
            }

            DropdownField field = new DropdownField(label, choices, choices.IndexOf(current), FormatChoice, FormatChoice) { tooltip = tooltip };
            field.RegisterValueChangedCallback(evt =>
            {
                apply(evt.newValue);
                Persist();
            });

            return field;
        }

        protected static Toggle BoolField(string label, bool value, Action<bool> apply)
        {
            Toggle toggle = new Toggle(label) { value = value };
            toggle.RegisterValueChangedCallback(evt =>
            {
                apply(evt.newValue);
                Persist();
            });

            return toggle;
        }

        protected static Foldout Advanced(params VisualElement[] fields)
        {
            Foldout foldout = new Foldout { text = "Advanced", value = false };
            foreach (VisualElement field in fields)
            {
                foldout.Add(field);
            }

            return foldout;
        }

        protected void SetStatus(string text, bool? success = null)
        {
            Status.text = text;
            Status.EnableInClassList(SuccessClass, success == true);
            Status.EnableInClassList(ErrorClass, success == false);
        }

        protected async void RunBusy(Func<Task> work)
        {
            List<Button> buttons = this.Query<Button>().ToList();
            foreach (Button button in buttons)
            {
                button.SetEnabled(false);
            }

            try
            {
                await work();
            }
            finally
            {
                foreach (Button button in buttons)
                {
                    button.SetEnabled(true);
                }
            }
        }

        protected void AddFooter(VisualElement extra)
        {
            if (Status.parent == null)
            {
                Add(Status);
            }

            if (extra != null)
            {
                Add(extra);
            }

            Add(ParleyStyles.Text("Changes apply to chats started after this point (press New in the Parley window).", ParleyStyles.Muted));
        }

        protected async void OpenTerminal(Task<string> buildCommand)
        {
            string command = await buildCommand;
            if (command == null)
            {
                SetStatus(ProfileLabels.Describe(Profile) + " CLI not found. Set its path above.", false);
                return;
            }

            if (TerminalLauncher.Open(command, out string error))
            {
                SetStatus("Opened a terminal. Finish there, then press Check status.");
                return;
            }

            SetStatus("Could not open a terminal (" + error + "). Run it yourself: " + command, false);
        }

        private static string FormatChoice(string value)
        {
            return string.IsNullOrEmpty(value) ? DefaultChoice : value;
        }

        private void Rename(string value)
        {
            Profile.Name = string.IsNullOrWhiteSpace(value) ? "Profile" : value;
            OnRenamed?.Invoke();
        }
    }
}
