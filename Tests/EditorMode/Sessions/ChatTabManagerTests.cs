using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using DTech.Parley.Editor;
using DTech.Parley.Editor.Sessions;
using DTech.Parley.Editor.Agents.Claude;
using DTech.Parley.Editor.Agents.Codex;
using DTech.Parley.Editor.Tools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class ChatTabManagerTests
    {
        private readonly List<ChatTabManager> _managers = new();
        private ParleyProfile _profile;
        private MemoryStore _store;
        private Factory _factory;
        private ChatTabManager _manager;

        [SetUp]
        public void SetUp()
        {
            _profile = ParleyProfile.CreateLocal("Test", LocalPreset.LmStudio);
            _store = new MemoryStore();
            _factory = new Factory();
            _manager = CreateManager(_store);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (ChatTabManager manager in _managers)
            {
                manager.Dispose();
            }

            _managers.Clear();
        }

        [Test]
        public void SwitchingTabsDoesNotDisposeOrShareBackends()
        {
            ChatSession first = _manager.Open(_profile);
            FakeBackend backend = (FakeBackend)first.Backend;
            first.SendAsync("first message", null);
            ChatSession second = _manager.Open(_profile);
            Assert.IsTrue(first.IsBusy);
            Assert.IsFalse(backend.Disposed);
            Assert.AreNotSame(first.Backend, second.Backend);
            Assert.AreNotSame(first.Profile, second.Profile);
            Assert.IsEmpty(second.Record.Entries);
            _manager.Select(first);
            Assert.AreSame(first, _manager.Active);
            Assert.AreEqual(1, backend.SendCount);
        }

        [Test]
        public async Task HandoffStartsFreshAcrossProvidersAndDeliversContextOnce()
        {
            ChatSession source = _manager.Open(_profile);
            source.Record.Entries.Add(new TranscriptEntry
            {
                Role = EntryRole.User,
                Blocks = { new TranscriptBlock { Kind = BlockKind.Text, Text = "original goal", IsFinished = true } },
            });
            source.Record.BackendSessionId = "source-thread";
            ContextSnapshot snapshot = ContextHandoffService.Capture(source.Record, 10);
            ParleyProfile target = ParleyProfile.CreateCodex("Target", CodexAuthMethod.CliDefault);
            ChatSession destination = _manager.OpenWithContext(target, snapshot);
            FakeBackend backend = (FakeBackend)destination.Backend;
            Assert.AreSame(destination, _manager.Active);
            Assert.AreEqual(source.SessionId, destination.ParentSessionId);
            Assert.AreNotEqual("source-thread", destination.Record.BackendSessionId);
            Assert.IsNull(destination.Record.ForkBackendSessionId);
            Assert.IsNull(destination.Record.LocalHistory);
            Assert.IsNull(backend.ResumeId);
            Assert.IsEmpty(destination.Messages);
            Task first = destination.SendAsync("continue", null);
            StringAssert.Contains("original goal", backend.LastText);
            StringAssert.Contains("continue", backend.LastText);
            backend.Complete();
            await first;
            Task second = destination.SendAsync("next", null);
            Assert.AreEqual("next", backend.LastText);
            backend.Complete();
            await second;
            _manager.Save();
            ChatTabManager restored = CreateManager(_store);
            restored.Restore(id => id == target.Id ? target : _profile, _profile);
            Assert.AreEqual(snapshot.Markdown, restored.Active.Record.ContextSnapshot.Markdown);
            Assert.IsTrue(restored.Active.Record.ContextDelivered);
            await destination.ClearAsync();
            Assert.IsNull(destination.Record.ContextSnapshot);
            Assert.IsFalse(destination.Record.ContextDelivered);
        }

        [Test]
        public void BackgroundCompletionStreamsToItsOwnTranscriptAndMarksUnread()
        {
            ChatSession first = _manager.Open(_profile);
            first.SendAsync("question", null);
            ChatSession second = _manager.Open(_profile);
            ChatSession notified = null;
            _manager.OnBackgroundCompleted += session => notified = session;
            FakeBackend backend = (FakeBackend)first.Backend;
            backend.Sink.TextDelta("reply", "background answer");
            backend.Complete();
            first.Save();
            Assert.IsEmpty(second.Record.Entries);
            Assert.AreEqual("background answer", first.Record.Entries[1].Blocks[0].Text);
            Assert.IsTrue(first.Record.HasUnreadResponse);
            Assert.AreSame(first, notified);
            _manager.Select(first);
            Assert.IsFalse(first.Record.HasUnreadResponse);
        }

        [Test]
        public void ActiveCompletionDoesNotMarkUnread()
        {
            ChatSession session = _manager.Open(_profile);
            session.SendAsync("question", null);
            ((FakeBackend)session.Backend).Complete();
            Assert.IsFalse(session.Record.HasUnreadResponse);
        }

        [Test]
        public void RestoresOrderActiveEmptyTabsAndPerSessionSelections()
        {
            ChatSession first = _manager.Open(_profile);
            first.SetModel("model-a");
            first.SetEffort("high");
            ChatSession second = _manager.Open(_profile);
            second.SetModel(null);
            second.SetEffort(null);
            _manager.Select(first);
            ChatTabManager restored = CreateManager(_store);
            restored.Restore(id => _profile, _profile);
            Assert.AreEqual(2, restored.Tabs.Count);
            Assert.AreEqual(first.SessionId, restored.Tabs[0].SessionId);
            Assert.AreEqual(second.SessionId, restored.Tabs[1].SessionId);
            Assert.AreSame(restored.Tabs[0], restored.Active);
            Assert.AreEqual("model-a", restored.Active.Model);
            Assert.AreEqual("high", restored.Active.Effort);
            Assert.IsNull(restored.Tabs[1].Model);
            Assert.IsEmpty(restored.Tabs[1].Record.Entries);
            Assert.IsNull(_profile.Model);
        }

        [Test]
        public void ReloadRestoresInterruptedWithoutReplayingAndExpiresApprovals()
        {
            ChatSession session = _manager.Open(_profile);
            session.SendAsync("do something", null);
            ((IAgentSink)session).RequestRaised(new PendingRequest
            {
                Id = "approval", Input = new JObject(), ToolName = "Bash",
            });
            Assert.AreEqual(ChatExecutionStatus.WaitingApproval, session.ExecutionStatus);
            _manager.Save();
            ChatTabManager restored = CreateManager(_store);
            restored.Restore(id => _profile, _profile);
            Assert.AreEqual(ChatExecutionStatus.Interrupted, restored.Active.ExecutionStatus);
            Assert.IsFalse(restored.HasActiveWork);
            Assert.IsEmpty(restored.Active.OpenRequests);
            Assert.AreEqual(0, ((FakeBackend)restored.Active.Backend).SendCount);
            Assert.AreEqual(0, ((FakeBackend)restored.Active.Backend).StartCount);
            Assert.IsNotEmpty(restored.Active.Record.Entries[1].Blocks[0].Resolution);
        }

        [Test]
        public void BackgroundTasksKeepWorkActiveAndRecoverAsInterrupted()
        {
            ChatSession session = _manager.Open(_profile);
            ((IAgentSink)session).BackgroundTaskChanged(new BackgroundTaskInfo { TaskId = "job", Status = "running" });
            Assert.IsTrue(_manager.HasActiveWork);
            ChatTabManager restored = CreateManager(_store);
            restored.Restore(id => _profile, _profile);
            Assert.AreEqual(ChatExecutionStatus.Interrupted, restored.Active.ExecutionStatus);
            Assert.AreEqual("interrupted", restored.Active.Record.BackgroundTasks[0].Status);
            Assert.IsFalse(restored.HasActiveWork);
        }

        [Test]
        public void LegacySingleSessionRetainsTranscriptAndUsesProfileDefaults()
        {
            _profile.Model = "profile-default";
            SessionRecord legacy = new SessionRecord { ProfileId = _profile.Id, Kind = _profile.Kind };
            legacy.Entries.Add(new TranscriptEntry { Role = EntryRole.User });
            _manager.Restore(id => _profile, _profile, legacy);
            Assert.AreEqual(legacy.Id, _manager.Active.SessionId);
            Assert.AreEqual(1, _manager.Active.Record.Entries.Count);
            Assert.AreEqual("profile-default", _manager.Active.Model);
            Assert.AreEqual(ChatExecutionStatus.Idle, _manager.Active.ExecutionStatus);
        }

        [Test]
        public void ResumeSelectsExistingLiveSessionRatherThanOpeningItsStaleRecord()
        {
            ChatSession session = _manager.Open(_profile);
            _manager.Open(_profile);
            ChatSession resumed = _manager.Open(_profile, new SessionRecord { Id = session.SessionId });
            Assert.AreSame(session, resumed);
            Assert.AreEqual(2, _manager.Tabs.Count);
        }

        [Test]
        public void DuplicateHasIndependentHistoryAndNoSharedBackendThread()
        {
            ChatSession session = _manager.Open(_profile);
            session.SendAsync("question", null);
            ((FakeBackend)session.Backend).Complete();
            session.Record.LocalHistory = new List<JObject> { new JObject { ["role"] = "user", ["content"] = "question" } };
            ChatSession copy = _manager.Duplicate(session);
            Assert.AreNotEqual(session.SessionId, copy.SessionId);
            Assert.AreEqual(session.SessionId, copy.ParentSessionId);
            Assert.IsNull(((FakeBackend)copy.Backend).ResumeId);
            Assert.AreNotSame(session.Record.Entries, copy.Record.Entries);
            Assert.AreNotSame(session.Record.Entries[0], copy.Record.Entries[0]);
            Assert.AreNotSame(session.Record.LocalHistory[0], copy.Record.LocalHistory[0]);
            Assert.IsFalse(copy.HasActiveWork);
        }

        [Test]
        public void CyclingAndClosingChooseAdjacentTabAndDisposeOnlyClosedBackend()
        {
            ChatSession first = _manager.Open(_profile);
            ChatSession second = _manager.Open(_profile);
            ChatSession third = _manager.Open(_profile);
            _manager.Cycle(1);
            Assert.AreSame(first, _manager.Active);
            _manager.Cycle(-1);
            Assert.AreSame(third, _manager.Active);
            _manager.Select(second);
            _manager.Close(second);
            Assert.AreSame(third, _manager.Active);
            Assert.IsTrue(((FakeBackend)second.Backend).Disposed);
            Assert.IsFalse(((FakeBackend)first.Backend).Disposed);
            Assert.IsFalse(((FakeBackend)third.Backend).Disposed);
        }

        [Test]
        public void TwoConcurrentSendsRemainBusyUntilBothPipelinesReturn()
        {
            ChatSession session = _manager.Open(_profile);
            session.SendAsync("first", null);
            session.SendAsync("second", null);
            FakeBackend backend = (FakeBackend)session.Backend;
            backend.Complete();
            Assert.IsTrue(session.IsBusy);
            Assert.IsTrue(_manager.HasActiveWork);
            backend.Complete();
            Assert.IsFalse(session.IsBusy);
        }

        [Test]
        public void CompletionBeforeBackendIdleIsReconciledOnNextUpdate()
        {
            ChatSession session = _manager.Open(_profile);
            session.SendAsync("question", null);
            FakeBackend backend = (FakeBackend)session.Backend;
            backend.Sink.TurnCompleted(new TurnResult());
            backend.Complete();
            session.RefreshExecutionState();
            Assert.AreEqual(ChatExecutionStatus.Idle, session.ExecutionStatus);
        }

        [TestCase(false, "interrupted", "Interrupted")]
        [TestCase(true, "error", "Error")]
        public void FinalStatusSurvivesCompletionBeforeBackendBecomesIdle(
            bool error, string subtype, string expected)
        {
            ChatSession session = _manager.Open(_profile);
            session.SendAsync("question", null);
            FakeBackend backend = (FakeBackend)session.Backend;
            backend.Complete(new TurnResult { IsError = error, Subtype = subtype });
            session.RefreshExecutionState();
            Assert.AreEqual(expected, session.ExecutionStatus.ToString());
        }

        [Test]
        public void AtomicStoreRoundTripsAndReplacesSnapshots()
        {
            string directory = Path.Combine(Path.GetTempPath(), "parley-tabs-" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(directory, "tabs.json");
            try
            {
                ChatTabStore store = new ChatTabStore(path);
                store.Save(new ChatTabState { ActiveSessionId = "first" });
                store.Save(new ChatTabState
                {
                    ActiveSessionId = "second",
                    Tabs = new List<SessionRecord> { new SessionRecord { Id = "second", Draft = "unfinished" } },
                });
                ChatTabState state = store.Load();
                Assert.AreEqual("second", state.ActiveSessionId);
                Assert.AreEqual("unfinished", state.Tabs[0].Draft);
                Assert.IsFalse(File.Exists(path + ".tmp"));
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void PendingNativeForkNeverExposesSourceThreadAsItsOwnIdentity(bool claude)
        {
            ToolCatalog tools = new ToolCatalog(Array.Empty<IParleyTool>());
            RecordingSink sink = new RecordingSink();
            using IAgentBackend backend = claude
                ? new ClaudeCodeBackend(_profile, sink, tools, "source-thread", PermissionMode.Default, true)
                : new CodexBackend(_profile, sink, tools, "source-thread", PermissionMode.Default, true);
            Assert.IsNull(backend.SessionId);
            backend.ClearContextAsync(CancellationToken.None);
            Assert.IsNull(backend.SessionId);
        }

        [Test]
        public void ClaudeForkInitAndReloadBeforeFirstResultCannotRestoreSourceIdentity()
        {
            ParleyProfile profile = ParleyProfile.CreateClaude("Fork", ClaudeAuthMethod.CliDefault);
            SessionRecord record = new SessionRecord
            {
                ProfileId = profile.Id, Kind = profile.Kind, ForkBackendSessionId = "source-thread",
            };
            using ChatSession session = new ChatSession(profile, record, true,
                (selected, saved, sink) => new ClaudeCodeBackend(selected, sink,
                    new ToolCatalog(Array.Empty<IParleyTool>()), saved.ForkBackendSessionId,
                    saved.Mode, true));
            session.OnStateChanged += session.Save;
            MethodInfo receive = typeof(ClaudeCodeBackend).GetMethod("StdoutLineHandler",
                BindingFlags.Instance | BindingFlags.NonPublic);
            receive.Invoke(session.Backend, new object[]
            {
                "{\"type\":\"system\",\"subtype\":\"init\",\"session_id\":\"copy-thread\",\"model\":\"test\"}",
            });
            session.Save();
            SessionRecord restored = JsonConvert.DeserializeObject<SessionRecord>(JsonConvert.SerializeObject(record));
            Assert.IsNull(session.Backend.SessionId);
            Assert.IsNull(restored.BackendSessionId);
            Assert.AreEqual("source-thread", restored.ForkBackendSessionId);
            // Only a result confirms a resumable fork. The source is never exposed as this tab's identity.
            receive.Invoke(session.Backend, new object[]
            {
                "{\"type\":\"result\",\"session_id\":\"copy-thread\",\"subtype\":\"success\",\"is_error\":false,\"num_turns\":1}",
            });
            session.Save();
            Assert.AreEqual("copy-thread", record.BackendSessionId);
            Assert.IsNull(record.ForkBackendSessionId);
        }

        [Test]
        public void AssetRefreshIsRetainedUntilGlobalIdleAndUnlockAndCoalescesRequests()
        {
            bool idle = false;
            bool locked = true;
            int refreshes = 0;
            DeferredAssetRefresh refresh = new DeferredAssetRefresh(() => idle && !locked, () => refreshes++);
            refresh.Request();
            refresh.Tick();
            idle = true;
            refresh.Request();
            refresh.Tick();
            Assert.AreEqual(0, refreshes);
            locked = false;
            refresh.Tick();
            refresh.Tick();
            Assert.AreEqual(1, refreshes);
        }

        [TestCase(true, "Error")]
        [TestCase(false, "Interrupted")]
        public void BackendExitTerminatesOrphanedTasksAndApprovals(bool unexpected, string expected)
        {
            ChatSession session = _manager.Open(_profile);
            IAgentSink sink = session;
            sink.BackgroundTaskChanged(new BackgroundTaskInfo { TaskId = "running", Status = "running" });
            sink.BackgroundTaskChanged(new BackgroundTaskInfo { TaskId = "pending", Status = "pending" });
            sink.BackgroundTaskChanged(new BackgroundTaskInfo { TaskId = "done", Status = "completed" });
            sink.RequestRaised(new PendingRequest { Id = "approval", Input = new JObject(), ToolName = "Bash" });
            Assert.IsTrue(_manager.HasActiveWork);
            sink.Exited("Process stopped", unexpected);
            Assert.IsFalse(_manager.HasActiveWork);
            Assert.IsEmpty(session.OpenRequests);
            Assert.AreEqual(expected, session.ExecutionStatus.ToString());
            Assert.AreEqual("interrupted", session.Record.BackgroundTasks[0].Status);
            Assert.AreEqual("interrupted", session.Record.BackgroundTasks[1].Status);
            Assert.AreEqual("completed", session.Record.BackgroundTasks[2].Status);
            Assert.IsNotEmpty(session.Record.Entries[0].Blocks[0].Resolution);
        }

        [UnityTest]
        public IEnumerator ReloadGuardWaitsForAllHoldersAndDeferredIdle()
        {
            bool previous = ParleyUserSettings.instance.LockReloadDuringTurn;
            object first = new object();
            object second = new object();
            try
            {
                ParleyUserSettings.instance.LockReloadDuringTurn = true;
                ReloadGuard.Acquire(first);
                ReloadGuard.Acquire(second);
                ReloadGuard.Release(first);
                yield return null;
                Assert.IsTrue(ReloadGuard.IsLocked);
                ReloadGuard.Release(second);
                Assert.IsTrue(ReloadGuard.IsLocked);
                // A chained operation reacquires before the delayed unlock can run.
                ReloadGuard.Acquire(first);
                yield return null;
                Assert.IsTrue(ReloadGuard.IsLocked);
                ReloadGuard.Release(first);
                for (int frame = 0; frame < 10 && ReloadGuard.IsLocked; frame++)
                {
                    yield return null;
                }

                Assert.IsFalse(ReloadGuard.IsLocked);
            }
            finally
            {
                ReloadGuard.Release(first);
                ReloadGuard.Release(second);
                ParleyUserSettings.instance.LockReloadDuringTurn = previous;
            }
        }

        private ChatTabManager CreateManager(IChatTabStore store)
        {
            ChatTabManager manager = new ChatTabManager(_factory, store);
            _managers.Add(manager);
            return manager;
        }

        private sealed class MemoryStore : IChatTabStore
        {
            private string _json;
            public ChatTabState Load() => _json == null ? null : JsonConvert.DeserializeObject<ChatTabState>(_json);
            public void Save(ChatTabState state) => _json = JsonConvert.SerializeObject(state);
        }

        private sealed class Factory : IChatSessionFactory
        {
            public ChatSession Create(ParleyProfile profile, SessionRecord record)
            {
                return new ChatSession(profile, record, true,
                    (selected, saved, sink) => new FakeBackend(sink, saved.BackendSessionId));
            }
        }

        private sealed class FakeBackend : IAgentBackend
        {
            private readonly Queue<TaskCompletionSource<bool>> _sends = new();
            public IAgentSink Sink { get; }
            public string ResumeId { get; }
            public int SendCount { get; private set; }
            public string LastText { get; private set; }
            public int StartCount { get; private set; }
            public bool Disposed { get; private set; }
            public bool IsRunning => StartCount > 0;
            public bool IsBusy => _sends.Count > 0;
            public string SessionId => "backend-thread";
            public PermissionMode Mode => PermissionMode.Default;
            public bool HasPendingRestart => false;

            public FakeBackend(IAgentSink sink, string resumeId)
            {
                Sink = sink;
                ResumeId = resumeId;
            }

            public void Dispose()
            {
                Disposed = true;
                while (_sends.Count > 0)
                {
                    _sends.Dequeue().TrySetCanceled();
                }
            }

            public Task StartAsync(CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                StartCount++;
                return Task.CompletedTask;
            }

            public Task SendAsync(UserTurn turn, CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                SendCount++;
                LastText = turn.Text;
                TaskCompletionSource<bool> completion = new TaskCompletionSource<bool>();
                _sends.Enqueue(completion);
                return completion.Task;
            }

            public void Complete(TurnResult result = null)
            {
                // CLI mappers report completion before releasing their busy flag.
                Sink.TurnCompleted(result ?? new TurnResult());
                _sends.Dequeue().SetResult(true);
            }

            public Task ClearContextAsync(CancellationToken token) => Task.CompletedTask;
            public void Interrupt() { }
            public void SetPermissionMode(PermissionMode mode) { }
            public void SetModel(string model) { }
            public void SetEffort(string effort) { }
            public void Respond(PendingRequest request, Decision decision) { }
            public void StopTask(string taskId) { }
            public void SetMcpConfiguration(McpConfiguration configuration) { }
            public void RefreshMcpStatus() { }
            public void SetSkillConfiguration(SkillConfiguration configuration) { }
            public void RefreshSkills() { }
            public void RefreshRateLimits() { }
        }
    }
}
