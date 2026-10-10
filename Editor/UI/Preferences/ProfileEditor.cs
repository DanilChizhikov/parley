using System;
using System.Threading.Tasks;
using UnityEngine.UIElements;

namespace DTech.Parley.Editor.UI
{
    internal abstract class ProfileEditor : VisualElement
    {
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
            VisualElement row = new VisualElement();
            row.AddToClassList("pl-request__buttons");
            foreach ((string label, Action action) in buttons)
            {
                row.Add(ParleyStyles.Button(label, action));
            }

            return row;
        }

        protected static TextField Text(TextFieldRequest request)
        {
            TextField field = new TextField(request.Label) { value = request.Value ?? string.Empty, isDelayed = true };
            if (!string.IsNullOrEmpty(request.Placeholder))
            {
                field.textEdition.placeholder = request.Placeholder;
            }

            Action<string> apply = request.Apply;
            field.RegisterValueChangedCallback(evt =>
            {
                apply(evt.newValue?.Trim());
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

        protected void AddFooter(VisualElement extra)
        {
            Add(Status);
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
                Status.text = ProfileLabels.Describe(Profile) + " CLI not found. Set its path above.";
                return;
            }

            Status.text = TerminalLauncher.Open(command, out string error)
                ? "Opened a terminal. Finish there, then press Check status."
                : "Could not open a terminal (" + error + "). Run it yourself: " + command;
        }

        private void Rename(string value)
        {
            Profile.Name = string.IsNullOrWhiteSpace(value) ? "Profile" : value;
            OnRenamed?.Invoke();
        }
    }
}