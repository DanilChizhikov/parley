using System;
using System.Collections.Generic;
using DTech.Parley.Editor.Sessions;
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

        public ChatTabBar(IChatTabManager manager, Func<ChatSession, bool> close) : base(ScrollViewMode.Horizontal)
        {
            _manager = manager;
            _close = close;
            AddToClassList("pl-chat-tabs");
            contentContainer.style.flexDirection = FlexDirection.Row;
            verticalScrollerVisibility = ScrollerVisibility.Hidden;
            horizontalScrollerVisibility = ScrollerVisibility.Auto;
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

                contentContainer.Add(new Button(() => _manager.Open(
                    _manager.Active?.Profile ?? ParleyUserSettings.instance.ActiveProfile))
                    { text = "+", tooltip = "New tab (Ctrl/Cmd+T)" });
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

        private void PopulateMenu(ContextualMenuPopulateEvent evt, ChatSession session)
        {
            evt.menu.AppendAction("Duplicate Tab", _ => _manager.Duplicate(session),
                session.HasActiveWork ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);
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
    }
}
