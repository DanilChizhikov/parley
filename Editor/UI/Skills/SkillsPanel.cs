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

        private ChatSession _session;
        private SkillForm _form;
        private TextField _newAutoName;
        private TextField _newAutoArguments;
        private string _renderedKey;
        private string _draftAutoName = string.Empty;
        private string _draftAutoArguments = string.Empty;
        private bool _libraryLoaded;

        public SkillsPanel()
        {
            AddToClassList("pl-mcp");
            VisualElement header = new VisualElement();
            header.AddToClassList("pl-mcp__header");
            header.Add(ParleyStyles.Text("Skills in this chat", "pl-mcp__title"));
            header.Add(ParleyStyles.Spacer());
            header.Add(ParleyStyles.Button("Refresh", RefreshSkills, "pl-button--small"));
            Add(header);
            _formHost = new VisualElement();
            Add(_formHost);
            _list = new VisualElement();
            Add(_list);
            _restartHint = ParleyStyles.Text("Some changes apply with your next message: the agent restarts and resumes this chat.", "pl-mcp__hint");
            ParleyStyles.SetVisible(_restartHint, false);
            Add(_restartHint);
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

        private static VisualElement CreateMain(string name, string description)
        {
            VisualElement main = new VisualElement();
            main.AddToClassList("pl-mcp__main");
            main.Add(ParleyStyles.Text(name, "pl-mcp__name"));
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
            _key.Append(_session.Record.Id).Append('|').Append(_session.Profile.Kind).Append('|').Append(_session.Backend.IsRunning).Append('|');
            foreach (AutoSkill skill in ParleyUserSettings.instance.AutoSkills)
            {
                _key.Append(skill.Name).Append(':').Append(skill.Arguments).Append(';');
            }

            _key.Append('|');
            if (_session.Record.PendingAutoSkills != null)
            {
                foreach (SkillInvocation skill in _session.Record.PendingAutoSkills)
                {
                    _key.Append(skill.Name).Append(':').Append(skill.Arguments).Append(';');
                }
            }

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

        private void Render()
        {
            _list.Clear();
            RenderAuto();
            RenderLibrary();
            RenderExternal();
        }

        private void RenderAuto()
        {
            _list.Add(ParleyStyles.Text("Auto-run in new chats", "pl-mcp__section"));
            _list.Add(ParleyStyles.Text("Each new chat runs these before its first message.", ParleyStyles.Muted));
            foreach (AutoSkill skill in ParleyUserSettings.instance.AutoSkills)
            {
                _list.Add(BuildAutoRow(skill));
            }

            VisualElement add = CreateRow();
            add.AddToClassList("pl-skills__auto-row");
            _newAutoName = new TextField { value = _draftAutoName, tooltip = "Skill name" };
            _newAutoName.textEdition.placeholder = "skill";
            _newAutoName.RegisterValueChangedCallback(evt => _draftAutoName = evt.newValue ?? string.Empty);
            _newAutoName.AddToClassList("pl-skills__auto-name");
            add.Add(_newAutoName);
            add.Add(ParleyStyles.Button("▾", ShowSkillMenu, "pl-button--small"));
            _newAutoArguments = new TextField { value = _draftAutoArguments, tooltip = "Arguments" };
            _newAutoArguments.textEdition.placeholder = "arguments";
            _newAutoArguments.RegisterValueChangedCallback(evt => _draftAutoArguments = evt.newValue ?? string.Empty);
            _newAutoArguments.AddToClassList("pl-skills__auto-args");
            add.Add(_newAutoArguments);
            add.Add(ParleyStyles.Button("Add", AddAutoSkill, "pl-button--small"));
            _list.Add(add);
            List<SkillInvocation> pending = _session.Record.PendingAutoSkills;
            if (pending == null || pending.Count == 0)
            {
                return;
            }

            _list.Add(ParleyStyles.Text("This chat, before your first message:", ParleyStyles.Muted));
            foreach (SkillInvocation skill in pending)
            {
                string name = skill.Name;
                VisualElement row = CreateRow();
                VisualElement main = CreateMain(skill.ToCommand(), _session.IsSkillEnabled(name) ? null : "Off in this chat, so it is skipped.");
                row.Add(main);
                row.Add(ParleyStyles.Button("Skip", () => _session?.SkipPendingAutoSkill(name), "pl-button--small"));
                _list.Add(row);
            }
        }

        private VisualElement BuildAutoRow(AutoSkill skill)
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
            row.Add(ParleyStyles.Button("Remove", () => RemoveAutoSkill(name), "pl-button--small"));
            return row;
        }

        private void RenderLibrary()
        {
            VisualElement header = new VisualElement();
            header.AddToClassList("pl-mcp__header");
            header.Add(ParleyStyles.Text("Parley skills", "pl-mcp__section"));
            header.Add(ParleyStyles.Spacer());
            header.Add(ParleyStyles.Button("+ New", () => OpenForm(null), "pl-button--small"));
            header.Add(ParleyStyles.Button("Import…", Import, "pl-button--small"));
            header.Add(ParleyStyles.Button("Folder", RevealLibrary, "pl-button--small"));
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
            VisualElement main = CreateMain(name, document.Description);
            VisualElement actions = new VisualElement();
            actions.AddToClassList("pl-mcp__actions");
            actions.Add(ParleyStyles.Button("Edit", () => OpenForm(SkillLibrary.Find(name)), "pl-button--small"));
            actions.Add(ParleyStyles.Button("Delete", () => Delete(name), "pl-button--small"));
            main.Add(actions);
            row.Add(main);
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

            _list.Add(ParleyStyles.Text(source, "pl-mcp__section"));
            _external.Clear();
            foreach (SkillInfo skill in _session.Skills)
            {
                if (!skill.IsLibrary)
                {
                    _external.Add(skill);
                }
            }

            if (_external.Count == 0)
            {
                string hint = _session.Backend.IsRunning ? "None found." : "Shown once the chat has started.";
                _list.Add(ParleyStyles.Text(hint, ParleyStyles.Muted));
                return;
            }

            _external.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase));
            foreach (SkillInfo skill in _external)
            {
                _list.Add(BuildExternalRow(skill));
            }
        }

        private VisualElement BuildExternalRow(SkillInfo skill)
        {
            string name = skill.Name;
            bool enabled = _session.IsExternalSkillEnabled(name);
            VisualElement row = CreateRow();
            Toggle toggle = new Toggle { value = enabled, tooltip = "Use this skill in this chat" };
            toggle.AddToClassList("pl-mcp__toggle");
            toggle.RegisterValueChangedCallback(evt => _session?.SetExternalSkillEnabled(name, evt.newValue));
            row.Add(toggle);
            row.Add(CreateMain(name, enabled ? skill.Description : "Off in this chat"));
            return row;
        }

        private void ShowSkillMenu()
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
                menu.AddItem(new UnityEngine.GUIContent(name.Replace("/", "∕")), false, () => _newAutoName.value = captured);
            }

            menu.DropDown(_newAutoName.worldBound);
        }

        private void AddAutoSkill()
        {
            string name = _draftAutoName.Trim().TrimStart('/');
            if (name.Length == 0)
            {
                return;
            }

            ParleyUserSettings.instance.SetAutoSkill(name, _draftAutoArguments);
            _draftAutoName = string.Empty;
            _draftAutoArguments = string.Empty;
            _renderedKey = null;
            Refresh();
        }

        private void RemoveAutoSkill(string name)
        {
            ParleyUserSettings.instance.RemoveAutoSkill(name);
            _renderedKey = null;
            Refresh();
        }

        private void OpenForm(SkillDocument document)
        {
            if (_form != null)
            {
                return;
            }

            _form = new SkillForm(document, SaveForm, CloseForm);
            _formHost.Add(_form);
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

            ParleyUserSettings.instance.SetLibrarySkillDefault(name, false);
            ParleyUserSettings.instance.RemoveAutoSkill(name);
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
    }
}
