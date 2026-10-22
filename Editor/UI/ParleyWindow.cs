using System;
using System.Collections.Generic;
using System.Globalization;
using DTech.Parley.Editor.Agents.Preview;
using DTech.Parley.Editor.Panels;
using DTech.Parley.Editor.Sessions;
using DTech.Parley.Editor.Updates;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DTech.Parley.Editor.UI
{
    internal sealed class ParleyWindow : EditorWindow
    {
        public const string PreferencesPath = "Preferences/DTech/Parley";

        private const string MenuPath = "Window/DTech/Parley";
        private const string PreviewMenuPath = "Window/DTech/Parley Debug Preview";
        private const string WindowTitle = "Parley";
        private const string PreviewTitle = "Parley Preview";
        private const long StateRefreshMs = 100;
        private const int MaxHistoryItems = 30;
        private const double NotificationSeconds = 2.0;
        private const string UpdateDialogTitle = "Update Parley";
        private const string UpdateAvailableText = "Update available ▾";
        private const string ShrinkClass = "pl-toolbar__shrink";
        private static readonly Vector2 MinWindowSize = new Vector2(360.0f, 320.0f);

        private static readonly PermissionMode[] _claudeModes =
        {
            PermissionMode.Default,
            PermissionMode.AcceptEdits,
            PermissionMode.Plan,
            PermissionMode.Auto,
            PermissionMode.BypassPermissions,
            PermissionMode.DontAsk,
        };

        private static readonly PermissionMode[] _agentModes =
        {
            PermissionMode.Default,
            PermissionMode.AcceptEdits,
            PermissionMode.Plan,
            PermissionMode.BypassPermissions,
            PermissionMode.DontAsk,
        };

        private ChatSession _session;
        private TranscriptView _transcript;
        private SidePanel _side;
        private Composer _composer;
        private AuthCard _authCard;
        private TrustCard _trustCard;
        private StatusBar _statusBar;
        private TwoPaneSplitView _split;
        private ToolbarButton _profileButton;
        private ToolbarButton _modelButton;
        private ToolbarButton _modeButton;
        private ToolbarButton _effortButton;
        private ToolbarButton _mcpButton;
        private ToolbarButton _skillsButton;
        private ToolbarButton _presetButton;
        private ToolbarButton _updateButton;
        private ToolbarButton _historyButton;
        private ToolbarToggle _sideToggle;
        private ToolbarButton _previewButton;
        private bool _stateDirty = true;

        private static bool _openingPreview;

        [SerializeField] private bool _preview;
        [SerializeField] private ProfileKind _previewKind = ProfileKind.ClaudeCode;

        [MenuItem(MenuPath)]
        public static void Open()
        {
            ParleyWindow window = Find(false);
            if (window == null)
            {
                window = Find(true) == null ? GetWindow<ParleyWindow>() : CreateWindow<ParleyWindow>();
            }

            window.titleContent = new GUIContent(WindowTitle);
            window.Show();
            window.Focus();
            window._composer?.FocusInput();
        }

        [MenuItem(PreviewMenuPath)]
        public static void OpenPreview()
        {
            ParleyWindow window = Find(true);
            if (window == null)
            {
                _openingPreview = true;
                try
                {
                    window = CreateWindow<ParleyWindow>();
                }
                finally
                {
                    _openingPreview = false;
                }
            }

            window.Show();
            window.Focus();
            window._composer?.FocusInput();
        }

        public static void OpenPreferences()
        {
            SettingsService.OpenUserPreferences(PreferencesPath);
        }

        internal static bool ConfirmBypass()
        {
            return EditorUtility.DisplayDialog("Bypass permissions",
                "The agent will run commands and edit files without asking. Use this only in a project you can restore from version control.", "Enable", "Cancel");
        }

        internal static void RunUpdate()
        {
            if (ParleyUpdates.Status != UpdateStatus.Available)
            {
                return;
            }

            if (ReloadGuard.IsLocked)
            {
                EditorUtility.DisplayDialog(UpdateDialogTitle, "Finish or stop the current turn first: Unity can't reload scripts while the agent is working.", "OK");
                return;
            }

            string message = "Update Parley from v" + ParleyUpdates.Current + " to v" + ParleyUpdates.Latest + "?\n\nUnity will recompile scripts. Running chats are interrupted by the reload.";
            if (!EditorUtility.DisplayDialog(UpdateDialogTitle, message, "Update", "Cancel"))
            {
                return;
            }

            _ = ParleyUpdates.InstallAsync();
        }

        private static string DescribeMode(PermissionMode mode)
        {
            return mode switch
            {
                PermissionMode.AcceptEdits => "Accept edits: file edits apply without asking; commands still ask.",
                PermissionMode.Plan => "Plan: the agent explores and proposes a plan without changing anything.",
                PermissionMode.Auto => "Auto: the agent decides when it needs to ask.",
                PermissionMode.BypassPermissions => "Bypass permissions: commands and edits run without asking.",
                PermissionMode.DontAsk => "Don't ask: anything not already allowed is denied without asking.",
                _ => "Ask before edits: the agent asks before editing files or running commands.",
            };
        }

        private static ParleyWindow Find(bool preview)
        {
            foreach (ParleyWindow window in Resources.FindObjectsOfTypeAll<ParleyWindow>())
            {
                if (window._preview == preview)
                {
                    return window;
                }
            }

            return null;
        }

        private static ParleyProfile CreatePreviewProfile(ProfileKind kind)
        {
            return kind switch
            {
                ProfileKind.Codex => ParleyProfile.CreateCodex("Preview · Codex", CodexAuthMethod.CliDefault),
                ProfileKind.Local => ParleyProfile.CreateLocal("Preview · Local model", LocalPreset.LmStudio),
                _ => ParleyProfile.CreateClaude("Preview · Claude Code", ClaudeAuthMethod.CliDefault),
            };
        }

        private static string HistoryLabel(SessionSummary summary, ParleyProfile profile)
        {
            string label = summary.UpdatedUtc.ToLocalTime().ToString("MMM d HH:mm", CultureInfo.InvariantCulture) + "  " + (summary.Title ?? "Chat")
                + (profile == null ? string.Empty : "  · " + profile.Name);
            return label.Replace("/", "∕");
        }

        private void OnEnable()
        {
            if (_openingPreview)
            {
                _preview = true;
            }

            titleContent = new GUIContent(_preview ? PreviewTitle : WindowTitle);
            minSize = MinWindowSize;
            if (_preview)
            {
                StartSession(CreatePreviewProfile(_previewKind), null);
                return;
            }

            ParleyUserSettings settings = ParleyUserSettings.instance;
            SessionRecord record = SessionStore.Load(settings.LastSessionId);
            ParleyProfile profile = record != null ? settings.Find(record.ProfileId) : null;
            if (profile == null)
            {
                profile = settings.ActiveProfile;
                record = record != null && record.ProfileId == profile.Id ? record : null;
            }

            StartSession(profile, record);
        }

        private void OnDisable()
        {
            ParleyUpdates.OnChanged -= MarkDirty;
            EndSession();
        }

        private void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            ParleyStyles.Apply(root);
            root.Add(BuildToolbar());
            _authCard = new AuthCard();
            root.Add(_authCard);
            _trustCard = new TrustCard();
            root.Add(_trustCard);
            _trustCard.Refresh();
            _split = new TwoPaneSplitView(1, ParleyUserSettings.instance.SidePanelWidth, TwoPaneSplitViewOrientation.Horizontal);
            _split.AddToClassList("pl-split");
            _transcript = new TranscriptView();
            _side = new SidePanel();
            _side.RegisterCallback<GeometryChangedEvent>(SideGeometryChangedHandler);
            _split.Add(_transcript);
            _split.Add(_side);
            root.Add(_split);
            _composer = new Composer();
            _composer.OnSubmit += SubmitHandler;
            _composer.OnStop += StopHandler;
            root.Add(_composer);
            _statusBar = new StatusBar();
            root.Add(_statusBar);
            root.schedule.Execute(RefreshStateIfDirty).Every(StateRefreshMs);
            ApplySideVisibility();
            BindViews();
            ParleyUpdates.OnChanged -= MarkDirty;
            ParleyUpdates.OnChanged += MarkDirty;
            ParleyUpdates.EnsureChecked();
        }

        private VisualElement BuildToolbar()
        {
            Toolbar toolbar = new Toolbar();
            toolbar.AddToClassList("pl-toolbar");
            _profileButton = new ToolbarButton(ShowProfileMenu) { tooltip = "Profile: Claude Code, Codex or a local model server" };
            _modelButton = new ToolbarButton(ShowModelMenu) { tooltip = "Model" };
            _modeButton = new ToolbarButton(ShowModeMenu) { tooltip = "Permission mode" };
            _effortButton = new ToolbarButton(ShowEffortMenu) { tooltip = "Reasoning effort" };
            _profileButton.AddToClassList(ShrinkClass);
            _modelButton.AddToClassList(ShrinkClass);
            _modeButton.AddToClassList(ShrinkClass);
            _effortButton.AddToClassList(ShrinkClass);
            _mcpButton = new ToolbarButton(ShowMcpPanel) { tooltip = "MCP servers in this chat" };
            _skillsButton = new ToolbarButton(ShowSkillsPanel) { tooltip = "Skills in this chat" };
            _presetButton = new ToolbarButton(ShowPresetMenu) { tooltip = "Presets: save this chat's skills, MCP servers and auto-run skills, apply them to any chat" };
            _presetButton.AddToClassList(ShrinkClass);
            toolbar.Add(_profileButton);
            toolbar.Add(_modelButton);
            toolbar.Add(_modeButton);
            toolbar.Add(_effortButton);
            toolbar.Add(_mcpButton);
            toolbar.Add(_skillsButton);
            toolbar.Add(_presetButton);
            if (_preview)
            {
                _previewButton = new ToolbarButton(ShowPreviewMenu) { text = "Preview ▾", tooltip = "Play a scripted scenario or open another window in preview. Nothing is sent to a model." };
                _previewButton.AddToClassList("pl-preview-button");
                toolbar.Add(_previewButton);
            }

            toolbar.Add(new ToolbarSpacer { flex = true });
            _updateButton = new ToolbarButton(ShowUpdateMenu) { text = UpdateAvailableText };
            _updateButton.AddToClassList("pl-update");
            ParleyStyles.SetVisible(_updateButton, false);
            toolbar.Add(_updateButton);
            toolbar.Add(new ToolbarButton(NewChat) { text = "New", tooltip = "Start a new chat" });
            _historyButton = new ToolbarButton(ShowHistoryMenu) { text = "History ▾", tooltip = "Resume or delete earlier chats" };
            toolbar.Add(_historyButton);
            _sideToggle = new ToolbarToggle
            {
                text = "Panel",
                tooltip = "Side panel: plan, todos, background tasks, MCP servers and skills",
                value = ParleyUserSettings.instance.SidePanelVisible,
            };
            _sideToggle.RegisterValueChangedCallback(SideToggledHandler);
            toolbar.Add(_sideToggle);
            toolbar.Add(new ToolbarButton(OpenPreferences) { text = "⚙", tooltip = "Parley preferences" });
            return toolbar;
        }

        private void StartSession(ParleyProfile profile, SessionRecord record)
        {
            EndSession();
            _session = new ChatSession(profile, record, _preview);
            _session.OnStateChanged += MarkDirty;
            _session.OnRequestRaised += RequestRaisedHandler;
            if (!_preview)
            {
                ParleyUserSettings.instance.SetActive(profile);
            }

            BindViews();
            _ = _session.EnsureStartedAsync();
        }

        private void EndSession()
        {
            if (_session == null)
            {
                return;
            }

            _session.OnStateChanged -= MarkDirty;
            _session.OnRequestRaised -= RequestRaisedHandler;
            _transcript?.Unbind();
            _session.Dispose();
            _session = null;
        }

        private void BindViews()
        {
            if (_transcript == null || _session == null)
            {
                return;
            }

            ChatSession session = _session;
            _transcript.Bind(session);
            _side.Bind(session);
            _authCard.Bind(session);
            _composer.SetCommandSource(() => session.Commands);
            MarkDirty();
        }

        private void ApplySideVisibility()
        {
            if (_split == null)
            {
                return;
            }

            if (ParleyUserSettings.instance.SidePanelVisible)
            {
                _split.UnCollapse();
                return;
            }

            _split.CollapseChild(1);
        }

        private void MarkDirty()
        {
            _stateDirty = true;
        }

        private void RefreshStateIfDirty()
        {
            if (!_stateDirty || _session == null)
            {
                return;
            }

            _stateDirty = false;
            ChatSession session = _session;
            ParleyProfile profile = session.Profile;
            _profileButton.text = profile.Name + " ▾";
            _profileButton.tooltip = "Profile: " + profile.Name + " (" + ProfileLabels.Describe(profile) + ")";
            string model = string.IsNullOrEmpty(session.Model) ? "Default model" : session.Model;
            _modelButton.text = model + " ▾";
            _modelButton.tooltip = "Model: " + model;
            _modeButton.text = PermissionModes.DisplayName(session.Mode) + " ▾";
            _modeButton.tooltip = "Permission mode. " + DescribeMode(session.Mode);
            _modeButton.EnableInClassList("pl-mode--plan", session.Mode == PermissionMode.Plan);
            _modeButton.EnableInClassList("pl-mode--auto", session.Mode == PermissionMode.AcceptEdits || session.Mode == PermissionMode.Auto);
            _modeButton.EnableInClassList("pl-mode--danger", session.Mode == PermissionMode.BypassPermissions);
            _effortButton.text = "Effort: " + (string.IsNullOrEmpty(session.Effort) ? "default" : session.Effort) + " ▾";
            ParleyStyles.SetVisible(_effortButton, profile.Kind != ProfileKind.Local);
            int mcpCount = McpPanel.EnabledCount(session);
            _mcpButton.text = mcpCount > 0 ? "MCP " + mcpCount : "MCP";
            int skillCount = SkillsPanel.EnabledCount(session);
            _skillsButton.text = skillCount > 0 ? "Skills " + skillCount : "Skills";
            ChatPreset preset = ChatPresets.FindMatch(session.Record, ParleyUserSettings.instance.Presets);
            _presetButton.text = preset != null ? "Preset: " + preset.Name + " ▾" : "Preset ▾";
            RefreshUpdateButton();
            _composer.SetBusy(session.IsBusy);
            _statusBar.Refresh(session);
            _authCard.Refresh();
            _trustCard.Refresh();
            _side.Refresh();
        }

        private void RefreshUpdateButton()
        {
            UpdateStatus status = ParleyUpdates.Status;
            bool installing = status == UpdateStatus.Installing;
            ParleyStyles.SetVisible(_updateButton, installing || status == UpdateStatus.Available);
            _updateButton.text = installing ? "Updating…" : UpdateAvailableText;
            _updateButton.tooltip = "Parley v" + ParleyUpdates.Current + " → v" + ParleyUpdates.Latest;
        }

        private void ShowUpdateMenu()
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("What's new"), false, ReleaseNotesWindow.Open);
            if (ParleyUpdates.Status != UpdateStatus.Available)
            {
                menu.AddDisabledItem(new GUIContent("Updating…"));
            }
            else if (ReloadGuard.IsLocked || (_session != null && _session.IsBusy))
            {
                menu.AddDisabledItem(new GUIContent("Update (finish the current turn first)"));
            }
            else
            {
                menu.AddItem(new GUIContent("Update"), false, RunUpdate);
            }

            menu.DropDown(_updateButton.worldBound);
        }

        private void ShowProfileMenu()
        {
            GenericMenu menu = new GenericMenu();
            if (_preview)
            {
                foreach (ProfileKind kind in new[] { ProfileKind.ClaudeCode, ProfileKind.Codex, ProfileKind.Local })
                {
                    ProfileKind captured = kind;
                    menu.AddItem(new GUIContent(CreatePreviewProfile(kind).Name), _previewKind == kind, () => SwitchPreviewKind(captured));
                }

                menu.AddSeparator(string.Empty);
                menu.AddDisabledItem(new GUIContent("Real profiles: Window > DTech > Parley"));
                menu.DropDown(_profileButton.worldBound);
                return;
            }

            foreach (ParleyProfile profile in ParleyUserSettings.instance.Profiles)
            {
                ParleyProfile captured = profile;
                string label = profile.Name + "  (" + ProfileLabels.Describe(profile) + ")";
                menu.AddItem(new GUIContent(label), _session?.Profile.Id == profile.Id, () => SwitchProfile(captured));
            }

            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Manage profiles…"), false, OpenPreferences);
            menu.DropDown(_profileButton.worldBound);
        }

        private void ShowModelMenu()
        {
            if (_session == null)
            {
                return;
            }

            _ = _session.EnsureStartedAsync();
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Default"), string.IsNullOrEmpty(_session.Model), () => _session.SetModel(null));
            IReadOnlyList<ModelOption> models = _session.Capabilities.Models;
            if (models.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent(_session.IsStarting ? "Loading models…" : "Models load after the agent starts"));
            }

            foreach (ModelOption model in models)
            {
                ModelOption captured = model;
                string label = string.IsNullOrEmpty(model.Description) ? model.DisplayName : model.DisplayName + "  —  " + model.Description;
                menu.AddItem(new GUIContent(label.Replace("/", "∕")), _session.Model == model.Value, () => _session.SetModel(captured.Value));
            }

            menu.DropDown(_modelButton.worldBound);
        }

        private void ShowModeMenu()
        {
            if (_session == null)
            {
                return;
            }

            IReadOnlyList<PermissionMode> modes = _session.Capabilities.Modes;
            if (modes.Count == 0)
            {
                modes = _session.Profile.Kind == ProfileKind.ClaudeCode ? _claudeModes : _agentModes;
            }

            GenericMenu menu = new GenericMenu();
            foreach (PermissionMode mode in modes)
            {
                PermissionMode captured = mode;
                menu.AddItem(new GUIContent(PermissionModes.DisplayName(mode)), _session.Mode == mode, () => SelectMode(captured));
            }

            menu.DropDown(_modeButton.worldBound);
        }

        private void SelectMode(PermissionMode mode)
        {
            if (_session == null)
            {
                return;
            }

            if (mode == PermissionMode.BypassPermissions && !ConfirmBypass())
            {
                return;
            }

            _session.SetMode(mode);
        }

        private void ShowEffortMenu()
        {
            if (_session == null)
            {
                return;
            }

            _ = _session.EnsureStartedAsync();
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Default"), string.IsNullOrEmpty(_session.Effort), () => _session.SetEffort(null));
            IReadOnlyList<string> efforts = _session.Capabilities.Efforts;
            if (efforts.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent(_session.IsStarting ? "Loading effort levels…" : "Effort levels load after the agent starts"));
            }

            foreach (string effort in efforts)
            {
                string captured = effort;
                menu.AddItem(new GUIContent(effort), _session.Effort == effort, () => _session.SetEffort(captured));
            }

            menu.DropDown(_effortButton.worldBound);
        }

        private void ShowMcpPanel()
        {
            if (_side == null)
            {
                return;
            }

            _sideToggle.value = true;
            _side.ShowTab(SidePanelTab.Mcp);
        }

        private void ShowSkillsPanel()
        {
            if (_side == null)
            {
                return;
            }

            _sideToggle.value = true;
            _side.ShowTab(SidePanelTab.Skills);
        }

        private void ShowPresetMenu()
        {
            if (_session == null)
            {
                return;
            }

            GenericMenu menu = new GenericMenu();
            ParleyUserSettings settings = ParleyUserSettings.instance;
            IReadOnlyList<ChatPreset> presets = settings.Presets;
            ChatPreset match = ChatPresets.FindMatch(_session.Record, presets);
            ChatPreset defaultPreset = settings.DefaultPreset;
            if (presets.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("No presets yet"));
            }

            foreach (ChatPreset preset in presets)
            {
                ChatPreset captured = preset;
                menu.AddItem(new GUIContent(MenuName(preset)), preset == match, () => ApplyPreset(captured));
            }

            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Save chat as preset…"), false, SaveChatAsPreset);
            if (presets.Count > 0)
            {
                foreach (ChatPreset preset in presets)
                {
                    ChatPreset captured = preset;
                    menu.AddItem(new GUIContent("Update from chat/" + MenuName(preset)), false, () => UpdatePreset(captured));
                }

                menu.AddSeparator(string.Empty);
                menu.AddItem(new GUIContent("Default for new chats/None"), defaultPreset == null, () => SetDefaultPreset(null));
                foreach (ChatPreset preset in presets)
                {
                    ChatPreset captured = preset;
                    menu.AddItem(new GUIContent("Default for new chats/" + MenuName(preset)), preset == defaultPreset, () => SetDefaultPreset(captured));
                }

                foreach (ChatPreset preset in presets)
                {
                    ChatPreset captured = preset;
                    menu.AddItem(new GUIContent("Rename/" + MenuName(preset) + "…"), false, () => RenamePreset(captured));
                }

                foreach (ChatPreset preset in presets)
                {
                    ChatPreset captured = preset;
                    menu.AddItem(new GUIContent("Delete/" + MenuName(preset)), false, () => DeletePreset(captured));
                }
            }

            menu.DropDown(_presetButton.worldBound);
        }

        private static string MenuName(ChatPreset preset)
        {
            return preset.Name.Replace("/", "∕");
        }

        private Rect PresetButtonScreenRect()
        {
            Rect bound = _presetButton.worldBound;
            return new Rect(position.x + bound.x, position.y + bound.y, bound.width, bound.height);
        }

        private void ApplyPreset(ChatPreset preset)
        {
            _session?.ApplyPreset(preset);
            ShowNotification(new GUIContent("Preset '" + preset.Name + "' applied"), NotificationSeconds);
        }

        private void SaveChatAsPreset()
        {
            PresetNameWindow.Show(PresetButtonScreenRect(), "Save chat as preset", string.Empty, null, name =>
            {
                if (_session == null)
                {
                    return;
                }

                ParleyUserSettings.instance.SavePreset(ChatPresets.Capture(_session.Record, name));
                MarkDirty();
            });
        }

        private void UpdatePreset(ChatPreset preset)
        {
            if (_session == null || !EditorUtility.DisplayDialog("Update preset", "Replace the preset '" + preset.Name + "' with this chat's skills, MCP servers and auto-run skills?", "Update", "Cancel"))
            {
                return;
            }

            ChatPresets.CopyInto(_session.Record, preset);
            ParleyUserSettings.instance.SavePreset(preset);
            MarkDirty();
        }

        private void RenamePreset(ChatPreset preset)
        {
            PresetNameWindow.Show(PresetButtonScreenRect(), "Rename preset", preset.Name, preset.Id, name =>
            {
                preset.Name = name;
                ParleyUserSettings.instance.SavePreset(preset);
                MarkDirty();
            });
        }

        private void SetDefaultPreset(ChatPreset preset)
        {
            ParleyUserSettings.instance.SetDefaultPreset(preset);
            MarkDirty();
        }

        private void DeletePreset(ChatPreset preset)
        {
            if (!EditorUtility.DisplayDialog("Delete preset", "Delete the preset '" + preset.Name + "'? Chats that used it keep their settings.", "Delete", "Cancel"))
            {
                return;
            }

            ParleyUserSettings.instance.RemovePreset(preset);
            MarkDirty();
        }

        private void ShowHistoryMenu()
        {
            GenericMenu menu = new GenericMenu();
            if (_preview)
            {
                menu.AddDisabledItem(new GUIContent("Preview chats are not saved"));
                menu.DropDown(_historyButton.worldBound);
                return;
            }

            List<SessionSummary> sessions = SessionStore.List();
            if (sessions.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("No saved chats"));
            }

            int count = Mathf.Min(sessions.Count, MaxHistoryItems);
            ParleyUserSettings settings = ParleyUserSettings.instance;
            string[] labels = new string[count];
            for (int i = 0; i < count; i++)
            {
                SessionSummary summary = sessions[i];
                labels[i] = HistoryLabel(summary, settings.Find(summary.ProfileId));
                menu.AddItem(new GUIContent(labels[i]), _session?.Record.Id == summary.Id, () => Resume(summary.Id));
            }

            if (count > 0)
            {
                menu.AddSeparator(string.Empty);
                for (int i = 0; i < count; i++)
                {
                    SessionSummary summary = sessions[i];
                    string label = labels[i];
                    menu.AddItem(new GUIContent("Delete/" + label), false, () => DeleteSession(summary.Id, label));
                }
            }

            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Reveal sessions folder"), false, () => EditorUtility.RevealInFinder(ProjectPaths.SessionsFolder));
            menu.DropDown(_historyButton.worldBound);
        }

        private void SwitchProfile(ParleyProfile profile)
        {
            if (_session != null && _session.Profile.Id == profile.Id)
            {
                return;
            }

            StartSession(profile, null);
        }

        private void NewChat()
        {
            ParleyProfile profile = _preview ? CreatePreviewProfile(_previewKind) : _session?.Profile ?? ParleyUserSettings.instance.ActiveProfile;
            StartSession(profile, null);
            _composer?.FocusInput();
        }

        private void Resume(string id)
        {
            SessionRecord record = SessionStore.Load(id);
            if (record == null)
            {
                return;
            }

            ParleyProfile profile = ParleyUserSettings.instance.Find(record.ProfileId);
            if (profile == null)
            {
                EditorUtility.DisplayDialog("Parley", "The profile this chat used no longer exists.", "OK");
                return;
            }

            StartSession(profile, record);
        }

        private void DeleteSession(string id, string label)
        {
            if (!EditorUtility.DisplayDialog("Delete chat", "Delete '" + label + "'? This cannot be undone.", "Delete", "Cancel"))
            {
                return;
            }

            SessionStore.Delete(id);
            if (_session?.Record.Id == id)
            {
                NewChat();
            }
        }

        private void ShowPreviewMenu()
        {
            GenericMenu menu = new GenericMenu();
            foreach (PreviewScenario scenario in (PreviewScenario[])Enum.GetValues(typeof(PreviewScenario)))
            {
                PreviewScenario captured = scenario;
                string label = ObjectNames.NicifyVariableName(scenario.ToString()) + "   " + PreviewBackend.CommandFor(scenario);
                menu.AddItem(new GUIContent("Chat/" + label.Replace("/", "∕")), false, () => RunPreview(captured));
            }

            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Trust card"), _trustCard != null && _trustCard.ForcePreview, ToggleTrustPreview);
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Windows/Panel with every item type"), false, PanelPreview.OpenDemo);
            menu.AddItem(new GUIContent("Windows/Panel with errors"), false, PanelPreview.OpenBroken);
            menu.AddItem(new GUIContent("Windows/Empty panel"), false, PanelPreview.OpenEmpty);
            menu.AddItem(new GUIContent("Windows/Saved panels"), false, PanelsWindow.Open);
            menu.AddItem(new GUIContent("Windows/Release notes"), false, ReleaseNotesWindow.OpenPreview);
            menu.AddItem(new GUIContent("Windows/Preferences"), false, OpenPreferences);
            menu.DropDown(_previewButton.worldBound);
        }

        private void RunPreview(PreviewScenario scenario)
        {
            if (_session == null)
            {
                return;
            }

            _transcript?.ScrollToBottom();
            _ = _session.SendAsync(PreviewBackend.CommandFor(scenario), new List<ChatAttachment>());
        }

        private void ToggleTrustPreview()
        {
            if (_trustCard == null)
            {
                return;
            }

            _trustCard.ForcePreview = !_trustCard.ForcePreview;
            _trustCard.Refresh();
        }

        private void SwitchPreviewKind(ProfileKind kind)
        {
            _previewKind = kind;
            StartSession(CreatePreviewProfile(kind), null);
        }

        private void SideToggledHandler(ChangeEvent<bool> evt)
        {
            ParleyUserSettings.instance.SidePanelVisible = evt.newValue;
            ApplySideVisibility();
        }

        private void SideGeometryChangedHandler(GeometryChangedEvent evt)
        {
            if (ParleyUserSettings.instance.SidePanelVisible && evt.newRect.width > 0)
            {
                ParleyUserSettings.instance.SidePanelWidth = evt.newRect.width;
            }
        }

        private void SubmitHandler(string text, List<ChatAttachment> attachments)
        {
            if (_session == null)
            {
                return;
            }

            _transcript.ScrollToBottom();
            _ = _session.SendAsync(text, attachments);
        }

        private void StopHandler()
        {
            _session?.Interrupt();
        }

        private void RequestRaisedHandler(PendingRequest request)
        {
            _transcript?.ScrollToBottom();
            if (request.Kind == RequestKind.PlanApproval && _side != null)
            {
                _sideToggle.value = true;
                _side.ShowTab(SidePanelTab.Plan);
            }

            if (focusedWindow != this)
            {
                ShowNotification(new GUIContent("Parley needs your input"), NotificationSeconds);
            }
        }
    }
}