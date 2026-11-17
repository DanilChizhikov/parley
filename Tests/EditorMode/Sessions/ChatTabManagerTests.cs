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
using DTech.Parley.Editor.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

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

        [UnityTest]
        public IEnumerator ContextAndCloseButtonFitNarrowAndWideWindowsInBothThemes()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Assert.Ignore("Requires a graphics device to verify editor layout.");
            }
            ChatSession session = _manager.Open(ParleyProfile.CreateCodex("Test", CodexAuthMethod.CliDefault));
            session.Record.Title = new string('W', 100);
            ((IAgentSink)session).CapabilitiesChanged(new BackendCapabilities
            {
                Account = "long-account-name@example.com · ChatGPT subscription",
            });
            ((IAgentSink)session).ContextUsage(56000, 200000);
            ((IAgentSink)session).TurnCompleted(new TurnResult { InputTokens = 150000, OutputTokens = 10000 });
            EditorWindow window = ScriptableObject.CreateInstance<EditorWindow>();
            Font font = Font.CreateDynamicFontFromOSFont(new[] { "Arial", "DejaVu Sans" }, 12);
            try
            {
                window.Show();
                VisualElement root = window.rootVisualElement;
                root.AddToClassList("pl-root");
                root.style.unityFontDefinition = new StyleFontDefinition(font);
                root.style.fontSize = 12;
                root.styleSheets.Add(Resources.Load<StyleSheet>("Parley/Parley"));
                ChatTabBar tabs = new(_manager, _ => false);
                tabs.Refresh();
                root.Add(tabs);
                root.Add(new VisualElement { style = { flexGrow = 1 } });
                StatusBar status = new();
                status.Refresh(session);
                root.Add(status);
                Label context = status.Q<Label>(className: "pl-status__context");
                Button close = tabs.Q<Button>(className: "pl-chat-tab__close");

                foreach (string theme in new[] { "Dark", "Light" })
                {
                    StyleSheet tokens = Resources.Load<StyleSheet>("Parley/ParleyTokens" + theme);
                    root.styleSheets.Add(tokens);
                    foreach (int width in new[] { 360, 900 })
                    {
                        root.style.width = width;
                        window.position = new Rect(0, 0, width, 320);
                        yield return null;
                        yield return null;

                        root.panel.GetType().GetMethod("ValidateLayout",
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                            .Invoke(root.panel, null);

                        Assert.Greater(context.resolvedStyle.width, 0, theme + " " + width);
                        Assert.GreaterOrEqual(context.worldBound.xMin, root.worldBound.xMin);
                        Assert.LessOrEqual(context.worldBound.xMax, root.worldBound.xMax + 1);
                        Assert.GreaterOrEqual(context.resolvedStyle.width,
                            context.MeasureTextSize(context.text, 0, VisualElement.MeasureMode.Undefined,
                                0, VisualElement.MeasureMode.Undefined).x - 1);
                        Assert.AreEqual(20, close.resolvedStyle.width);
                        Assert.LessOrEqual(close.worldBound.xMax, close.parent.worldBound.xMax);
                        Assert.AreEqual(0, close.resolvedStyle.backgroundColor.a);
                        Assert.Greater(close.parent.resolvedStyle.backgroundColor.a, 0);
                    }
                    root.styleSheets.Remove(tokens);
                }
            }
            finally
            {
                window.Close();
                UnityEngine.Object.DestroyImmediate(font);
            }
        }

        [TestCase(56000L, 200000L, "Context: 72% left · 144k / 200k", false)]
        [TestCase(0L, 200000L, "Context: 100% left · 200k / 200k", false)]
        [TestCase(200000L, 200000L, "Context: 0% left · 0 / 200k", true)]
        [TestCase(300000L, 200000L, "Context: 0% left · 0 / 200k", true)]
        [TestCase(-100L, 200000L, "Context: 100% left · 200k / 200k", false)]
        [TestCase(160000L, 200000L, "Context: 20% left · 40k / 200k", true)]
        [TestCase(159999L, 200000L, "Context: 20% left · 40k / 200k", false)]
        [TestCase(100L, 0L, "Context: —", false)]
        [TestCase(100L, -1L, "Context: —", false)]
        public void ContextIndicatorShowsClampedRemainingTokens(long used, long max, string expected, bool warning)
        {
            ChatSession session = _manager.Open(ParleyProfile.CreateClaude("Test", ClaudeAuthMethod.CliDefault));
            ((IAgentSink)session).ContextUsage(used, max);
            StatusBar bar = new();
            bar.Refresh(session);
            Label context = bar.Q<Label>(className: "pl-status__context");

            Assert.AreEqual(expected, context.text);
            Assert.AreEqual(warning, context.ClassListContains("pl-status__context--warning"));
            Assert.IsFalse(bar.Q<Label>(className: "pl-status__usage").text.Contains("ctx"));
        }

        [Test]
        public void ContextIndicatorUsesOnlyTheSelectedChatsReading()
        {
            ParleyProfile profile = ParleyProfile.CreateCodex("Test", CodexAuthMethod.CliDefault);
            ChatSession first = _manager.Open(profile);
            ChatSession second = _manager.Open(profile);
            ((IAgentSink)first).ContextUsage(56000, 200000);
            StatusBar bar = new();
            bar.Refresh(first);
            Label context = bar.Q<Label>(className: "pl-status__context");
            Assert.AreEqual("Context: 72% left · 144k / 200k", context.text);
            StringAssert.Contains("56000 of 200000 tokens used", context.tooltip);
            StringAssert.Contains("144000 tokens remaining", context.tooltip);
            StringAssert.Contains("reported by this chat's backend", context.tooltip);

            bar.Refresh(second);
            Assert.AreEqual("Context: —", context.text);
            StringAssert.Contains("Waiting for this chat's backend", context.tooltip);

            ((IAgentSink)second).ContextUsage(90000, 100000);
            bar.Refresh(second);
            Assert.AreEqual("Context: 10% left · 10k / 100k", context.text);
            bar.Refresh(first);
            Assert.AreEqual("Context: 72% left · 144k / 200k", context.text);
        }

        [Test]
        public void LocalContextIsExplicitlyEstimatedAndTextEstimateStaysInTooltip()
        {
            ChatSession session = _manager.Open(_profile);
            session.Record.Entries.Add(new TranscriptEntry
            {
                Role = EntryRole.User,
                Blocks = { new TranscriptBlock { Kind = BlockKind.Text, Text = "test" } },
            });
            ((IAgentSink)session).ContextUsage(56000, 200000);
            StatusBar bar = new();
            bar.Refresh(session);
            Label context = bar.Q<Label>(className: "pl-status__context");

            Assert.AreEqual("Context: ~72% left · ~144k / 200k", context.text);
            StringAssert.Contains("estimated local context usage", context.tooltip);
            StringAssert.Contains("Text estimate: 4 characters, ~1 tokens", context.tooltip);
            Assert.AreEqual(string.Empty, bar.Q<Label>(className: "pl-status__usage").text);
        }

        [Test]
        public async Task ChangingModelAndClearingChatDiscardOldContextReadings()
        {
            ChatSession session = _manager.Open(_profile);
            ((IAgentSink)session).ContextUsage(56000, 200000);
            session.SetModel("another-model");
            Assert.AreEqual(0, session.ContextUsed);
            Assert.AreEqual(0, session.ContextMax);

            ((IAgentSink)session).ContextUsage(1000, 100000);
            session.SetModel("another-model");
            Assert.AreEqual(100000, session.ContextMax);
            await session.ClearAsync();
            Assert.AreEqual(0, session.ContextUsed);
            Assert.AreEqual(0, session.ContextMax);
        }

        [Test]
        public async Task FirstMessageWaitsForSessionInitialization()
        {
            ChatSession session = _manager.Open(_profile);
            FakeBackend backend = (FakeBackend)session.Backend;
            backend.Startup = new TaskCompletionSource<bool>();
            Task startup = session.EnsureStartedAsync();
            Task send = session.SendAsync("first message", null);

            Assert.AreEqual(0, backend.SendCount);
            Assert.IsFalse(send.IsCompleted);
            backend.Startup.SetResult(true);
            await startup;
            await Task.Yield();
            Assert.AreEqual(1, backend.StartCount);
            Assert.AreEqual(1, backend.SendCount);
            Assert.AreEqual("first message", backend.LastText);
            backend.Complete();
            await send;
        }

        [Test]
        public async Task StartupFailurePreventsFirstMessageFromReachingBackend()
        {
            ChatSession session = _manager.Open(_profile);
            FakeBackend backend = (FakeBackend)session.Backend;
            backend.Startup = new TaskCompletionSource<bool>();
            Task startup = session.EnsureStartedAsync();
            Task send = session.SendAsync("first message", null);
            backend.Startup.SetException(new AgentSetupException("Sign in required"));
            await startup;
            await send;

            Assert.AreEqual(0, backend.SendCount);
            Assert.AreEqual("Sign in required", session.AuthMessage);
            Assert.IsFalse(session.IsBusy);
        }

        [Test]
        public async Task ClosingDuringStartupCancelsTheWaitingFirstMessage()
        {
            ChatSession session = _manager.Open(_profile);
            FakeBackend backend = (FakeBackend)session.Backend;
            backend.Startup = new TaskCompletionSource<bool>();
            Task startup = session.EnsureStartedAsync();
            Task send = session.SendAsync("first message", null);

            _manager.Close(session);
            await startup;
            await send;

            Assert.AreEqual(0, backend.SendCount);
            Assert.IsTrue(backend.Disposed);
            Assert.IsFalse(session.IsBusy);
        }

        [Test]
        public async Task InitializationRunsOnceWithoutSendingDraftOrAutoSkills()
        {
            ChatSession session = _manager.Open(_profile);
            FakeBackend backend = (FakeBackend)session.Backend;
            backend.Startup = new TaskCompletionSource<bool>();
            session.Record.Draft = "unsent draft";
            session.Record.PendingAutoSkills = new List<SkillInvocation> { new() { Name = "review" } };

            Task startup = session.EnsureStartedAsync();
            await session.EnsureStartedAsync();

            Assert.IsTrue(session.IsStarting);
            Assert.AreEqual(1, backend.StartCount);
            Assert.AreEqual(0, backend.SendCount);
            Assert.IsEmpty(session.Messages);
            Assert.AreEqual("unsent draft", session.Record.Draft);
            Assert.AreEqual(1, session.Record.PendingAutoSkills.Count);
            backend.Startup.SetResult(true);
            await startup;
            await session.EnsureStartedAsync();
            Assert.IsFalse(session.IsStarting);
            Assert.AreEqual(1, backend.StartCount);
        }

        [Test]
        public async Task ClosingDuringInitializationCancelsStartupWithoutAnError()
        {
            ChatSession session = _manager.Open(_profile);
            FakeBackend backend = (FakeBackend)session.Backend;
            backend.Startup = new TaskCompletionSource<bool>();
            Task startup = session.EnsureStartedAsync();

            _manager.Close(session);
            await startup;
            await session.EnsureStartedAsync();

            Assert.IsTrue(backend.Disposed);
            Assert.IsFalse(session.IsStarting);
            Assert.AreEqual(1, backend.StartCount);
            Assert.IsEmpty(session.Messages);
        }

        [Test]
        public async Task InitializationFailureShowsTheProfileErrorWithoutSending()
        {
            ChatSession session = _manager.Open(_profile);
            FakeBackend backend = (FakeBackend)session.Backend;
            backend.Startup = new TaskCompletionSource<bool>();
            Task startup = session.EnsureStartedAsync();
            backend.Startup.SetException(new AgentSetupException("Sign in required"));
            await startup;

            Assert.AreEqual("Sign in required", session.AuthMessage);
            Assert.AreEqual(ChatExecutionStatus.Error, session.ExecutionStatus);
            Assert.IsFalse(session.IsStarting);
            Assert.AreEqual(0, backend.SendCount);
        }

        [Test]
        public void TabSelectionHighlightsTheWholeTab()
        {
            ChatSession first = _manager.Open(_profile);
            ChatSession second = _manager.Open(_profile);
            ChatTabBar bar = new ChatTabBar(_manager, _ => true);
            bar.Refresh();

            Button select = bar.Query<Button>(className: "pl-chat-tab__select").First();
            Click(select);
            bar.Refresh();

            Assert.AreSame(first, _manager.Active);
            Assert.IsTrue(select.parent.ClassListContains("pl-chat-tab--active"));
            Assert.IsNotNull(select.parent.Q<Button>(className: "pl-chat-tab__close"));
            Assert.IsFalse(bar.Query<Button>(className: "pl-chat-tab__select").ToList()[1]
                .parent.ClassListContains("pl-chat-tab--active"));
        }

        [Test]
        public void ClosingInactiveTabDoesNotSelectIt()
        {
            ChatSession first = _manager.Open(_profile);
            ChatSession second = _manager.Open(_profile);
            ChatSession activeWhenClosing = null;
            ChatTabBar bar = new ChatTabBar(_manager, session =>
            {
                activeWhenClosing = _manager.Active;
                _manager.Close(session);
                return true;
            });
            bar.Refresh();

            Click(bar.Query<Button>(className: "pl-chat-tab__close").First());
            bar.Refresh();

            Assert.AreSame(second, activeWhenClosing);
            Assert.AreSame(second, _manager.Active);
            Assert.AreEqual(1, bar.Query<Button>(className: "pl-chat-tab__close").ToList().Count);
        }

        [Test]
        public void RejectedTabClosePreservesSelectionAndTabs()
        {
            _manager.Open(_profile);
            ChatSession active = _manager.Open(_profile);
            ChatTabBar bar = new ChatTabBar(_manager, _ => false);
            bar.Refresh();

            Click(bar.Query<Button>(className: "pl-chat-tab__close").First());

            Assert.AreSame(active, _manager.Active);
            Assert.AreEqual(2, _manager.Tabs.Count);
        }

        [Test]
        public void ManualHandoffPreparesNewTabWithoutRunningOrForkingBackend()
        {
            ChatSession source = _manager.Open(_profile);
            source.Record.BackendSessionId = "live-parent-thread";
            ContextSnapshot snapshot = ContextHandoffService.Capture(source.Record, 10);
            ChatSession child = ManualContextHandoff.Prepare(_manager, source, null, _profile, snapshot, false);
            FakeBackend backend = (FakeBackend)child.Backend;
            Assert.AreEqual(0, backend.SendCount);
            Assert.AreEqual(0, backend.StartCount);
            Assert.AreSame(source, ManualContextHandoff.FindParent(_manager, child));
            Assert.IsNull(child.Record.ContextSnapshot);
            Assert.AreEqual(snapshot.Markdown, child.Record.Draft);
            Assert.IsNull(child.Record.ForkBackendSessionId);
            Assert.IsNotEmpty(child.Record.Draft);
            Assert.IsEmpty(child.Record.Entries);
            _manager.Save();
            ChatTabManager restored = CreateManager(_store);
            restored.Restore(id => _profile, _profile);
            Assert.AreEqual(snapshot.Markdown, restored.Active.Record.Draft);
            Assert.AreEqual(source.SessionId, restored.Active.Record.ParentSessionId);
        }

        [Test]
        public async Task PreparedContextIsSentOnlyAfterExplicitSendAndRemainsEditable()
        {
            ChatSession source = _manager.Open(_profile);
            ContextSnapshot snapshot = new(source.SessionId, DateTime.UtcNow, "captured context");
            ChatSession child = ManualContextHandoff.Prepare(_manager, source, null, _profile, snapshot, false);
            child.Record.Draft = "reviewed and edited context";
            FakeBackend backend = (FakeBackend)child.Backend;
            Assert.AreEqual(0, backend.SendCount);
            Task send = child.SendAsync(child.Record.Draft, null);
            Assert.AreEqual("reviewed and edited context", backend.LastText);
            backend.Complete();
            await send;
        }

        [Test]
        public void ManualHandoffAppendsToExistingDraftAndPreservesAttachmentsAndLineage()
        {
            ChatSession source = _manager.Open(_profile);
            ChatSession target = _manager.Open(_profile);
            target.Record.Draft = "existing request";
            target.Record.ParentSessionId = "original-parent";
            target.Record.DraftAttachments.Add(new ChatAttachment());
            ContextSnapshot snapshot = ContextHandoffService.Capture(source.Record, 10);
            ManualContextHandoff.Prepare(_manager, source, target, null, snapshot, false);
            Assert.AreEqual("existing request\n\n" + snapshot.Markdown, target.Record.Draft);
            Assert.AreEqual(1, target.Record.DraftAttachments.Count);
            Assert.AreEqual("original-parent", target.Record.ParentSessionId);
            Assert.AreEqual(0, ((FakeBackend)target.Backend).SendCount);
            Assert.IsEmpty(target.Record.Entries);
        }

        [Test]
        public void BusyOrClosedTargetRejectsHandoffWithoutChangingState()
        {
            ChatSession source = _manager.Open(_profile);
            ChatSession target = _manager.Open(_profile);
            target.Record.Draft = "keep draft";
            target.SendAsync("running request", null);
            _manager.Select(source);
            string before = JsonConvert.SerializeObject(target.Record);
            ContextSnapshot snapshot = ContextHandoffService.Capture(source.Record, 10);
            Assert.Throws<InvalidOperationException>(() =>
                ManualContextHandoff.Prepare(_manager, source, target, null, snapshot, false));
            Assert.AreEqual(before, JsonConvert.SerializeObject(target.Record));
            Assert.AreSame(source, _manager.Active);
            Assert.AreEqual(1, ((FakeBackend)target.Backend).SendCount);
            _manager.Close(target);
            Assert.Throws<InvalidOperationException>(() =>
                ManualContextHandoff.Prepare(_manager, source, target, null, snapshot, false));
            Assert.AreEqual(1, _manager.Tabs.Count);
        }

        [Test]
        public void ResultReturnPreparesParentDraftAndNeverRecreatesClosedParent()
        {
            ChatSession parent = _manager.Open(_profile);
            ChatSession child = _manager.OpenWithContext(_profile,
                ContextHandoffService.Capture(parent.Record, 10));
            ContextSnapshot result = new(child.SessionId, DateTime.UtcNow, "final outcome");
            ManualContextHandoff.Prepare(_manager, child, parent, null, result, true);
            StringAssert.Contains("final outcome", parent.Record.Draft);
            Assert.AreEqual(0, ((FakeBackend)parent.Backend).SendCount);
            _manager.Close(parent);
            Assert.IsNull(ManualContextHandoff.FindParent(_manager, child));
            Assert.Throws<InvalidOperationException>(() =>
                ManualContextHandoff.Prepare(_manager, child, null, _profile, result, true));
            Assert.AreEqual(1, _manager.Tabs.Count);
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
        public async Task InterruptedClaudeAutoSkillKeepsSnapshotForNextSend()
        {
            if (CommandLine.IsWindows)
            {
                Assert.Ignore("Uses /bin/cat as a CLI input sink.");
            }

            ParleyProfile profile = ParleyProfile.CreateClaude("Test", ClaudeAuthMethod.CliDefault);
            ContextSnapshot snapshot = ContextHandoffService.Capture(new SessionRecord
            {
                Entries = { new TranscriptEntry
                {
                    Role = EntryRole.User,
                    Blocks = { new TranscriptBlock { Kind = BlockKind.Text, Text = "source goal", IsFinished = true } },
                } },
            }, 10);
            ClaudeCodeBackend backend = null;
            using ChatSession session = new(profile, new SessionRecord
            {
                Kind = profile.Kind, ContextSnapshot = snapshot,
            }, true, (selected, record, sink) => backend = new ClaudeCodeBackend(selected, sink,
                new ToolCatalog(Array.Empty<IParleyTool>()), null, PermissionMode.Default));
            ChildProcess process = new(new System.Diagnostics.ProcessStartInfo("/bin/cat"));
            Assert.IsTrue(process.Start(out string error), error);
            typeof(ClaudeCodeBackend).GetField("_process", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(backend, process);
            session.Record.PendingAutoSkills = new List<SkillInvocation> { new() { Name = "prepare" } };
            Task interrupted = session.SendAsync("continue", null);
            Assert.IsNotNull(typeof(ClaudeCodeBackend).GetField("_skillTurn", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(backend));
            backend.Interrupt();
            await interrupted;
            Assert.IsFalse(session.Record.ContextDelivered);
            Assert.AreEqual(1, session.Record.PendingAutoSkills.Count);
            Assert.IsNull(typeof(ClaudeCodeBackend).GetField("_lastTurn", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(backend));
            session.Record.PendingAutoSkills.Clear();
            await session.SendAsync("try again", null);
            UserTurn accepted = (UserTurn)typeof(ClaudeCodeBackend)
                .GetField("_lastTurn", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(backend);
            StringAssert.Contains("source goal", accepted.Text);
            Assert.IsTrue(session.Record.ContextDelivered);
        }

        [Test]
        public void ReloadedUnresolvedEditDoesNotBecomeModifiedFileContext()
        {
            TranscriptBlock unresolved = new()
            {
                Kind = BlockKind.ToolUse, ToolName = "Edit",
                Input = new JObject { ["file_path"] = "Assets/Unconfirmed.cs" },
            };
            SessionRecord record = new()
            {
                Kind = _profile.Kind,
                Entries = { new TranscriptEntry { Role = EntryRole.Assistant, Blocks = { unresolved } } },
            };
            ChatSession restored = _manager.Open(_profile, record);
            Assert.IsTrue(unresolved.IsFinished);
            Assert.IsFalse(unresolved.IsError);
            StringAssert.DoesNotContain("Assets/Unconfirmed.cs",
                ContextHandoffService.Capture(restored.Record, 10).Markdown);
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

        private static void Click(Button button)
        {
            typeof(Clickable).GetMethod("SimulateSingleClick", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(button.clickable, new object[] { null, 0 });
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
            public TaskCompletionSource<bool> Startup { get; set; }
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
                Startup?.TrySetCanceled();
                while (_sends.Count > 0)
                {
                    _sends.Dequeue().TrySetCanceled();
                }
            }

            public Task StartAsync(CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                StartCount++;
                return Startup?.Task ?? Task.CompletedTask;
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
