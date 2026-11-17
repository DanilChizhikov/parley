using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace DTech.Parley.Editor.Sessions
{
    internal interface IChatTabManager
    {
        event Action OnChanged;
        event Action<ChatSession> OnBackgroundCompleted;
        IReadOnlyList<ChatSession> Tabs { get; }
        ChatSession Active { get; }
        bool HasActiveWork { get; }
        Orchestration.OrchestrationEngine Engine { get; }
        ChatSession Open(ParleyProfile profile, SessionRecord record = null);
        void Select(ChatSession session);
        void Cycle(int direction);
        void Close(ChatSession session);
        ChatSession Duplicate(ChatSession session);
        ChatSession OpenWithContext(ParleyProfile profile, ContextSnapshot snapshot);
        void Save();
    }

    internal sealed class ChatTabManager : IChatTabManager, IDisposable
    {
        public event Action OnChanged;
        public event Action<ChatSession> OnBackgroundCompleted;

        private readonly IChatSessionFactory _factory;
        private readonly IChatTabStore _store;
        private readonly List<ChatSession> _tabs = new();
        private readonly Dictionary<ChatSession, Action> _completedHandlers = new();
        private bool _disposed;
        public Orchestration.OrchestrationEngine Engine { get; internal set; }
        internal List<Orchestration.OrchestrationRun> Runs { get; private set; } = new();

        public IReadOnlyList<ChatSession> Tabs => _tabs;
        public ChatSession Active { get; private set; }
        public bool HasActiveWork => Engine?.HasActiveWork == true || _tabs.Exists(session => session.HasActiveWork);

        public ChatTabManager(IChatSessionFactory factory, IChatTabStore store)
        {
            _factory = factory;
            _store = store;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Save();
            Engine?.Dispose();
            _disposed = true;
            foreach (ChatSession session in _tabs)
            {
                Unsubscribe(session);
                session.Dispose();
            }

            _tabs.Clear();
            Active = null;
            OnChanged = null;
            OnBackgroundCompleted = null;
        }

        public void Restore(Func<string, ParleyProfile> findProfile, ParleyProfile defaultProfile,
            SessionRecord legacyRecord = null)
        {
            ChatTabState state = _store.Load();
            Runs = state?.Runs ?? new List<Orchestration.OrchestrationRun>();
            if (state?.Tabs != null)
            {
                foreach (SessionRecord record in state.Tabs)
                {
                    ParleyProfile profile = record == null ? null : findProfile(record.ProfileId);
                    if (profile == null || _tabs.Exists(session => session.SessionId == record.Id))
                    {
                        continue;
                    }

                    RecoverInterrupted(record);
                    Add(profile, record);
                }

                Active = _tabs.Find(session => session.SessionId == state.ActiveSessionId);
            }

            if (_tabs.Count == 0)
            {
                ParleyProfile profile = legacyRecord == null ? null : findProfile(legacyRecord.ProfileId);
                if (legacyRecord != null)
                {
                    RecoverInterrupted(legacyRecord);
                }

                Add(profile ?? defaultProfile, profile == null ? null : legacyRecord);
            }

            Active ??= _tabs[0];
            Active.Record.HasUnreadResponse = false;
            ChangedHandler();
        }

        public ChatSession Open(ParleyProfile profile, SessionRecord record = null)
        {
            ChatSession session = record == null ? null : _tabs.Find(tab => tab.SessionId == record.Id);
            session ??= Add(profile, record);
            Select(session);
            return session;
        }

        public void Select(ChatSession session)
        {
            if (!_tabs.Contains(session))
            {
                return;
            }

            Active = session;
            session.Record.HasUnreadResponse = false;
            ChangedHandler();
        }

        public void Cycle(int direction)
        {
            if (_tabs.Count > 0)
            {
                Select(_tabs[(_tabs.IndexOf(Active) + direction + _tabs.Count) % _tabs.Count]);
            }
        }

        public void Close(ChatSession session)
        {
            int index = _tabs.IndexOf(session);
            if (index < 0)
            {
                return;
            }

            Unsubscribe(session);
            Engine?.CancelSession(session);
            _tabs.RemoveAt(index);
            session.Dispose();
            if (Active == session)
            {
                Active = _tabs.Count == 0 ? null : _tabs[Math.Min(index, _tabs.Count - 1)];
                if (Active != null)
                {
                    Active.Record.HasUnreadResponse = false;
                }
            }

            ChangedHandler();
        }

        public ChatSession Duplicate(ChatSession session)
        {
            session.Save();
            SessionRecord record = JsonConvert.DeserializeObject<SessionRecord>(JsonConvert.SerializeObject(session.Record));
            record.Id = Guid.NewGuid().ToString("N");
            record.ParentSessionId = session.SessionId;
            record.Title += " copy";
            record.ForkBackendSessionId = record.BackendSessionId ?? record.ForkBackendSessionId;
            record.BackendSessionId = null;
            record.ExecutionStatus = ChatExecutionStatus.Idle;
            record.BackgroundTasks.Clear();
            record.HasUnreadResponse = false;
            record.ActiveSkillId = null;
            record.OrchestrationRunId = null;
            record.OrchestrationTurnId = null;
            ChatSession copy = Open(session.Profile, record);
            _ = copy.EnsureStartedAsync();
            return copy;
        }

        public void Save()
        {
            if (_disposed)
            {
                return;
            }

            ChatTabState state = new ChatTabState { ActiveSessionId = Active?.SessionId, Runs = Runs };
            foreach (ChatSession session in _tabs)
            {
                session.Save();
                state.Tabs.Add(session.Record);
            }

            _store.Save(state);
        }

        internal void NotifyOrchestrationChanged() { OnChanged?.Invoke(); }

        public ChatSession OpenWithContext(ParleyProfile profile, ContextSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }
            return Open(profile, new SessionRecord
            {
                ProfileId = profile.Id,
                Kind = profile.Kind,
                ParentSessionId = snapshot.SourceSessionId,
                ContextSnapshot = snapshot,
                Title = "Context handoff",
            });
        }

        private ChatSession Add(ParleyProfile profile, SessionRecord record)
        {
            ChatSession session = _factory.Create(profile, record);
            _tabs.Add(session);
            session.Orchestration = Engine;
            session.OnStateChanged += ChangedHandler;
            Action completed = () => CompletedHandler(session);
            _completedHandlers.Add(session, completed);
            session.OnTurnCompleted += completed;
            return session;
        }

        private void Unsubscribe(ChatSession session)
        {
            session.OnStateChanged -= ChangedHandler;
            session.OnTurnCompleted -= _completedHandlers[session];
            _completedHandlers.Remove(session);
        }

        private void ChangedHandler()
        {
            Save();
            OnChanged?.Invoke();
        }

        private void CompletedHandler(ChatSession session)
        {
            if (session != Active)
            {
                session.Record.HasUnreadResponse = true;
                OnBackgroundCompleted?.Invoke(session);
            }

            ChangedHandler();
        }

        private static void RecoverInterrupted(SessionRecord record)
        {
            bool interrupted = record.ExecutionStatus == ChatExecutionStatus.Streaming ||
                record.ExecutionStatus == ChatExecutionStatus.WaitingApproval;
            record.BackgroundTasks ??= new List<BackgroundTaskInfo>();
            foreach (BackgroundTaskInfo task in record.BackgroundTasks)
            {
                if (task.Status == "running" || task.Status == "pending")
                {
                    task.Status = "interrupted";
                    interrupted = true;
                }
            }

            if (interrupted)
            {
                record.ExecutionStatus = ChatExecutionStatus.Interrupted;
                record.ActiveSkillId = null;
                record.Entries.Add(new TranscriptEntry
                {
                    Role = EntryRole.Notice,
                    Blocks = { new TranscriptBlock
                    {
                        Kind = BlockKind.Notice, Level = NoticeLevel.Warning, IsFinished = true,
                        Text = "Interrupted by an editor reload. Review the last action before continuing; nothing was replayed.",
                    } },
                });
            }
        }
    }
}
