using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DTech.Parley.Editor;
using DTech.Parley.Editor.Agents.Claude;
using DTech.Parley.Editor.Agents.Codex;
using DTech.Parley.Editor.Agents.Local;
using DTech.Parley.Editor.Orchestration;
using DTech.Parley.Editor.Sessions;
using DTech.Parley.Editor.Skills;
using DTech.Parley.Editor.Tools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class OrchestrationEngineTests
    {
        private ChatTabManager _tabs;
        private OrchestrationEngine _engine;
        private ParleyProfile _profile;
        private SkillRegistry _registry;
        private MemoryStore _store;
        private Summary _summary;
        private ChatSession _parent;
        private int _maxHops;

        [SetUp]
        public void SetUp()
        {
            _profile = ParleyProfile.CreateLocal("Test", LocalPreset.LmStudio);
            _profile.Model = "test-model";
            _registry = new SkillRegistry();
            _registry.Add(new SkillDocument
            {
                Name = "worker", Body = "Solve the task.", PreferredProvider = "current", Model = "current",
                HandoffMode = "auto", AllowedTargets = new List<string> { "skill:worker" },
            }, "test");
            _store = new MemoryStore();
            _summary = new Summary();
            _maxHops = 4;
            _tabs = new ChatTabManager(new Factory(), _store);
            _engine = Engine(_tabs);
            _parent = _tabs.Open(_profile, new SessionRecord
            {
                Kind = _profile.Kind, Mode = PermissionMode.Plan, RoleSkillId = "skill:root",
                RoleInstructions = "Coordinate.", RoleHandoffMode = "auto",
                RoleAllowedTargets = new List<string> { "skill:worker" },
            });
        }

        [TearDown]
        public void TearDown() { _tabs.Dispose(); }

        [Test]
        public async Task RunsChildWithFreshContextAndResumesParentExactlyOnce()
        {
            _parent.SetModel("selected-current-model");
            await _parent.SendAsync("original task", null);
            string receipt = Submit(_parent, "request-1");
            Assert.AreEqual(receipt, Submit(_parent, "request-1"));
            Assert.AreEqual(receipt, Submit(_parent, "other-transport-id"));
            Assert.AreEqual(1, _engine.Runs[0].Delegations.Count);
            _engine.Tick();
            Assert.AreEqual(1, _tabs.Tabs.Count, "Child waits for parent completion.");
            Complete(_parent, "Delegated.");
            _engine.Tick();
            ChatSession child = _tabs.Tabs[1];
            FakeBackend childBackend = Backend(child);
            Assert.AreEqual(_parent.SessionId, child.ParentSessionId);
            Assert.AreEqual(_parent.Mode, child.Mode);
            Assert.AreNotEqual(_parent.SessionId, child.SessionId);
            Assert.AreEqual("skill:worker", child.Record.RoleSkillId);
            Assert.AreEqual("selected-current-model", child.Model);
            Assert.IsNull(child.Record.BackendSessionId);
            StringAssert.Contains("original task", childBackend.LastText);
            StringAssert.Contains("do work", childBackend.LastText);
            Assert.AreSame(_parent, _tabs.Active);
            child.SetMode(PermissionMode.BypassPermissions);
            Assert.AreEqual(PermissionMode.Plan, child.Mode);
            Complete(child, "child final output");
            _engine.Tick();
            Assert.AreEqual(2, Backend(_parent).Sends);
            StringAssert.Contains("child final output", Backend(_parent).LastText);
            Assert.AreEqual(DelegationStatus.Delivered, _engine.Runs[0].Delegations[0].Status);
            _engine.Tick();
            Assert.AreEqual(2, Backend(_parent).Sends);
            Complete(_parent, "all finished");
            _engine.Tick();
            Assert.AreEqual(RunStatus.Completed, _engine.Runs[0].Status);
            Assert.IsFalse(_engine.HasActiveWork);
        }

        [Test]
        public async Task ApprovalAndResultApprovalAreDurableAndIdempotent()
        {
            _parent.Record.RoleHandoffMode = "ask_approval";
            await _parent.SendAsync("task", null);
            Submit(_parent, "approval-1");
            Complete(_parent, "waiting");
            _engine.Tick();
            OrchestrationRun run = _engine.Runs[0];
            DelegationRecord request = run.Delegations[0];
            Assert.AreEqual(DelegationStatus.WaitingApproval, request.Status);
            Assert.AreEqual(1, _tabs.Tabs.Count);
            Assert.IsTrue(_tabs.HasActiveWork);
            Assert.AreEqual(DelegationStatus.WaitingApproval, _store.Load().Runs[0].Delegations[0].Status);
            Assert.IsTrue(_engine.Approve(run.Id, request.Id, true));
            Assert.IsFalse(_engine.Approve(run.Id, request.Id, true));
            _engine.Tick();
            Complete(_tabs.Tabs[1], "review me");
            _engine.Tick();
            Assert.AreEqual(DelegationStatus.WaitingResultApproval, request.Status);
            Assert.AreEqual(1, Backend(_parent).Sends);
            Assert.IsTrue(_engine.Approve(run.Id, request.Id, false));
            _engine.Tick();
            StringAssert.Contains("withheld", Backend(_parent).LastText);
            StringAssert.DoesNotContain("review me", Backend(_parent).LastText);
        }

        [Test]
        public async Task RejectsEmptyTargetsUnknownSkillsAndConflictingIds()
        {
            await _parent.SendAsync("task", null);
            _parent.Record.RoleAllowedTargets.Clear();
            Assert.Throws<InvalidOperationException>(() => Submit(_parent, "one"));
            _parent.Record.RoleAllowedTargets.Add("missing");
            Assert.Throws<InvalidOperationException>(() => _engine.Submit(_parent,
                new DelegationRequest("missing", "missing", "task", "summary")));
            _parent.Record.RoleAllowedTargets.Add("worker");
            Submit(_parent, "one");
            Assert.Throws<InvalidOperationException>(() => _engine.Submit(_parent,
                new DelegationRequest("one", "worker", "different", "last_messages")));
            Assert.AreEqual(1, _engine.Runs[0].Delegations.Count);
        }

        [Test]
        public async Task EnforcesDepthLimitAndKeepsRunLimitStable()
        {
            _maxHops = 1;
            await _parent.SendAsync("task", null);
            _maxHops = 4;
            Submit(_parent, "one");
            Complete(_parent, "waiting");
            _engine.Tick();
            ChatSession child = _tabs.Tabs[1];
            Assert.Throws<InvalidOperationException>(() => Submit(child, "too-deep"));
            Assert.AreEqual(1, _engine.Runs[0].MaxHops);
            Assert.AreEqual(1, _engine.Runs[0].Delegations.Count);
        }

        [Test]
        public async Task SummaryUsesContextServiceAndStopFencesItsLateCompletion()
        {
            _summary.Pending = new TaskCompletionSource<string>();
            await _parent.SendAsync("context to summarize", null);
            _engine.Submit(_parent, new DelegationRequest("summary", "worker", "do work", "summary"));
            Complete(_parent, "waiting");
            _engine.Tick();
            Assert.AreEqual(1, _summary.Calls);
            Assert.AreEqual(DelegationStatus.Preparing, _engine.Runs[0].Delegations[0].Status);
            Assert.IsTrue(_tabs.HasActiveWork);
            _parent.Interrupt();
            _summary.Pending.SetResult("late summary");
            await Task.Yield();
            _engine.Tick();
            Assert.AreEqual(1, _tabs.Tabs.Count);
            Assert.AreEqual(RunStatus.Cancelled, _engine.Runs[0].Status);
            Assert.IsTrue(_summary.Token.IsCancellationRequested);
            Assert.AreEqual(1, Backend(_parent).Sends);
        }

        [Test]
        public async Task StopCancelsChildrenQueuedDelegationsTasksAndLateCallbacks()
        {
            await _parent.SendAsync("task", null);
            Submit(_parent, "one");
            Complete(_parent, "waiting");
            _engine.Tick();
            ChatSession child = _tabs.Tabs[1];
            ((IAgentSink)child).BackgroundTaskChanged(new BackgroundTaskInfo { TaskId = "job", Status = "pending" });
            _parent.Interrupt();
            Backend(child).Sink.TextDelta("late", "late output");
            Backend(child).Sink.TurnCompleted(new TurnResult { ResultText = "late final" });
            _engine.Tick();
            Assert.AreEqual(RunStatus.Cancelled, _engine.Runs[0].Status);
            Assert.AreEqual(DelegationStatus.Cancelled, _engine.Runs[0].Delegations[0].Status);
            Assert.IsFalse(_tabs.HasActiveWork);
            Assert.AreEqual("cancelled", child.Tasks.First().Status);
            Assert.AreEqual(1, Backend(_parent).Sends);
            Assert.Throws<InvalidOperationException>(() => Submit(child, "late-native"));
        }

        [Test]
        public async Task ReloadInterruptsPendingApprovalAndNeverReplays()
        {
            _parent.Record.RoleHandoffMode = "ask_approval";
            await _parent.SendAsync("task", null);
            Submit(_parent, "one");
            Complete(_parent, "waiting");
            _engine.Tick();
            using ChatTabManager restored = new(new Factory(), _store);
            restored.Restore(_ => _profile, _profile);
            using OrchestrationEngine engine = Engine(restored);
            engine.Tick();
            Assert.AreEqual(RunStatus.Interrupted, engine.Runs[0].Status);
            Assert.AreEqual(DelegationStatus.Interrupted, engine.Runs[0].Delegations[0].Status);
            Assert.IsFalse(engine.HasActiveWork);
            Assert.AreEqual(0, Backend(restored.Active).Sends);
            Assert.IsFalse(engine.Approve(engine.Runs[0].Id, engine.Runs[0].Delegations[0].Id, true));
        }

        [Test]
        public async Task NativeBridgesRegisterAndSubmitWithTransportRequestIds()
        {
            await _parent.SendAsync("task", null);
            ToolCatalog tools = _parent.CreateToolCatalog(false);
            JObject arguments = new()
            {
                ["target_skill"] = "worker", ["task_summary"] = "do work", ["context_mode"] = "last_messages",
            };
            CodexToolBridge codex = new(tools, () => new ToolContext
            {
                TurnCorrelationId = _parent.Record.OrchestrationTurnId,
            });
            Assert.IsTrue(codex.Specs()[0]["tools"].Any(item => (string)item["name"] == "delegate_to_agent"));
            JObject response = await codex.CallAsync(new JObject
            {
                ["namespace"] = "unity", ["tool"] = "delegate_to_agent", ["callId"] = "codex-call",
                ["arguments"] = arguments,
            }, CancellationToken.None);
            Assert.IsTrue((bool)response["success"]);
            Assert.AreEqual("codex-call", _engine.Runs[0].Delegations[0].RequestId);
            SdkMcpBridge claude = new(tools, () => new ToolContext
            {
                TurnCorrelationId = _parent.Record.OrchestrationTurnId,
            });
            JObject list = await claude.HandleAsync("unity", new JObject
            {
                ["id"] = 1, ["method"] = "tools/list",
            }, CancellationToken.None);
            Assert.IsTrue(list["result"]["tools"].Any(item => (string)item["name"] == "delegate_to_agent"));
            JObject answer = await claude.HandleAsync("unity", new JObject
            {
                ["id"] = "claude-call", ["method"] = "tools/call",
                ["params"] = new JObject { ["name"] = "delegate_to_agent", ["arguments"] = arguments },
            }, CancellationToken.None);
            Assert.IsFalse((bool)answer["result"]["isError"]);
            Assert.AreEqual(1, _engine.Runs[0].Delegations.Count, "Cross-transport duplicate shares the receipt.");
            Assert.IsTrue(_parent.CreateToolCatalog(true).TryGet("delegate_to_agent", out _));
        }

        [Test]
        public async Task FallbackUsesAuthoritativeTopLevelTextAndIgnoresNestedTools()
        {
            await _parent.SendAsync("task", null);
            IAgentSink sink = _parent;
            string tag = "<parley_delegate target=\"worker\" mode=\"last_messages\">do work</parley_delegate>";
            sink.TextDelta("stream", tag.Substring(0, 20));
            sink.TextDelta("stream", tag.Substring(20));
            sink.BlockFinalized(new BlockFinalizeRequest("stream", BlockKind.Text, null, "Revised plain answer", null, null, null));
            Assert.IsEmpty(_engine.Runs[0].Delegations);
            sink.BlockFinalized(new BlockFinalizeRequest("nested", BlockKind.Text, "native-child", tag, null, null, null));
            Assert.IsEmpty(_engine.Runs[0].Delegations);
            sink.BlockFinalized(new BlockFinalizeRequest("real", BlockKind.Text, null, tag, null, null, null));
            sink.BlockFinalized(new BlockFinalizeRequest("real", BlockKind.Text, null, tag, null, null, null));
            Assert.AreEqual(1, _engine.Runs[0].Delegations.Count);
        }

        [Test]
        public async Task MultipleChildrenResumeOnlyAfterAllResultsAreReady()
        {
            await _parent.SendAsync("task", null);
            Submit(_parent, "one");
            _engine.Submit(_parent, new DelegationRequest("two", "worker", "other work", "last_messages"));
            Complete(_parent, "waiting");
            _engine.Tick();
            Assert.AreEqual(3, _tabs.Tabs.Count);
            Complete(_tabs.Tabs[1], "first result");
            _engine.Tick();
            Assert.AreEqual(1, Backend(_parent).Sends);
            Complete(_tabs.Tabs[2], "second result");
            _engine.Tick();
            StringAssert.Contains("first result", Backend(_parent).LastText);
            StringAssert.Contains("second result", Backend(_parent).LastText);
            Assert.AreEqual(2, Backend(_parent).Sends);
        }

        [Test]
        public async Task NestedChildResumesBeforeItsFinalResultReachesRoot()
        {
            await _parent.SendAsync("task", null);
            Submit(_parent, "root-request");
            Complete(_parent, "waiting for child");
            _engine.Tick();
            ChatSession child = _tabs.Tabs[1];
            Submit(child, "nested-request");
            Complete(child, "waiting for grandchild");
            _engine.Tick();
            ChatSession grandchild = _tabs.Tabs[2];
            Complete(grandchild, "grandchild result");
            _engine.Tick();
            StringAssert.Contains("grandchild result", Backend(child).LastText);
            Assert.AreEqual(1, Backend(_parent).Sends);
            Complete(child, "child consolidated result");
            _engine.Tick();
            StringAssert.Contains("child consolidated result", Backend(_parent).LastText);
            Assert.AreEqual(2, Backend(_parent).Sends);
        }

        [Test]
        public async Task ReloadAfterDeliveryDoesNotRedeliverOrResume()
        {
            await _parent.SendAsync("task", null);
            Submit(_parent, "one");
            Complete(_parent, "waiting");
            _engine.Tick();
            Complete(_tabs.Tabs[1], "child result");
            _engine.Tick();
            using ChatTabManager restored = new(new Factory(), _store);
            restored.Restore(_ => _profile, _profile);
            using OrchestrationEngine engine = Engine(restored);
            engine.Tick();
            Assert.AreEqual(RunStatus.Interrupted, engine.Runs[0].Status);
            Assert.AreEqual(DelegationStatus.Delivered, engine.Runs[0].Delegations[0].Status);
            Assert.AreEqual(0, Backend(restored.Tabs[0]).Sends);
            Assert.AreEqual(0, Backend(restored.Tabs[1]).Sends);
            Assert.AreEqual(ChatExecutionStatus.Interrupted, restored.Tabs[0].ExecutionStatus);
        }

        [Test]
        public async Task DenialAndSummaryFailureReturnExplicitResultsWithoutSpawning()
        {
            _parent.Record.RoleHandoffMode = "ask_approval";
            await _parent.SendAsync("task", null);
            Submit(_parent, "denied");
            Complete(_parent, "waiting");
            OrchestrationRun run = _engine.Runs[0];
            Assert.IsTrue(_engine.Approve(run.Id, run.Delegations[0].Id, false));
            _engine.Tick();
            Assert.AreEqual(1, _tabs.Tabs.Count);
            StringAssert.Contains("denied", Backend(_parent).LastText);
            Complete(_parent, "done");
            _engine.Tick();

            _parent.Record.RoleHandoffMode = "auto";
            _summary.Pending = new TaskCompletionSource<string>();
            _summary.Pending.SetException(new InvalidOperationException("summary service offline"));
            await _parent.SendAsync("next task", null);
            _engine.Submit(_parent, new DelegationRequest("failed-summary", "worker", "work", "summary"));
            Complete(_parent, "waiting again");
            _engine.Tick();
            Assert.AreEqual(1, _tabs.Tabs.Count);
            StringAssert.Contains("summary service offline", Backend(_parent).LastText);
            Assert.AreEqual(DelegationStatus.Delivered, _engine.Runs[1].Delegations[0].Status);
        }

        [Test]
        public async Task ClearAndCloseCancelQueuedWorkflows()
        {
            await _parent.SendAsync("task", null);
            Submit(_parent, "one");
            Complete(_parent, "waiting");
            await _parent.ClearAsync();
            _engine.Tick();
            Assert.AreEqual(RunStatus.Cancelled, _engine.Runs[0].Status);
            Assert.AreEqual(1, _tabs.Tabs.Count);
            Assert.IsNull(_parent.Record.OrchestrationRunId);
            await _parent.SendAsync("another task", null);
            Submit(_parent, "two");
            Complete(_parent, "waiting");
            _tabs.Close(_parent);
            _engine.Tick();
            Assert.AreEqual(RunStatus.Cancelled, _engine.Runs[1].Status);
            Assert.IsFalse(_engine.HasActiveWork);
        }

        [Test]
        public async Task StaleNativeTurnCannotDelegateIntoANewerRun()
        {
            await _parent.SendAsync("old task", null);
            string oldTurn = _parent.Record.OrchestrationTurnId;
            _parent.Interrupt();
            await _parent.SendAsync("new task", null);
            Assert.Throws<InvalidOperationException>(() => _engine.Submit(_parent,
                new DelegationRequest("old-call", "worker", "old work", "last_messages"), oldTurn));
            Assert.IsEmpty(_engine.Runs[1].Delegations);
        }

        [Test]
        public async Task FailedParentNeverStartsItsQueuedChild()
        {
            await _parent.SendAsync("task", null);
            Submit(_parent, "one");
            Backend(_parent).Complete("parent error", true);
            _engine.Tick();
            Assert.AreEqual(1, _tabs.Tabs.Count);
            Assert.AreEqual(RunStatus.Failed, _engine.Runs[0].Status);
        }

        [Test]
        public async Task StopDuringBackendStartupCancelsThePendingSend()
        {
            FakeBackend backend = Backend(_parent);
            backend.Starting = new TaskCompletionSource<bool>();
            Task send = _parent.SendAsync("task", null);
            _parent.Interrupt();
            backend.Starting.SetResult(true);
            await send;
            _engine.Tick();
            Assert.AreEqual(0, backend.Sends);
            Assert.AreEqual(RunStatus.Cancelled, _engine.Runs[0].Status);
            Assert.IsFalse(_tabs.HasActiveWork);
        }

        [Test]
        public async Task RootDowngradeStopsLiveDescendantsAndCancelsQueuedGrandchild()
        {
            _parent.SetMode(PermissionMode.BypassPermissions);
            await _parent.SendAsync("task", null);
            Submit(_parent, "one");
            Complete(_parent, "waiting");
            _engine.Tick();
            ChatSession child = _tabs.Tabs[1];
            Assert.AreEqual(PermissionMode.BypassPermissions, child.Mode);
            Submit(child, "grandchild");
            _parent.SetMode(PermissionMode.Plan);
            _engine.Tick();
            Assert.AreEqual(PermissionMode.Plan, child.Mode);
            Assert.AreEqual(PermissionMode.Plan, child.Backend.Mode);
            Assert.IsTrue(Backend(child).Restricted);
            Assert.AreEqual(RunStatus.Cancelled, _engine.Runs[0].Status);
            Assert.AreEqual(2, _tabs.Tabs.Count);
            Assert.IsFalse(_tabs.HasActiveWork);
            Assert.Throws<InvalidOperationException>(() => Submit(child, "late-grandchild"));
        }

        [Test]
        public async Task SpawnUsesEffectiveAncestorCeilingEvenIfIntermediateModeIsStale()
        {
            _parent.SetMode(PermissionMode.BypassPermissions);
            await _parent.SendAsync("task", null);
            Submit(_parent, "one");
            Complete(_parent, "waiting");
            _engine.Tick();
            ChatSession child = _tabs.Tabs[1];
            Submit(child, "grandchild");
            _parent.Record.Mode = PermissionMode.Plan;
            Complete(child, "waiting for grandchild");
            _engine.Tick();
            Assert.AreEqual(PermissionMode.Plan, _tabs.Tabs[2].Mode);
            Assert.AreEqual(PermissionMode.Plan, _tabs.Tabs[2].Backend.Mode);
        }

        [Test]
        public void PreparedTabSynchronizesItsBackendAfterRecordPermissionsWereNarrowed()
        {
            using ChatSession preview = new(_profile, new SessionRecord
            {
                Kind = _profile.Kind, Mode = PermissionMode.BypassPermissions,
            }, true, (_, saved, sink) => new FakeBackend(sink, saved.Mode));
            preview.Record.Mode = PermissionMode.Plan;
            Assert.AreEqual(PermissionMode.BypassPermissions, preview.Backend.Mode);
            OrchestrationEngine.ApplyChildPermissions(preview, PermissionMode.Plan);
            Assert.AreEqual(PermissionMode.Plan, preview.Record.Mode);
            Assert.AreEqual(PermissionMode.Plan, preview.Backend.Mode);
            Assert.IsTrue(Backend(preview).Restricted);
            Assert.AreEqual(0, Backend(preview).Sends);
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public async Task ChildFailureIsPersistedAndDeliveredAsAnError(bool approval, bool partial)
        {
            _parent.Record.RoleHandoffMode = approval ? "ask_approval" : "auto";
            await _parent.SendAsync("task", null);
            Submit(_parent, "one");
            Complete(_parent, "waiting");
            OrchestrationRun run = _engine.Runs[0];
            DelegationRecord request = run.Delegations[0];
            if (approval) _engine.Approve(run.Id, request.Id, true);
            _engine.Tick();
            Backend(_tabs.Tabs[1]).Complete(new TurnResult
            {
                IsError = true, Subtype = "error", Errors = { "Codex provider failed" },
                ResultText = partial ? "unfinished analysis" : null,
            });
            _engine.Tick();
            Assert.IsTrue(request.IsError);
            Assert.AreEqual("Codex provider failed", request.Error);
            if (approval)
            {
                Assert.AreEqual(DelegationStatus.WaitingResultApproval, request.Status);
                Assert.AreEqual(1, Backend(_parent).Sends);
                _engine.Approve(run.Id, request.Id, true);
                _engine.Tick();
            }
            Assert.AreEqual(DelegationStatus.Delivered, request.Status);
            StringAssert.Contains("Delegation failed", Backend(_parent).LastText);
            StringAssert.Contains("Codex provider failed", Backend(_parent).LastText);
            if (partial)
            {
                StringAssert.Contains("Partial output (incomplete)", Backend(_parent).LastText);
                Assert.AreEqual("unfinished analysis", request.PartialOutput);
            }
            Assert.IsTrue(_store.Load().Runs[0].Delegations[0].IsError);
        }

        private OrchestrationEngine Engine(ChatTabManager tabs) => new(tabs,
            new ContextHandoffService(_summary), () => _registry, () => new[] { _profile }, () => _maxHops);
        private string Submit(ChatSession session, string id) => _engine.Submit(session,
            new DelegationRequest(id, "worker", "do work", "last_messages"));
        private static FakeBackend Backend(ChatSession session) => (FakeBackend)session.Backend;
        private static void Complete(ChatSession session, string output) => Backend(session).Complete(output);

        private sealed class Summary : IContextSummaryGenerator
        {
            public int Calls;
            public CancellationToken Token;
            public TaskCompletionSource<string> Pending;
            public Task<string> GenerateAsync(ParleyProfile profile, string prompt, CancellationToken token)
            {
                Calls++;
                Token = token;
                return Pending?.Task ?? Task.FromResult("summary");
            }
        }

        private sealed class MemoryStore : IChatTabStore
        {
            private string _json;
            public ChatTabState Load() => _json == null ? null : JsonConvert.DeserializeObject<ChatTabState>(_json);
            public void Save(ChatTabState state) { _json = JsonConvert.SerializeObject(state); }
        }

        private sealed class Factory : IChatSessionFactory
        {
            public ChatSession Create(ParleyProfile profile, SessionRecord record) => new(profile, record, true,
                (_, saved, sink) => new FakeBackend(sink, saved.Mode));
        }

        private sealed class FakeBackend : IAgentBackend, IAgentPermissionBoundary
        {
            public IAgentSink Sink { get; }
            public int Sends;
            public string LastText;
            public TaskCompletionSource<bool> Starting;
            public bool Restricted;
            public bool IsRunning => true;
            public bool IsBusy { get; private set; }
            public string SessionId => null;
            public PermissionMode Mode { get; private set; }
            public bool HasPendingRestart => false;
            public FakeBackend(IAgentSink sink, PermissionMode mode) { Sink = sink; Mode = mode; }
            public void Dispose() { IsBusy = false; }
            public Task StartAsync(CancellationToken token) => Task.CompletedTask;
            public async Task SendAsync(UserTurn turn, CancellationToken token)
            {
                if (Starting != null) await Starting.Task;
                token.ThrowIfCancellationRequested();
                Sends++;
                LastText = turn.Text;
                IsBusy = true;
            }
            public void Complete(string output, bool error = false)
            {
                Complete(new TurnResult { ResultText = output, IsError = error });
            }
            public void Complete(TurnResult result)
            {
                IsBusy = false;
                Sink.TurnCompleted(result);
            }
            public void RestrictPermissions(PermissionMode mode)
            {
                Restricted = true;
                Interrupt();
                SetPermissionMode(mode);
            }
            public void Interrupt() { IsBusy = false; }
            public Task ClearContextAsync(CancellationToken token) => Task.CompletedTask;
            public void SetPermissionMode(PermissionMode mode) { Mode = mode; Sink.ModeChanged(mode); }
            public void SetModel(string model) { }
            public void SetEffort(string effort) { }
            public void Respond(PendingRequest request, Decision decision) { }
            public void StopTask(string id) { }
            public void SetMcpConfiguration(McpConfiguration configuration) { }
            public void RefreshMcpStatus() { }
            public void SetSkillConfiguration(SkillConfiguration configuration) { }
            public void RefreshSkills() { }
            public void RefreshRateLimits() { }
        }
    }
}
