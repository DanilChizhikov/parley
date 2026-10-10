using System.Collections.Generic;
using System.Globalization;
using DTech.Parley.Editor.Sessions;
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
        private const string WindowTitle = "Parley";
        private const long StateRefreshMs = 100;
        private const int MaxHistoryItems = 30;
        private const double NotificationSeconds = 2.0;

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
        private ToolbarToggle _sideToggle;
        private bool _stateDirty = true;

        [MenuItem(MenuPath)]
        public static void Open()
        {
            ParleyWindow window = GetWindow<ParleyWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(780.0f, 620.0f);
            window.Show();
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

        private static string HistoryLabel(SessionSummary summary, ParleyProfile profile)
        {
            string label = summary.UpdatedUtc.ToLocalTime().ToString("MMM d HH:mm", CultureInfo.InvariantCulture) + "  " + (summary.Title ?? "Chat")
                + (profile == null ? string.Empty : "  · " + profile.Name);
            return label.Replace("/", "∕");
        }

        private void OnEnable()
        {
            titleContent = new GUIContent(WindowTitle);
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
        }

        private VisualElement BuildToolbar()
        {
            Toolbar toolbar = new Toolbar();
            toolbar.AddToClassList("pl-toolbar");
            _profileButton = new ToolbarButton(ShowProfileMenu) { tooltip = "Profile: Claude Code, Codex or a local model server" };
            _modelButton = new ToolbarButton(ShowModelMenu) { tooltip = "Model" };
            _modeButton = new ToolbarButton(ShowModeMenu) { tooltip = "Permission mode" };
            _effortButton = new ToolbarButton(ShowEffortMenu) { tooltip = "Reasoning effort" };
            toolbar.Add(_profileButton);
            toolbar.Add(_modelButton);
            toolbar.Add(_modeButton);
            toolbar.Add(_effortButton);
            toolbar.Add(new ToolbarSpacer { flex = true });
            toolbar.Add(new ToolbarButton(NewChat) { text = "New", tooltip = "Start a new chat" });
            toolbar.Add(new ToolbarButton(ShowHistoryMenu) { text = "History ▾", tooltip = "Resume or delete earlier chats" });
            _sideToggle = new ToolbarToggle { text = "Panel", tooltip = "Show plan, todos and background tasks", value = ParleyUserSettings.instance.SidePanelVisible };
            _sideToggle.RegisterValueChangedCallback(SideToggledHandler);
            toolbar.Add(_sideToggle);
            toolbar.Add(new ToolbarButton(OpenPreferences) { text = "⚙", tooltip = "Parley preferences" });
            return toolbar;
        }

        private void StartSession(ParleyProfile profile, SessionRecord record)
        {
            EndSession();
            _session = new ChatSession(profile, record);
            _session.OnStateChanged += MarkDirty;
            _session.OnRequestRaised += RequestRaisedHandler;
            ParleyUserSettings.instance.SetActive(profile);
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
            _composer.SetCommandSource(() => session.Capabilities.Commands);
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
            _modelButton.text = (string.IsNullOrEmpty(session.Model) ? "Default model" : session.Model) + " ▾";
            _modeButton.text = PermissionModes.DisplayName(session.Mode) + " ▾";
            _modeButton.EnableInClassList("pl-mode--plan", session.Mode == PermissionMode.Plan);
            _modeButton.EnableInClassList("pl-mode--auto", session.Mode == PermissionMode.AcceptEdits || session.Mode == PermissionMode.Auto);
            _modeButton.EnableInClassList("pl-mode--danger", session.Mode == PermissionMode.BypassPermissions);
            _effortButton.text = "Effort: " + (string.IsNullOrEmpty(session.Effort) ? "default" : session.Effort) + " ▾";
            ParleyStyles.SetVisible(_effortButton, profile.Kind != ProfileKind.Local);
            _composer.SetBusy(session.IsBusy);
            _statusBar.Refresh(session);
            _authCard.Refresh();
            _trustCard.Refresh();
            _side.Refresh();
        }

        private void ShowProfileMenu()
        {
            GenericMenu menu = new GenericMenu();
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
                menu.AddDisabledItem(new GUIContent(_session.IsStarting ? "Loading models…" : "No model list (open the menu again once started)"));
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
                menu.AddDisabledItem(new GUIContent(_session.IsStarting ? "Loading effort levels…" : "No effort levels (open the menu again once started)"));
            }

            foreach (string effort in efforts)
            {
                string captured = effort;
                menu.AddItem(new GUIContent(effort), _session.Effort == effort, () => _session.SetEffort(captured));
            }

            menu.DropDown(_effortButton.worldBound);
        }

        private void ShowHistoryMenu()
        {
            GenericMenu menu = new GenericMenu();
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
            menu.ShowAsContext();
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
            StartSession(_session?.Profile ?? ParleyUserSettings.instance.ActiveProfile, null);
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