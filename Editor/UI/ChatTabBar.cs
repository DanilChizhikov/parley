using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DTech.Parley.Editor.Sessions;
using DTech.Parley.Editor.Skills;
using UnityEditor;
using UnityEngine.UIElements;

namespace DTech.Parley.Editor.UI
{
    internal sealed class ChatTabBar : ScrollView
    {
        private static readonly string[] SpinnerFrames = { "◐", "◓", "◑", "◒" };

        private readonly IChatTabManager _manager;
        private readonly Func<ChatSession, bool> _close;
        private readonly Dictionary<ChatSession, Button> _buttons = new();
        private bool _handoffRunning;
        private CancellationTokenSource _handoffCancellation;

        public ChatTabBar(IChatTabManager manager, Func<ChatSession, bool> close) : base(ScrollViewMode.Horizontal)
        {
            _manager = manager;
            _close = close;
            AddToClassList("pl-chat-tabs");
            contentContainer.style.flexDirection = FlexDirection.Row;
            verticalScrollerVisibility = ScrollerVisibility.Hidden;
            horizontalScrollerVisibility = ScrollerVisibility.Auto;
            RegisterCallback<DetachFromPanelEvent>(_ => _handoffCancellation?.Cancel());
        }

        public void Refresh()
        {
            bool rebuild = _buttons.Count != _manager.Tabs.Count;
            foreach (ChatSession session in _manager.Tabs)
            {
                rebuild |= !_buttons.ContainsKey(session);
            }

            if (rebuild)
            {
                contentContainer.Clear();
                _buttons.Clear();
                foreach (ChatSession session in _manager.Tabs)
                {
                    VisualElement tab = new VisualElement();
                    tab.AddToClassList("pl-chat-tab");
                    Button select = new Button(() => _manager.Select(session));
                    select.AddToClassList("pl-chat-tab__select");
                    tab.Add(select);
                    _buttons.Add(session, select);
                    tab.Add(new Button(() => _close(session)) { text = "×", tooltip = "Close tab" });
                    tab.AddManipulator(new ContextualMenuManipulator(evt => PopulateMenu(evt, session)));
                    contentContainer.Add(tab);
                }

                contentContainer.Add(new Button(ShowNewTabMenu)
                    { text = "+ ▾", tooltip = "New blank tab or role preset (Ctrl/Cmd+T for blank)" });
            }

            foreach (KeyValuePair<ChatSession, Button> pair in _buttons)
            {
                ChatSession session = pair.Key;
                string status = session.ExecutionStatus == ChatExecutionStatus.WaitingApproval ? "! " :
                    session.HasActiveWork ? Spinner() + " " :
                    session.ExecutionStatus == ChatExecutionStatus.Error ? "⚠ " :
                    session.ExecutionStatus == ChatExecutionStatus.Interrupted ? "■ " : string.Empty;
                pair.Value.text = status + session.Title + (session.Record.HasUnreadResponse ? " ●" : string.Empty);
                pair.Value.tooltip = session.Profile.Name + " · " + session.ExecutionStatus +
                    (session.Record.HasUnreadResponse ? " · Unread reply" : string.Empty);
                pair.Value.EnableInClassList("pl-chat-tab--active", session == _manager.Active);
            }
        }

        private static string Spinner()
        {
            return SpinnerFrames[(int)(EditorApplication.timeSinceStartup * 8) % SpinnerFrames.Length];
        }

        public void ShowNewTabMenu()
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new UnityEngine.GUIContent("Blank chat"), false,
                () => _manager.Open(_manager.Active?.Profile ?? ParleyUserSettings.instance.ActiveProfile));
            foreach (SkillDocument skill in SkillRegistry.Current.Skills)
            {
                SkillDocument role = skill;
                menu.AddItem(new UnityEngine.GUIContent("Role Preset/" + role.Name, role.Description), false, () =>
                {
                    ParleyUserSettings settings = ParleyUserSettings.instance;
                    if (ChatPresets.TryCreateRole(role, settings.Profiles, _manager.Active?.Profile ?? settings.ActiveProfile,
                        settings.DefaultMode, out ParleyProfile profile, out SessionRecord record, out string error))
                        _manager.Open(profile, record);
                    else EditorUtility.DisplayDialog("Cannot open role", error, "OK");
                });
            }
            menu.AddSeparator(string.Empty);
            menu.AddItem(new UnityEngine.GUIContent("Reload skills"), false, SkillRegistry.Reload);
            menu.ShowAsContext();
        }

        private void PopulateMenu(ContextualMenuPopulateEvent evt, ChatSession session)
        {
            evt.menu.AppendAction("Duplicate Tab", _ => _manager.Duplicate(session),
                session.HasActiveWork ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);
            DropdownMenuAction.Status handoffStatus = _handoffRunning || session.HasActiveWork
                ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal;
            foreach (ParleyProfile target in ParleyUserSettings.instance.Profiles)
            {
                ParleyProfile destination = target;
                foreach (int count in new[] { 10, 25, 50 })
                {
                    int lastMessages = count;
                    evt.menu.AppendAction("Send Context to New Tab/" + target.Name + "/Last " + count + " Messages",
                        _ => _manager.OpenWithContext(destination,
                            ContextHandoffService.Capture(session.Record, lastMessages)), handoffStatus);
                }

                evt.menu.AppendAction("Summarize & Send to New Tab/" + target.Name + "/Use current model",
                    action => { _ = HandoffAsync(session, destination, session.Profile, CancellationToken.None); }, handoffStatus);
                evt.menu.AppendAction("Summarize & Send to New Tab/" + target.Name + "/Use target model",
                    action => { _ = HandoffAsync(session, destination, destination, CancellationToken.None); }, handoffStatus);
            }
            evt.menu.AppendAction("Close Other Tabs", _ =>
            {
                _manager.Select(session);
                List<ChatSession> others = new List<ChatSession>(_manager.Tabs);
                foreach (ChatSession other in others)
                {
                    if (other != session && !_close(other))
                    {
                        break;
                    }
                }
            });
        }

        private async Task HandoffAsync(ChatSession source, ParleyProfile target, ParleyProfile summarizer,
            CancellationToken cancellationToken)
        {
            if (_handoffRunning)
            {
                return;
            }
            // Freeze before awaiting: later source messages cannot enter the summary request.
            ContextSnapshot captured = ContextHandoffService.Capture(source.Record, int.MaxValue);
            ParleyProfile model = UnityEngine.JsonUtility.FromJson<ParleyProfile>(
                UnityEngine.JsonUtility.ToJson(summarizer));
            if (ReferenceEquals(summarizer, source.Profile))
            {
                model.Model = source.Model;
                model.Effort = source.Effort;
            }
            _handoffRunning = true;
            using CancellationTokenSource lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _handoffCancellation = lifetime;
            ReloadGuard.Acquire(this);
            try
            {
                ContextHandoffService service = new(new ContextSummaryGenerator());
                ContextSnapshot snapshot = await service.SummarizeAsync(captured, model, lifetime.Token);
                _manager.OpenWithContext(target, snapshot);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog("Context handoff failed", exception.Message, "OK");
            }
            finally
            {
                _handoffRunning = false;
                _handoffCancellation = null;
                ReloadGuard.Release(this);
            }
        }
    }
}
