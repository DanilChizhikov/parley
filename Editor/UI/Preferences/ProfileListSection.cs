using UnityEditor;
using UnityEngine.UIElements;

namespace DTech.Parley.Editor.UI
{
    internal sealed class ProfileListSection : VisualElement
    {
        private readonly VisualElement _list;
        private readonly VisualElement _editor;

        private ParleyProfile _selected;

        public ProfileListSection()
        {
            AddToClassList("pl-prefs__profiles");
            _list = new VisualElement();
            _list.AddToClassList("pl-prefs__profile-list");
            _editor = new VisualElement();
            _editor.AddToClassList("pl-prefs__profile-editor");
            Add(_list);
            Add(_editor);
            Select(ParleyUserSettings.instance.ActiveProfile);
        }

        private void Select(ParleyProfile profile)
        {
            _selected = profile;
            _editor.Clear();
            ProfileEditor editor = ProfileEditor.Create(profile);
            editor.OnRenamed += RebuildList;
            _editor.Add(editor);
            RebuildList();
        }

        private void RebuildList()
        {
            _list.Clear();
            foreach (ParleyProfile profile in ParleyUserSettings.instance.Profiles)
            {
                ParleyProfile captured = profile;
                Button item = ParleyStyles.Button(profile.Name + "\n" + ProfileLabels.Describe(profile), () => Select(captured), "pl-prefs__profile-item");
                item.EnableInClassList("pl-prefs__profile-item--selected", profile == _selected);
                _list.Add(item);
            }

            VisualElement actions = new VisualElement();
            actions.AddToClassList("pl-prefs__profile-actions");
            actions.Add(ParleyStyles.Button("+ Claude", () => AddProfile(ParleyProfile.CreateClaude("Claude Code", ClaudeAuthMethod.ClaudeAiLogin)), "pl-button--small"));
            actions.Add(ParleyStyles.Button("+ Codex", () => AddProfile(ParleyProfile.CreateCodex("Codex", CodexAuthMethod.CliDefault)), "pl-button--small"));
            actions.Add(ParleyStyles.Button("+ Local", () => AddProfile(ParleyProfile.CreateLocal("Local model", LocalPreset.LmStudio)), "pl-button--small"));
            actions.Add(ParleyStyles.Button("Duplicate", () => AddProfile(_selected.Clone()), "pl-button--small"));
            actions.Add(ParleyStyles.Button("Delete", DeleteSelected, "pl-button--small"));
            _list.Add(actions);
        }

        private void AddProfile(ParleyProfile profile)
        {
            ParleyUserSettings.instance.Add(profile);
            Select(profile);
        }

        private void DeleteSelected()
        {
            ParleyUserSettings settings = ParleyUserSettings.instance;
            if (settings.Profiles.Count <= 1)
            {
                return;
            }

            string message = "Delete profile '" + _selected.Name + "'? Its stored secrets stay in the keychain until you clear them.";
            if (!EditorUtility.DisplayDialog("Delete profile", message, "Delete", "Cancel"))
            {
                return;
            }

            settings.Remove(_selected);
            Select(settings.Profiles[0]);
        }
    }
}