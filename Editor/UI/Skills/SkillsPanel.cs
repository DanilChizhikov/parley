using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DTech.Parley.Editor.Sessions;
using DTech.Parley.Editor.Skills;
using UnityEditor;
using UnityEngine.UIElements;

namespace DTech.Parley.Editor.UI
{
    internal sealed class SkillsPanel : VisualElement
    {
        private readonly VisualElement _formHost;
        private readonly VisualElement _list;
        private readonly Label _restartHint;
        private readonly StringBuilder _key = new ();
        private readonly List<SkillInfo> _external = new ();
        private readonly List<SkillDocument> _library = new ();
        private readonly List<string> _names = new ();
        private readonly AutoSkillDraft _chatDraft = new ();
        private readonly AutoSkillDraft _defaultsDraft = new ();

        private ChatSession _session;
        private SkillForm _form;
        private string _renderedKey;
        private bool _libraryLoaded;

        public SkillsPanel()
        {
            AddToClassList("pl-mcp");
            VisualElement header = new VisualElement();
            header.AddToClassList("pl-mcp__header");
            header.Add(ParleyStyles.Text("Skills in this chat", "pl-mcp__title"));
            header.Add(ParleyStyles.Spacer());
            Button refresh = ParleyStyles.Button("Refresh", RefreshSkills, "pl-button--small");
            refresh.tooltip = "Reload the skill list";
            header.Add(refresh);
            Add(header);
            _restartHint = ParleyStyles.Text("Some changes apply with your next message: the agent restarts and resumes this chat.", "pl-mcp__hint");
            ParleyStyles.SetVisible(_restartHint, false);
            Add(_restartHint);
            _formHost = new VisualElement();
            Add(_formHost);
            _list = new VisualElement();
            Add(_list);
        }

        public static int EnabledCount(ChatSession session)
        {
            int count = 0;
            foreach (SkillInfo skill in session.Skills)
            {
                if (session.IsSkillEnabled(skill.Name))
                {
                    count++;
                }
            }

            foreach (string name in session.Record.EnabledLibrarySkills)
            {
                if (!IsReported(session, name))
                {
                    count++;
                }
            }

            return count;
        }

        public void Bind(ChatSession session)
        {
            _session = session;
            _renderedKey = null;
            _libraryLoaded = false;
        }

        public void Refresh()
        {
            if (_session == null)
            {
                _list.Clear();
                _renderedKey = null;
                ParleyStyles.SetVisible(_restartHint, false);
                return;
            }

            ParleyStyles.SetVisible(_restartHint, _session.Backend.HasPendingRestart);

            if (!_libraryLoaded)
            {
                LoadLibrary();
            }

            string key = BuildKey();
            if (key == _renderedKey)
            {
                return;
            }

            _renderedKey = key;
            Render();
        }

        private static bool IsReported(ChatSession session, string name)
        {
            foreach (SkillInfo skill in session.Skills)
            {
                if (skill.Name == name)
                {
                    return true;
                }
            }

            return false;
        }

        private static VisualElement CreateRow()
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("pl-mcp__row");
            return row;
        }

        private static VisualElement CreateMain(string name, string description, Toggle toggle = null)
        {
            VisualElement main = new VisualElement();
            main.AddToClassList("pl-mcp__main");
            Label title = ParleyStyles.Text(name, "pl-mcp__name");
            if (toggle != null)
            {
                title.AddToClassList("pl-mcp__name--toggle");
                title.RegisterCallback<ClickEvent>(_ => toggle.value = !toggle.value);
            }

            main.Add(title);
            if (!string.IsNullOrWhiteSpace(description))
            {
                Label detail = ParleyStyles.Text(description.Trim(), ParleyStyles.Muted);
                detail.tooltip = description.Trim();
                detail.AddToClassList("pl-mcp__detail");
                detail.AddToClassList("pl-skills__description");
                main.Add(detail);
            }

            return main;
        }

        private string BuildKey()
        {
            _key.Clear();
            ParleyUserSettings settings = ParleyUserSettings.instance;
            _key.Append(_session.Record.Id).Append('|').Append(_session.Profile.Kind).Append('|').Append(_session.Backend.IsRunning).Append('|');
            _key.Append(settings.DefaultPreset?.Id).Append(':').Append(settings.DefaultPreset?.Name).Append('|');
            foreach (AutoSkill skill in settings.AutoSkills)
            {
                _key.Append(skill.Name).Append(':').Append(skill.Arguments).Append(';');
            }

            AppendInvocations(_session.Record.AutoSkills);
            AppendInvocations(_session.Record.PendingAutoSkills);

            _key.Append('|');
            foreach (SkillDocument document in _library)
            {
                _key.Append(document.Name).Append(':').Append(document.Description).Append(':').Append(_session.IsLibrarySkillEnabled(document.Name)).Append(';');
            }

            _key.Append('|');
            foreach (SkillInfo skill in _session.Skills)
            {
                _key.Append(skill.Name).Append(':').Append(skill.IsLibrary).Append(':').Append(_session.IsSkillEnabled(skill.Name)).Append(';');
            }

            return _key.ToString();
        }

        private void AppendInvocations(List<SkillInvocation> skills)
        {
            _key.Append('|');
            if (skills == null)
            {
                return;
            }

            foreach (SkillInvocation skill in skills)
            {
                _key.Append(skill.Name).Append(':').Append(skill.Arguments).Append(';');
            }
        }

        private void Render()
        {
            _list.Clear();
            RenderAuto();
            RenderLibrary();
            RenderExternal();
        }

        private void RenderAuto()
        {
            _list.Add(ParleyStyles.Text("Auto-run in this chat", "pl-mcp__section"));
            List<SkillInvocation> configured = _session.Record.AutoSkills;
            if (configured.Count == 0)
            {
                _list.Add(ParleyStyles.Text("Skills added here run before your next message.", ParleyStyles.Muted));
            }

            foreach (SkillInvocation skill in configured)
            {
                _list.Add(BuildChatAutoRow(skill));
            }

            _list.Add(BuildAddRow(_chatDraft, "Run this skill before your next message in this chat", AddChatAutoSkill));
            _list.Add(ParleyStyles.Text("Auto-run in new chats", "pl-mcp__section"));
            ChatPreset preset = ParleyUserSettings.instance.DefaultPreset;
            if (preset != null)
            {
                _list.Add(ParleyStyles.Text("New chats use the preset '" + preset.Name + "'. Change it with Preset ▾ in the toolbar.", ParleyStyles.Muted));
                return;
            }

            _list.Add(ParleyStyles.Text("Each new chat runs these before its first message.", ParleyStyles.Muted));
            foreach (AutoSkill skill in ParleyUserSettings.instance.AutoSkills)
            {
                _list.Add(BuildDefaultAutoRow(skill));
            }

            _list.Add(BuildAddRow(_defaultsDraft, "Run this skill at the start of every new chat", AddDefaultAutoSkill));
        }

        private VisualElement BuildChatAutoRow(SkillInvocation skill)
        {
            string name = skill.Name;
            bool pending = _session.IsAutoSkillPending(name);
            VisualElement row = CreateRow();
            row.AddToClassList("pl-skills__auto-row");
            string state = !_session.IsSkillEnabled(name) ? "Off in this chat, so it is skipped." : pending ? "Runs with your next message" : "Not queued";
            VisualElement main = CreateMain("/" + name, state);
            TextField arguments = new TextField { value = skill.Arguments ?? string.Empty, isDelayed = true, tooltip = "Arguments" };
            arguments.textEdition.placeholder = "arguments";
            arguments.AddToClassList("pl-skills__auto-args");
            arguments.RegisterValueChangedCallback(evt => _session?.SetAutoSkill(name, evt.newValue, false));
            row.Add(main);
            row.Add(arguments);
            if (pending)
            {
                Button skip = ParleyStyles.Button("Skip", () => _session?.SkipPendingAutoSkill(name), "pl-button--small");
                skip.tooltip = "Don't run it this time, keep it in the list";
                row.Add(skip);
            }

            Button remove = ParleyStyles.Button("Remove", () => _session?.RemoveAutoSkill(name), "pl-button--small");
            remove.tooltip = "Remove it from this chat";
            row.Add(remove);
            return row;
        }

        private VisualElement BuildDefaultAutoRow(AutoSkill skill)
        {
            string name = skill.Name;
            VisualElement row = CreateRow();
            row.AddToClassList("pl-skills__auto-row");
            row.Add(ParleyStyles.Text("/" + name, "pl-mcp__name"));
            TextField arguments = new TextField { value = skill.Arguments ?? string.Empty, isDelayed = true, tooltip = "Arguments" };
            arguments.textEdition.placeholder = "arguments";
            arguments.AddToClassList("pl-skills__auto-args");
            arguments.RegisterValueChangedCallback(evt => ParleyUserSettings.instance.SetAutoSkill(name, evt.newValue));
            row.Add(arguments);
            Button remove = ParleyStyles.Button("Remove", () => RemoveDefaultAutoSkill(name), "pl-button--small");
            remove.tooltip = "Stop running it in new chats";
            row.Add(remove);
            return row;
        }

        private VisualElement BuildAddRow(AutoSkillDraft draft, string tooltip, Action<AutoSkillDraft> onAdd)
        {
            VisualElement add = CreateRow();
            add.AddToClassList("pl-skills__auto-row");
            Button addButton = ParleyStyles.Button("Add", () => onAdd(draft), "pl-button--small");
            addButton.tooltip = tooltip;
            addButton.SetEnabled(draft.SkillName.Length > 0);
            TextField name = new TextField { value = draft.Name, tooltip = "Skill name" };
            name.textEdition.placeholder = "skill";
            name.RegisterValueChangedCallback(evt =>
            {
                draft.Name = evt.newValue ?? string.Empty;
                addButton.SetEnabled(draft.SkillName.Length > 0);
            });
            name.AddToClassList("pl-skills__auto-name");
            add.Add(name);
            Button pick = ParleyStyles.Button("▾", () => ShowSkillMenu(name), "pl-button--small");
            pick.AddToClassList("pl-skills__pick");
            pick.tooltip = "Pick a skill";
            add.Add(pick);
            TextField arguments = new TextField { value = draft.Arguments, tooltip = "Arguments" };
            arguments.textEdition.placeholder = "arguments";
            arguments.RegisterValueChangedCallback(evt => draft.Arguments = evt.newValue ?? string.Empty);
            arguments.AddToClassList("pl-skills__auto-args");
            add.Add(arguments);
            add.Add(addButton);
            return add;
        }

        private void RenderLibrary()
        {
            VisualElement header = new VisualElement();
            header.AddToClassList("pl-mcp__header");
            header.Add(ParleyStyles.Text("Parley skills", "pl-mcp__section"));
            header.Add(ParleyStyles.Spacer());
            if (_library.Count > 0)
            {
                _names.Clear();
                int enabled = 0;
                foreach (SkillDocument document in _library)
                {
                    _names.Add(document.Name);
                    enabled += _session.IsLibrarySkillEnabled(document.Name) ? 1 : 0;
                }

                List<string> names = new (_names);
                header.Add(ParleyStyles.AllToggle(enabled, names.Count, value => _session?.SetLibrarySkillsEnabled(names, value)));
            }

            Button create = ParleyStyles.Button("+ Add", () => OpenForm(null), "pl-button--small");
            create.tooltip = "Write a new skill";
            header.Add(create);
            Button import = ParleyStyles.Button("Import…", Import, "pl-button--small");
            import.tooltip = "Copy a folder with a SKILL.md into the library";
            header.Add(import);
            Button reveal = ParleyStyles.Button("Open folder", RevealLibrary, "pl-button--small");
            reveal.tooltip = "Show the skill library folder";
            header.Add(reveal);
            _list.Add(header);
            if (_library.Count == 0)
            {
                _list.Add(ParleyStyles.Text("No skills yet. Create one or import a folder with a SKILL.md, then switch it on for this chat.", ParleyStyles.Muted));
                return;
            }

            foreach (SkillDocument document in _library)
            {
                _list.Add(BuildLibraryRow(document));
            }
        }

        private VisualElement BuildLibraryRow(SkillDocument document)
        {
            string name = document.Name;
            VisualElement row = CreateRow();
            Toggle toggle = new Toggle { value = _session.IsLibrarySkillEnabled(name), tooltip = "Use this skill in this chat" };
            toggle.AddToClassList("pl-mcp__toggle");
            toggle.RegisterValueChangedCallback(evt => _session?.SetLibrarySkillEnabled(name, evt.newValue));
            row.Add(toggle);
            row.Add(CreateMain(name, document.Description, toggle));
            row.Add(ParleyStyles.RowActions(() => OpenForm(SkillLibrary.Find(name)), () => Delete(name), "skill"));
            return row;
        }

        private void RenderExternal()
        {
            string source = _session.Profile.Kind switch
            {
                ProfileKind.ClaudeCode => "From Claude Code",
                ProfileKind.Codex => "From Codex",
                _ => "From skill folders",
            };

            _external.Clear();
            _names.Clear();
            int enabled = 0;
            foreach (SkillInfo skill in _session.Skills)
            {
                if (!skill.IsLibrary)
                {
                    _external.Add(skill);
                    if (!_names.Contains(skill.Name))
                    {
                        _names.Add(skill.Name);
                        enabled += _session.IsExternalSkillEnabled(skill.Name) ? 1 : 0;
                    }
                }
            }

            VisualElement header = new VisualElement();
            header.AddToClassList("pl-mcp__header");
            header.Add(ParleyStyles.Text(source, "pl-mcp__section"));
            header.Add(ParleyStyles.Spacer());
            if (_names.Count > 0)
            {
                List<string> names = new (_names);
                header.Add(ParleyStyles.AllToggle(enabled, names.Count, value => _session?.SetExternalSkillsEnabled(names, value)));
            }

            _list.Add(header);
            if (_external.Count == 0)
            {
                string hint = _session.Backend.IsRunning ? "None found." : "Shown once the chat has started.";
                _list.Add(ParleyStyles.Text(hint, ParleyStyles.Muted));
                return;
            }

            _external.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase));
            for (int i = 0; i < _external.Count; i++)
            {
                bool duplicate = (i > 0 && _external[i - 1].Name == _external[i].Name) || (i + 1 < _external.Count && _external[i + 1].Name == _external[i].Name);
                _list.Add(BuildExternalRow(_external[i], duplicate));
            }
        }

        private VisualElement BuildExternalRow(SkillInfo skill, bool duplicate)
        {
            string name = skill.Name;
            bool enabled = _session.IsExternalSkillEnabled(name);
            VisualElement row = CreateRow();
            Toggle toggle = new Toggle { value = enabled, tooltip = "Use this skill in this chat" };
            toggle.AddToClassList("pl-mcp__toggle");
            toggle.RegisterValueChangedCallback(evt => _session?.SetExternalSkillEnabled(name, evt.newValue));
            row.Add(toggle);
            VisualElement main = CreateMain(name, skill.Description, toggle);
            if (duplicate && !string.IsNullOrEmpty(skill.Path))
            {
                Label source = ParleyStyles.Text(skill.Path, "pl-mcp__source");
                source.tooltip = skill.Path;
                main.Add(source);
            }

            if (!enabled)
            {
                main.Add(ParleyStyles.Text("Off in this chat", "pl-mcp__state-text"));
            }

            row.Add(main);
            return row;
        }

        private void ShowSkillMenu(TextField target)
        {
            GenericMenu menu = new GenericMenu();
            List<string> names = new ();
            foreach (SkillDocument document in _library)
            {
                names.Add(document.Name);
            }

            foreach (SkillInfo skill in _session.Skills)
            {
                if (!names.Contains(skill.Name))
                {
                    names.Add(skill.Name);
                }
            }

            names.Sort(StringComparer.OrdinalIgnoreCase);
            if (names.Count == 0)
            {
                menu.AddDisabledItem(new UnityEngine.GUIContent("No skills found yet"));
            }

            foreach (string name in names)
            {
                string captured = name;
                menu.AddItem(new UnityEngine.GUIContent(name.Replace("/", "∕")), false, () => target.value = captured);
            }

            menu.DropDown(target.worldBound);
        }

        private void AddChatAutoSkill(AutoSkillDraft draft)
        {
            if (draft.SkillName.Length == 0)
            {
                return;
            }

            _session?.SetAutoSkill(draft.SkillName, draft.Arguments, true);
            draft.Clear();
            _renderedKey = null;
            Refresh();
        }

        private void AddDefaultAutoSkill(AutoSkillDraft draft)
        {
            if (draft.SkillName.Length == 0)
            {
                return;
            }

            ParleyUserSettings.instance.SetAutoSkill(draft.SkillName, draft.Arguments);
            draft.Clear();
            _renderedKey = null;
            Refresh();
        }

        private void RemoveDefaultAutoSkill(string name)
        {
            ParleyUserSettings.instance.RemoveAutoSkill(name);
            _renderedKey = null;
            Refresh();
        }

        private void OpenForm(SkillDocument document)
        {
            if (_form != null && !_form.ConfirmDiscard())
            {
                ParleyStyles.ScrollIntoView(_form);
                return;
            }

            _formHost.Clear();
            _form = new SkillForm(document, SaveForm, CloseForm);
            _formHost.Add(_form);
            ParleyStyles.ScrollIntoView(_form);
        }

        private void SaveForm(SkillDocument document, string previousName, bool enabledByDefault)
        {
            try
            {
                SkillLibrary.Save(document, previousName);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException)
            {
                _form?.ShowError(exception.Message);
                return;
            }

            ParleyUserSettings settings = ParleyUserSettings.instance;
            settings.RenameLibrarySkill(previousName, document.Name);
            settings.SetLibrarySkillDefault(document.Name, enabledByDefault);
            CloseForm();
            if (previousName == null)
            {
                _session?.SetLibrarySkillEnabled(document.Name, true);
            }
            else if (previousName != document.Name)
            {
                _session?.RenameLibrarySkill(previousName, document.Name);
            }
            else
            {
                _session?.ReapplySkills();
            }
        }

        private void CloseForm()
        {
            _formHost.Clear();
            _form = null;
            ReloadLibrary();
        }

        private void Import()
        {
            string folder = EditorUtility.OpenFolderPanel("Import skill folder", ProjectPaths.HomeFolder, string.Empty);
            if (string.IsNullOrEmpty(folder))
            {
                return;
            }

            string name;
            try
            {
                name = SkillLibrary.Import(folder, out string error);
                if (name == null)
                {
                    EditorUtility.DisplayDialog("Import skill", error, "OK");
                    return;
                }
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                EditorUtility.DisplayDialog("Import skill", "Could not copy the skill: " + exception.Message, "OK");
                return;
            }

            ReloadLibrary();
            _session?.SetLibrarySkillEnabled(name, true);
        }

        private void RevealLibrary()
        {
            Directory.CreateDirectory(SkillLibrary.Folder);
            EditorUtility.RevealInFinder(SkillLibrary.Folder);
        }

        private void Delete(string name)
        {
            string message = "Delete skill '" + name + "' and its folder? It is removed from every chat.";
            if (!EditorUtility.DisplayDialog("Delete skill", message, "Delete", "Cancel"))
            {
                return;
            }

            try
            {
                SkillLibrary.Delete(name);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                EditorUtility.DisplayDialog("Delete skill", "Could not delete the skill: " + exception.Message, "OK");
                return;
            }

            ParleyUserSettings.instance.RemoveLibrarySkill(name);
            _session?.RemoveAutoSkill(name);
            ReloadLibrary();
            _session?.ReapplySkills();
        }

        private void LoadLibrary()
        {
            _library.Clear();
            _library.AddRange(SkillLibrary.List());
            _libraryLoaded = true;
            _renderedKey = null;
        }

        private void ReloadLibrary()
        {
            LoadLibrary();
            Refresh();
        }

        private void RefreshSkills()
        {
            _session?.RefreshSkills();
            ReloadLibrary();
        }

        private sealed class AutoSkillDraft
        {
            public string Name { get; set; } = string.Empty;

            public string Arguments { get; set; } = string.Empty;

            public string SkillName => Name.Trim().TrimStart('/');

            public void Clear()
            {
                Name = string.Empty;
                Arguments = string.Empty;
            }
        }
    }
}
