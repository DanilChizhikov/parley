using DTech.Parley.Editor.Secrets;
using UnityEditor;
using UnityEngine;
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

        private static void CopySecrets(string fromProfileId, string toProfileId)
        {
            ISecretStore store = SecretStores.Default;
            foreach (string field in SecretFields.All)
            {
                if (store.TryGet(SecretStores.Key(fromProfileId, field), out string secret)
                    && !store.Set(SecretStores.Key(toProfileId, field), secret, out string error))
                {
                    Debug.LogWarning("[Parley] Could not copy secret '" + field + "': " + error);
                }
            }
        }

        private static void DeleteSecrets(string profileId)
        {
            ISecretStore store = SecretStores.Default;
            foreach (string field in SecretFields.All)
            {
                store.Delete(SecretStores.Key(profileId, field));
            }
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

        private static string UniqueName(string name)
        {
            string candidate = name;
            int index = 2;
            while (IsNameTaken(candidate))
            {
                candidate = name + " " + index++;
            }

            return candidate;
        }

        private static bool IsNameTaken(string name)
        {
            foreach (ParleyProfile profile in ParleyUserSettings.instance.Profiles)
            {
                if (profile.Name == name)
                {
                    return true;
                }
            }

            return false;
        }

        private void RebuildList()
        {
            _list.Clear();
            ParleyUserSettings settings = ParleyUserSettings.instance;
            ParleyProfile active = settings.ActiveProfile;
            foreach (ParleyProfile profile in settings.Profiles)
            {
                ParleyProfile captured = profile;
                Button item = ParleyStyles.Button(string.Empty, () => Select(captured), "pl-prefs__profile-item");
                item.EnableInClassList("pl-prefs__profile-item--selected", profile == _selected);
                VisualElement title = new VisualElement();
                title.AddToClassList("pl-prefs__profile-title");
                title.Add(ParleyStyles.Text(profile.Name, "pl-prefs__profile-name"));
                if (profile == active)
                {
                    Label badge = ParleyStyles.Text("Active", "pl-chip");
                    badge.tooltip = "Used by new chats";
                    title.Add(badge);
                }

                item.Add(title);
                item.Add(ParleyStyles.Text(ProfileLabels.Describe(profile), ParleyStyles.Muted));
                _list.Add(item);
            }

            VisualElement actions = new VisualElement();
            actions.AddToClassList("pl-prefs__profile-actions");
            Button add = ParleyStyles.Button("+ Add ▾", null, "pl-button--small");
            add.tooltip = "Add a Claude Code, Codex or local model profile";
            add.clicked += () => ShowAddMenu(add);
            actions.Add(add);
            Button duplicate = ParleyStyles.Button("Duplicate", DuplicateSelected, "pl-button--small");
            duplicate.tooltip = "Copy the selected profile with its secrets";
            actions.Add(duplicate);
            Button delete = ParleyStyles.Button("Delete", DeleteSelected, "pl-button--small");
            delete.AddToClassList("pl-button--danger");
            bool canDelete = settings.Profiles.Count > 1;
            delete.SetEnabled(canDelete);
            delete.tooltip = canDelete ? "Delete the selected profile and its secrets" : "At least one profile is required";
            actions.Add(delete);
            _list.Add(actions);
        }

        private void ShowAddMenu(VisualElement anchor)
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Claude Code"), false, () => AddProfile(ParleyProfile.CreateClaude(UniqueName("Claude Code"), ClaudeAuthMethod.ClaudeAiLogin)));
            menu.AddItem(new GUIContent("Codex"), false, () => AddProfile(ParleyProfile.CreateCodex(UniqueName("Codex"), CodexAuthMethod.CliDefault)));
            menu.AddItem(new GUIContent("Local model"), false, () => AddProfile(ParleyProfile.CreateLocal(UniqueName("Local model"), LocalPreset.LmStudio)));
            menu.DropDown(anchor.worldBound);
        }

        private void AddProfile(ParleyProfile profile)
        {
            ParleyUserSettings.instance.Add(profile);
            Select(profile);
        }

        private void DuplicateSelected()
        {
            ParleyProfile copy = _selected.Clone();
            copy.Name = UniqueName(copy.Name);
            CopySecrets(_selected.Id, copy.Id);
            AddProfile(copy);
        }

        private void DeleteSelected()
        {
            ParleyUserSettings settings = ParleyUserSettings.instance;
            if (settings.Profiles.Count <= 1)
            {
                return;
            }

            string message = "Delete profile '" + _selected.Name + "' and its stored secrets?";
            if (!EditorUtility.DisplayDialog("Delete profile", message, "Delete", "Cancel"))
            {
                return;
            }

            ParleyProfile removed = _selected;
            settings.Remove(removed);
            DeleteSecrets(removed.Id);
            Select(settings.Profiles[0]);
        }
    }
}