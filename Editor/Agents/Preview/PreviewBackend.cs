using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DTech.Parley.Editor.Agents.Claude;
using DTech.Parley.Editor.Agents.Codex;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Agents.Preview
{
    internal sealed class PreviewBackend : IAgentBackend
    {
        private const string SessionPrefix = "preview-";
        private const string KeyPrefix = "preview-block-";
        private const string ToolIdPrefix = "toolu_preview_";
        private const int StartDelayMs = 600;
        private const int ChunkChars = 14;
        private const int ChunkDelayMs = 20;
        private const int StepDelayMs = 300;
        private const int ToolDelayMs = 600;
        private const int SlowToolDelayMs = 2500;
        private const int LongTurnMs = 20000;
        private const long ContextWindow = 200000;
        private const long ContextPerTurn = 2400;
        private const long ContextAlmostFull = 176000;
        private const double CostPerTurn = 0.0123;
        private const double SessionLimitUsed = 38.0;
        private const double WeeklyLimitUsed = 61.0;
        private const double SessionLimitPerTurn = 1.5;
        private const double WeeklyLimitPerTurn = 0.2;
        private const double SessionResetMinutes = 133.0;
        private const double WeeklyResetDays = 3.0;

        private static readonly Dictionary<string, PreviewScenario> _commands = new (StringComparer.OrdinalIgnoreCase)
        {
            ["showcase"] = PreviewScenario.Showcase,
            ["markdown"] = PreviewScenario.Markdown,
            ["thinking"] = PreviewScenario.Thinking,
            ["tools"] = PreviewScenario.Tools,
            ["edits"] = PreviewScenario.Edits,
            ["subagent"] = PreviewScenario.Subagent,
            ["permission"] = PreviewScenario.Permission,
            ["question"] = PreviewScenario.Question,
            ["plan"] = PreviewScenario.Plan,
            ["enter-plan"] = PreviewScenario.EnterPlan,
            ["todos"] = PreviewScenario.TodosAndTasks,
            ["notices"] = PreviewScenario.Notices,
            ["sign-in"] = PreviewScenario.SignIn,
            ["crash"] = PreviewScenario.Crash,
            ["long-turn"] = PreviewScenario.LongTurn,
            ["context-full"] = PreviewScenario.ContextFull,
            ["restart-hint"] = PreviewScenario.RestartHint,
        };

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

        private readonly ParleyProfile _profile;
        private readonly IAgentSink _sink;
        private readonly Dictionary<string, TaskCompletionSource<Decision>> _decisions = new ();
        private readonly Dictionary<string, BackgroundTaskInfo> _tasks = new ();
        private readonly CancellationTokenSource _lifetime = new ();

        private CancellationTokenSource _turn;
        private McpConfiguration _mcp = new ();
        private SkillConfiguration _skills = new ();
        private string _mcpFingerprint;
        private string _skillFingerprint;
        private int _sequence;
        private long _contextUsed = 18000;
        private double _cost;
        private double _sessionLimitUsed = SessionLimitUsed;
        private double _weeklyLimitUsed = WeeklyLimitUsed;
        private DateTimeOffset _limitsStartedAt;
        private bool _restartPending;
        private bool _disposed;

        public PreviewBackend(ParleyProfile profile, IAgentSink sink, PermissionMode mode)
        {
            _profile = profile;
            _sink = sink;
            Mode = mode;
            SessionId = SessionPrefix + Guid.NewGuid().ToString("N");
        }

        public bool IsRunning { get; private set; }

        public bool IsBusy => _turn != null;

        public string SessionId { get; }

        public PermissionMode Mode { get; private set; }

        public bool HasPendingRestart => _restartPending;

        public static string CommandFor(PreviewScenario scenario)
        {
            foreach (KeyValuePair<string, PreviewScenario> pair in _commands)
            {
                if (pair.Value == scenario)
                {
                    return "/" + pair.Key;
                }
            }

            return "/showcase";
        }

        public static string Describe(PreviewScenario scenario)
        {
            return scenario switch
            {
                PreviewScenario.Showcase => "Everything below, one after another",
                PreviewScenario.Markdown => "Headings, lists, tables, quotes and code",
                PreviewScenario.Thinking => "A streamed thinking block",
                PreviewScenario.Tools => "Read, search, shell, web, MCP and skill tool cards",
                PreviewScenario.Edits => "Edit, multi-edit, write and patch diffs",
                PreviewScenario.Subagent => "A subagent with nested tool calls",
                PreviewScenario.Permission => "Permission requests for a command, an edit and a web fetch",
                PreviewScenario.Question => "A question card with single and multiple choice",
                PreviewScenario.Plan => "A plan waiting for approval",
                PreviewScenario.EnterPlan => "The agent asks to switch to plan mode",
                PreviewScenario.TodosAndTasks => "Todo list and background tasks in the side panel",
                PreviewScenario.Notices => "Info, warning and error notices",
                PreviewScenario.SignIn => "The sign-in card",
                PreviewScenario.Crash => "The agent exits unexpectedly",
                PreviewScenario.LongTurn => "A slow command, so you can try Stop",
                PreviewScenario.ContextFull => "Context window almost full",
                _ => "The restart hint in the MCP and Skills tabs",
            };
        }

        public async Task StartAsync(CancellationToken token)
        {
            if (IsRunning)
            {
                return;
            }

            await Task.Delay(StartDelayMs, token);
            IsRunning = true;
            _sink.CapabilitiesChanged(BuildCapabilities());
            _sink.SessionStarted(new SessionInfo { SessionId = SessionId, Mode = Mode, Cwd = ProjectPaths.Root });
            ReportMcp();
            ReportSkills();
            _sink.ContextUsage(_contextUsed, ContextWindow);
            _limitsStartedAt = DateTimeOffset.Now;
            ReportRateLimits();
        }

        public async Task SendAsync(UserTurn turn, CancellationToken token)
        {
            if (!IsRunning)
            {
                await StartAsync(token);
            }

            if (_restartPending)
            {
                _restartPending = false;
                ReportMcp();
                ReportSkills();
                _sink.Notice(NoticeLevel.Info, "Preview agent restarted with the new MCP and skill settings.");
            }

            string text = (turn.Text ?? string.Empty).Trim();
            if (TryParse(text, out PreviewScenario scenario))
            {
                await PlayAsync(turnToken => RunAsync(scenario, turnToken), token);
                return;
            }

            await PlayAsync(turnToken => ReplyAsync(turn, turnToken), token);
        }

        public void Interrupt()
        {
            _turn?.Cancel();
        }

        public void SetPermissionMode(PermissionMode mode)
        {
            Mode = mode;
            _sink.ModeChanged(mode);
        }

        public void SetModel(string model)
        {
        }

        public void SetEffort(string effort)
        {
        }

        public void Respond(PendingRequest request, Decision decision)
        {
            if (_decisions.TryGetValue(request.Id, out TaskCompletionSource<Decision> source))
            {
                _decisions.Remove(request.Id);
                source.TrySetResult(decision);
            }
        }

        public void StopTask(string taskId)
        {
            if (_tasks.TryGetValue(taskId, out BackgroundTaskInfo task))
            {
                task.Status = "stopped";
                _sink.BackgroundTaskChanged(task);
            }
        }

        public void SetMcpConfiguration(McpConfiguration configuration)
        {
            _mcp = configuration ?? new McpConfiguration();
            string fingerprint = Fingerprint(_mcp);
            bool changed = _mcpFingerprint != null && fingerprint != _mcpFingerprint;
            _mcpFingerprint = fingerprint;
            if (IsRunning && changed)
            {
                _restartPending = true;
            }
        }

        public void RefreshMcpStatus()
        {
            if (IsRunning)
            {
                ReportMcp();
            }
        }

        public void SetSkillConfiguration(SkillConfiguration configuration)
        {
            _skills = configuration ?? new SkillConfiguration();
            string fingerprint = _skills.Fingerprint();
            bool changed = _skillFingerprint != null && fingerprint != _skillFingerprint;
            _skillFingerprint = fingerprint;
            if (IsRunning && changed)
            {
                _restartPending = true;
            }
        }

        public void RefreshSkills()
        {
            if (IsRunning)
            {
                ReportSkills();
            }
        }

        public void RefreshRateLimits()
        {
            if (IsRunning)
            {
                ReportRateLimits();
            }
        }

        public void Dispose()
        {
            _disposed = true;
            _turn?.Cancel();
            _lifetime.Cancel();
        }

        private static bool TryParse(string text, out PreviewScenario scenario)
        {
            scenario = PreviewScenario.Showcase;
            if (!text.StartsWith("/", StringComparison.Ordinal))
            {
                return false;
            }

            int space = text.IndexOf(' ');
            string name = space < 0 ? text.Substring(1) : text.Substring(1, space - 1);
            return _commands.TryGetValue(name, out scenario);
        }

        private static string Fingerprint(McpConfiguration configuration)
        {
            StringBuilder builder = new StringBuilder();
            foreach (McpServerLaunch server in configuration.Servers)
            {
                builder.Append(server.Name).Append(';');
            }

            builder.Append('|');
            foreach (string name in configuration.DisabledExternal)
            {
                builder.Append(name).Append(';');
            }

            return builder.ToString();
        }

        private static JObject EditInput(string path, string oldText, string newText)
        {
            return new JObject { ["file_path"] = path, ["old_string"] = oldText, ["new_string"] = newText };
        }

        private BackendCapabilities BuildCapabilities()
        {
            BackendCapabilities capabilities = new BackendCapabilities { Account = PreviewSamples.Account };
            capabilities.Models.Add(new ModelOption { Value = "opus", DisplayName = "Opus", Description = "Most capable for complex work" });
            capabilities.Models.Add(new ModelOption { Value = "sonnet", DisplayName = "Sonnet", Description = "Fast and capable for everyday tasks" });
            capabilities.Models.Add(new ModelOption { Value = "fable", DisplayName = "Fable", Description = "Preview model" });
            capabilities.Models.Add(new ModelOption
            {
                Value = "preview-model-with-a-very-long-identifier-2026-10-22",
                DisplayName = "preview-model-with-a-very-long-identifier-2026-10-22",
                Description = "Shows how long model names are cut in the toolbar",
            });

            capabilities.Modes.AddRange(_profile.Kind == ProfileKind.ClaudeCode ? _claudeModes : _agentModes);
            if (_profile.Kind != ProfileKind.Local)
            {
                capabilities.Efforts.AddRange(new[] { "low", "medium", "high", "xhigh", "max" });
            }

            foreach (KeyValuePair<string, PreviewScenario> pair in _commands)
            {
                capabilities.Commands.Add(new SlashCommandInfo { Name = pair.Key, Description = "Preview: " + Describe(pair.Value) });
            }

            return capabilities;
        }

        private void ReportRateLimits()
        {
            if (_profile.Kind == ProfileKind.Local)
            {
                return;
            }

            RateLimitWindow session = new RateLimitWindow(_sessionLimitUsed, _limitsStartedAt.AddMinutes(SessionResetMinutes));
            RateLimitWindow weekly = new RateLimitWindow(_weeklyLimitUsed, _limitsStartedAt.AddDays(WeeklyResetDays));
            _sink.RateLimitsChanged(new RateLimits(session, weekly, false, DateTimeOffset.Now));
        }

        private void ReportMcp()
        {
            List<McpServerStatus> servers = new List<McpServerStatus>();
            int index = 0;
            foreach (McpServerLaunch launch in _mcp.Servers)
            {
                McpConnectionState state = (index++ % 4) switch
                {
                    0 => McpConnectionState.Connected,
                    1 => McpConnectionState.Pending,
                    2 => McpConnectionState.Failed,
                    _ => McpConnectionState.NeedsAuth,
                };

                servers.Add(new McpServerStatus
                {
                    Name = launch.Name,
                    State = state,
                    ToolCount = state == McpConnectionState.Connected ? 5 : -1,
                    Error = state == McpConnectionState.Failed ? "spawn npx ENOENT: the command was not found on PATH." : null,
                });
            }

            if (_profile.Kind != ProfileKind.Local)
            {
                servers.Add(External("github", McpConnectionState.Connected, 12, null));
                servers.Add(External("linear", McpConnectionState.NeedsAuth, -1, null));
                servers.Add(External("sentry", McpConnectionState.Failed, -1, "Connection closed before the server answered initialize."));
            }

            _sink.McpStatusChanged(servers);
        }

        private McpServerStatus External(string name, McpConnectionState state, int tools, string error)
        {
            bool disabled = _mcp.DisabledExternal.Contains(name);
            return new McpServerStatus
            {
                Name = name,
                IsExternal = true,
                State = disabled ? McpConnectionState.Disabled : state,
                ToolCount = disabled ? -1 : tools,
                Error = disabled ? null : error,
            };
        }

        private void ReportSkills()
        {
            List<SkillInfo> skills = new List<SkillInfo>();
            foreach (string name in _skills.LibraryNames)
            {
                skills.Add(new SkillInfo { Name = name, IsLibrary = true, Description = "From your Parley skill library." });
            }

            skills.Add(new SkillInfo { Name = "commit", Description = "Create a git commit with a short conventional message.", Path = "~/.claude/skills/commit/SKILL.md" });
            skills.Add(new SkillInfo { Name = "review-pr", ArgumentHint = "[number]", Description = "Review a pull request and leave one comment per finding.", Path = "~/.claude/skills/review-pr/SKILL.md" });
            skills.Add(new SkillInfo
            {
                Name = "pdf",
                Description = "Read, fill and merge PDF documents. This description is long on purpose to show how the panel cuts it and shows the rest in a tooltip.",
                Path = "~/.claude/plugins/documents/skills/pdf/SKILL.md",
            });

            skills.Add(new SkillInfo { Name = "helper", Description = "Helper from the user folder.", Path = "~/.claude/skills/helper/SKILL.md" });
            skills.Add(new SkillInfo { Name = "helper", Description = "Helper from a plugin with the same name.", Path = "~/.claude/plugins/tools/skills/helper/SKILL.md" });
            _sink.SkillsChanged(skills);
        }

        private async Task PlayAsync(Func<CancellationToken, Task> play, CancellationToken token)
        {
            _turn?.Cancel();
            CancellationTokenSource turn = CancellationTokenSource.CreateLinkedTokenSource(token, _lifetime.Token);
            _turn = turn;
            bool interrupted = false;
            DateTime started = DateTime.UtcNow;
            try
            {
                await play(turn.Token);
            }
            catch (OperationCanceledException)
            {
                interrupted = true;
            }
            finally
            {
                if (_turn == turn)
                {
                    _turn = null;
                }

                turn.Dispose();
            }

            if (_disposed)
            {
                return;
            }

            if (interrupted)
            {
                _sink.Notice(NoticeLevel.Warning, "Interrupted by user.");
            }

            _contextUsed = Math.Min(ContextWindow, _contextUsed + ContextPerTurn);
            _sink.ContextUsage(_contextUsed, ContextWindow);
            _cost += CostPerTurn;
            _sessionLimitUsed = Math.Min(100.0, _sessionLimitUsed + SessionLimitPerTurn);
            _weeklyLimitUsed = Math.Min(100.0, _weeklyLimitUsed + WeeklyLimitPerTurn);
            ReportRateLimits();
            _sink.UsageReported(new UsageReport(null, null, null, 1200, 400, 9600, 380, 0));
            _sink.TurnCompleted(new TurnResult
            {
                IsError = interrupted,
                DurationMs = (long)(DateTime.UtcNow - started).TotalMilliseconds,
                NumTurns = 1,
                CostUsd = _profile.Kind == ProfileKind.Local ? null : (double?)_cost,
                InputTokens = 1200,
                OutputTokens = 380,
                CacheReadTokens = 9600,
                CacheCreationTokens = 400,
            });
        }

        private Task RunAsync(PreviewScenario scenario, CancellationToken token)
        {
            return scenario switch
            {
                PreviewScenario.Showcase => ShowcaseAsync(token),
                PreviewScenario.Markdown => TextAsync(PreviewSamples.Markdown, token),
                PreviewScenario.Thinking => ThinkingAsync(token),
                PreviewScenario.Tools => ToolsAsync(token),
                PreviewScenario.Edits => EditsAsync(token),
                PreviewScenario.Subagent => SubagentAsync(token),
                PreviewScenario.Permission => PermissionAsync(token, false),
                PreviewScenario.Question => QuestionAsync(token),
                PreviewScenario.Plan => PlanAsync(token),
                PreviewScenario.EnterPlan => EnterPlanAsync(token),
                PreviewScenario.TodosAndTasks => TodosAsync(token),
                PreviewScenario.Notices => NoticesAsync(token),
                PreviewScenario.SignIn => SignInAsync(token),
                PreviewScenario.Crash => CrashAsync(token),
                PreviewScenario.LongTurn => LongTurnAsync(token),
                PreviewScenario.ContextFull => ContextFullAsync(token),
                _ => RestartHintAsync(token),
            };
        }

        private async Task ShowcaseAsync(CancellationToken token)
        {
            await ThinkingAsync(token);
            await TextAsync(PreviewSamples.Markdown, token);
            await ToolsAsync(token);
            await EditsAsync(token);
            await SubagentAsync(token);
            await TodosAsync(token);
            await NoticesAsync(token);
            await PermissionAsync(token, true);
            await QuestionAsync(token);
            await PlanAsync(token);
        }

        private async Task ReplyAsync(UserTurn turn, CancellationToken token)
        {
            foreach (SkillInvocation skill in turn.Skills)
            {
                await ToolAsync(token, "Skill", new JObject { ["skill"] = skill.Name, ["args"] = skill.Arguments }, "Launching skill: " + skill.Name);
            }

            if (turn.Attachments.Count > 0)
            {
                await TextAsync("Got " + turn.Attachments.Count + " attachment(s). They show above as chips in your message.", token);
            }

            string quoted = string.IsNullOrEmpty(turn.Text) ? "(empty)" : turn.Text.Replace("\n", "\n> ");
            StringBuilder reply = new StringBuilder(PreviewSamples.Reply).Append(quoted).Append("\n\nScenarios:\n\n");
            foreach (KeyValuePair<string, PreviewScenario> pair in _commands)
            {
                reply.Append("- `/").Append(pair.Key).Append("`: ").Append(Describe(pair.Value)).Append('\n');
            }

            await TextAsync(reply.ToString(), token);
        }

        private async Task ThinkingAsync(CancellationToken token)
        {
            await TextAsync(PreviewSamples.Thinking, token, BlockKind.Thinking);
            await TextAsync("Plan: read `PlayerHealth`, clamp the damage, then run the tests.", token);
        }

        private async Task ToolsAsync(CancellationToken token)
        {
            await TextAsync("Let me look at the code first.", token);
            await ToolAsync(token, "Read", new JObject { ["file_path"] = PreviewSamples.ScriptPath }, PreviewSamples.ReadResult);
            await ToolAsync(token, "Grep", new JObject { ["pattern"] = "TakeDamage", ["path"] = "Assets/Scripts", ["output_mode"] = "content" }, PreviewSamples.GrepResult);
            await ToolAsync(token, "Glob", new JObject { ["pattern"] = "**/*Health*.cs" }, PreviewSamples.ScriptPath + "\nAssets/Tests/PlayerHealthTests.cs");
            await ToolAsync(token, "Bash", new JObject { ["command"] = "git status --short", ["description"] = "Show working tree status" }, PreviewSamples.GitStatus);
            await ToolAsync(token, "Bash", new JObject { ["command"] = "dotnet test Tests.csproj", ["description"] = "Run the tests" }, PreviewSamples.TestFailure, true,
                null, SlowToolDelayMs);
            await ToolAsync(token, "WebSearch", new JObject { ["query"] = "Unity Mathf.Max float" },
                "1. Mathf.Max - Unity Scripting API\n2. Clamping values in Unity - Unity Learn");
            await ToolAsync(token, "WebFetch", new JObject { ["url"] = "https://docs.unity3d.com/ScriptReference/Mathf.Max.html", ["prompt"] = "What does it return?" },
                "Returns the largest of two or more values.");
            await ToolAsync(token, SdkMcpBridge.ToolPrefix + "console_read", new JObject { ["level"] = "error" }, "No errors in the console.");
            await ToolAsync(token, "mcp__github__search_issues", new JObject { ["query"] = "player health" }, "#42 Player health goes negative\n#57 Add regeneration");
            await ToolAsync(token, "Skill", new JObject { ["skill"] = "commit" }, "Launching skill: commit");
            await ToolAsync(token, "CustomTool", new JObject { ["target"] = "PlayerHealth", ["depth"] = 2, ["verbose"] = true }, "{\"ok\": true}");
            await TextAsync("Found the method. The tests fail because of a typo in `_curent`.", token);
        }

        private async Task EditsAsync(CancellationToken token)
        {
            await ToolAsync(token, "Edit", EditInput(PreviewSamples.ScriptPath, PreviewSamples.OldMethod, PreviewSamples.NewMethod),
                "The file " + PreviewSamples.ScriptPath + " has been updated.");
            JObject multiEdit = new JObject
            {
                ["file_path"] = PreviewSamples.ScriptPath,
                ["edits"] = new JArray
                {
                    new JObject { ["old_string"] = "private float _current;", ["new_string"] = "private float _current;\n    public event System.Action<float> OnDamaged;" },
                    new JObject { ["old_string"] = "using UnityEngine;", ["new_string"] = "using System;\nusing UnityEngine;" },
                },
            };

            await ToolAsync(token, "MultiEdit", multiEdit, "Applied 2 edits.");
            await ToolAsync(token, "Write", new JObject { ["file_path"] = PreviewSamples.ConfigPath, ["content"] = PreviewSamples.ConfigContent },
                "File created successfully at: " + PreviewSamples.ConfigPath);
            await ToolAsync(token, "Write", new JObject { ["file_path"] = PreviewSamples.ScriptPath, ["content"] = PreviewSamples.ReadResult.Replace("_current -= amount;", "_current = Mathf.Max(0.0f, _current - amount);") },
                "The file " + PreviewSamples.ScriptPath + " has been overwritten.", false, null, ToolDelayMs, new JObject { ["originalFile"] = PreviewSamples.ReadResult });
            JObject patch = new JObject
            {
                ["changes"] = new JArray
                {
                    new JObject { ["path"] = PreviewSamples.ScriptPath, ["kind"] = "update", ["diff"] = PreviewSamples.Patch },
                    new JObject { ["path"] = PreviewSamples.ConfigPath, ["kind"] = "add", ["diff"] = "+" + PreviewSamples.ConfigContent.Replace("\n", "\n+") },
                },
            };

            await ToolAsync(token, CodexApprovals.ApplyPatchTool, patch, "Success. Updated the following files:\nM " + PreviewSamples.ScriptPath + "\nA " + PreviewSamples.ConfigPath);
            await ToolAsync(token, "Edit", EditInput(PreviewSamples.ScriptPath, "_regen", "_regeneration"),
                "String to replace not found in file.\nString: _regen", true);
        }

        private async Task SubagentAsync(CancellationToken token)
        {
            await TextAsync("I'll ask a subagent to find every damage source.", token);
            JObject input = new JObject { ["description"] = "Find all damage sources", ["subagent_type"] = "Explore", ["prompt"] = "List every call to TakeDamage." };
            string id = await StartToolAsync(token, "Task", input, null);
            BackgroundTaskInfo task = Track(new BackgroundTaskInfo
            {
                TaskId = "agent-" + id,
                Description = "Find all damage sources",
                TaskType = "Explore agent",
                Status = "running",
                ToolUseId = id,
            });

            await ToolAsync(token, "Grep", new JObject { ["pattern"] = "TakeDamage\\(", ["path"] = "Assets" }, PreviewSamples.GrepResult, false, id);
            task.LastToolName = "Grep";
            _sink.BackgroundTaskChanged(task);
            await ToolAsync(token, "Read", new JObject { ["file_path"] = "Assets/Scripts/Enemies/EnemyAttack.cs" }, "_target.TakeDamage(_damage);", false, id);
            await TextAsync("Two callers outside the player: `EnemyAttack` and the tests.", token, BlockKind.Text, id);
            FinishTool(id, "Found 3 damage sources:\n\n- `EnemyAttack.Hit` (melee)\n- `Projectile.OnTriggerEnter` (ranged)\n- `PlayerHealthTests` (tests only)", false);
            task.Status = "completed";
            task.Summary = "Found 3 damage sources.";
            _sink.BackgroundTaskChanged(task);
        }

        private async Task PermissionAsync(CancellationToken token, bool single)
        {
            await TextAsync("I need to push the branch.", token);
            PendingRequest push = Request("Bash", new JObject { ["command"] = "git push origin feature/clamp-damage", ["description"] = "Push the branch" });
            push.Suggestions = PreviewSamples.BashSuggestions();
            push.DecisionReason = "Commands that change the remote need your approval.";
            Decision decision = await AskAsync(push, token);
            if (decision.Allow)
            {
                await ToolAsync(token, "Bash", push.Input, "To github.com:example/game.git\n * [new branch]      feature/clamp-damage -> feature/clamp-damage");
            }
            else
            {
                await TextAsync("Not pushing. " + (string.IsNullOrEmpty(decision.Message) ? string.Empty : "You said: *" + decision.Message + "*"), token);
            }

            if (single)
            {
                return;
            }

            PendingRequest edit = Request("Edit", EditInput(PreviewSamples.ScriptPath, PreviewSamples.OldMethod, PreviewSamples.NewMethod));
            decision = await AskAsync(edit, token);
            await TextAsync(decision.Allow ? "Edit applied." : "Edit skipped.", token);
            PendingRequest fetch = Request("WebFetch", new JObject { ["url"] = "https://docs.unity3d.com/Manual/UIElements.html", ["prompt"] = "Summarize USS selectors" });
            fetch.Description = "Fetch a page from docs.unity3d.com";
            decision = await AskAsync(fetch, token);
            await TextAsync(decision.Allow ? "Fetched the page." : "Skipped the page.", token);
            PendingRequest write = Request("Write", new JObject { ["file_path"] = "/etc/hosts", ["content"] = "127.0.0.1 localhost" });
            write.Title = "Write outside the project?";
            write.BlockedPath = "/etc/hosts";
            write.DecisionReason = "The path is outside the allowed directories.";
            decision = await AskAsync(write, token);
            await TextAsync(decision.Allow ? "Wrote the file." : "Left the file alone.", token);
        }

        private async Task QuestionAsync(CancellationToken token)
        {
            await TextAsync("A couple of questions before I continue.", token);
            Decision decision = await AskAsync(Request(PendingRequest.AskUserQuestionTool, PreviewSamples.Questions()), token);
            JObject updated = decision.UpdatedInput;
            string response = (string)updated?["response"];
            if (!string.IsNullOrEmpty(response))
            {
                await TextAsync("You replied in your own words:\n\n> " + response, token);
                return;
            }

            if (!decision.Allow || !(updated?["answers"] is JObject answers))
            {
                await TextAsync("Questions dismissed; I'll use the defaults.", token);
                return;
            }

            StringBuilder text = new StringBuilder("Your answers:\n\n");
            foreach (JProperty answer in answers.Properties())
            {
                text.Append("- **").Append(answer.Name).Append("** ").Append(answer.Value.Type == JTokenType.Array ? string.Join(", ", answer.Value.ToObject<string[]>()) : (string)answer.Value)
                    .Append('\n');
            }

            await TextAsync(text.ToString(), token);
        }

        private async Task PlanAsync(CancellationToken token)
        {
            await ToolAsync(token, "Read", new JObject { ["file_path"] = PreviewSamples.ScriptPath }, PreviewSamples.ReadResult);
            Decision decision = await AskAsync(Request(PendingRequest.ExitPlanModeTool, new JObject { ["plan"] = PreviewSamples.Plan }), token);
            if (!decision.Allow)
            {
                await TextAsync("I'll keep planning." + (string.IsNullOrEmpty(decision.Message) ? string.Empty : " Noted: *" + decision.Message + "*"), token);
                return;
            }

            if (decision.NextMode.HasValue)
            {
                SetPermissionMode(decision.NextMode.Value);
            }

            await TextAsync("Plan approved. Starting with step 1.", token);
            await ToolAsync(token, "Edit", EditInput(PreviewSamples.ScriptPath, PreviewSamples.OldMethod, PreviewSamples.NewMethod),
                "The file " + PreviewSamples.ScriptPath + " has been updated.");
        }

        private async Task EnterPlanAsync(CancellationToken token)
        {
            await TextAsync("This touches several systems; planning first would help.", token);
            Decision decision = await AskAsync(Request(PendingRequest.EnterPlanModeTool, new JObject()), token);
            if (decision.Allow)
            {
                SetPermissionMode(PermissionMode.Plan);
                await TextAsync("Switched to plan mode. Exploring before changing anything.", token);
                return;
            }

            await TextAsync("Continuing without a plan.", token);
        }

        private async Task TodosAsync(CancellationToken token)
        {
            for (int completed = 0; completed <= 4; completed++)
            {
                await ToolAsync(token, "TodoWrite", new JObject { ["todos"] = PreviewSamples.Todos(completed, completed < 4 ? 1 : 0) }, "Todos updated.");
            }

            BackgroundTaskInfo watch = Track(new BackgroundTaskInfo
            {
                TaskId = "bash-watch",
                Description = "npm run watch",
                TaskType = "Background command",
                Status = "running",
                LastToolName = "Bash",
                Summary = "Watching 128 files for changes…",
            });

            Track(new BackgroundTaskInfo
            {
                TaskId = "agent-docs",
                Description = "Summarize the combat docs",
                TaskType = "General agent",
                Status = "completed",
                Summary = "The combat docs describe three damage types: melee, ranged and area.",
            });

            Track(new BackgroundTaskInfo
            {
                TaskId = "bash-build",
                Description = "Build the WebGL player",
                TaskType = "Background command",
                Status = "failed",
                Summary = "Build failed: 2 shader errors.",
            });

            await TextAsync("The side panel's **Todos** and **Tasks** tabs now have content. `npm run watch` keeps running: stop it from the Tasks tab.", token);
            watch.LastToolName = "Bash";
            _sink.BackgroundTaskChanged(watch);
        }

        private async Task NoticesAsync(CancellationToken token)
        {
            await TextAsync("Notices follow.", token);
            _sink.Notice(NoticeLevel.Info, "Conversation compacted to free up context.");
            await Task.Delay(StepDelayMs, token);
            _sink.Notice(NoticeLevel.Warning, "Rate limited: retrying in 5 seconds.");
            await Task.Delay(StepDelayMs, token);
            _sink.Notice(NoticeLevel.Error, "API Error: 529 {\"type\":\"error\",\"error\":{\"type\":\"overloaded_error\",\"message\":\"Overloaded\"}}");
        }

        private async Task SignInAsync(CancellationToken token)
        {
            await TextAsync("Pretending the profile is signed out.", token);
            _sink.AuthRequired(_profile.Kind == ProfileKind.Local
                ? "Could not reach the local server at " + _profile.LocalBaseUrl + ". Start the server or fix the URL."
                : "Not signed in. Sign in to continue, or pick another sign-in method in the profile.");
        }

        private async Task CrashAsync(CancellationToken token)
        {
            await TextAsync("Simulating a crash…", token);
            IsRunning = false;
            _sink.Exited("The agent process exited unexpectedly (exit code 137). Send a message to start it again.", true);
        }

        private async Task LongTurnAsync(CancellationToken token)
        {
            await TextAsync("Starting a slow build. Press **Stop** to interrupt it.", token);
            await ToolAsync(token, "Bash", new JObject { ["command"] = "./build.sh --all-platforms", ["description"] = "Build every platform" }, "Build finished.", false, null, LongTurnMs);
        }

        private async Task ContextFullAsync(CancellationToken token)
        {
            _contextUsed = ContextAlmostFull;
            _sink.ContextUsage(_contextUsed, ContextWindow);
            await TextAsync("The context window is almost full: the status bar turns its usage yellow. Hover it for details.", token);
        }

        private async Task RestartHintAsync(CancellationToken token)
        {
            _restartPending = true;
            await TextAsync("The MCP and Skills tabs now show the restart hint. It clears with your next message.", token);
        }

        private BackgroundTaskInfo Track(BackgroundTaskInfo task)
        {
            _tasks[task.TaskId] = task;
            _sink.BackgroundTaskChanged(task);
            return task;
        }

        private PendingRequest Request(string toolName, JObject input)
        {
            string id = "request-" + (++_sequence);
            return new PendingRequest { Id = id, ToolName = toolName, Input = input, ToolUseId = ToolIdPrefix + id };
        }

        private async Task<Decision> AskAsync(PendingRequest request, CancellationToken token)
        {
            TaskCompletionSource<Decision> source = new TaskCompletionSource<Decision>();
            _decisions[request.Id] = source;
            _sink.RequestRaised(request);
            try
            {
                using (token.Register(() => source.TrySetCanceled()))
                {
                    return await source.Task;
                }
            }
            finally
            {
                if (_decisions.Remove(request.Id) && !_disposed)
                {
                    _sink.RequestCancelled(request.Id);
                }
            }
        }

        private async Task TextAsync(string text, CancellationToken token, BlockKind kind = BlockKind.Text, string parent = null)
        {
            string key = KeyPrefix + (++_sequence);
            _sink.BlockStarted(new BlockStartRequest(key, kind, parent, null, null));
            for (int start = 0; start < text.Length; start += ChunkChars)
            {
                _sink.TextDelta(key, text.Substring(start, Math.Min(ChunkChars, text.Length - start)));
                await Task.Delay(ChunkDelayMs, token);
            }

            _sink.BlockFinalized(new BlockFinalizeRequest(key, kind, parent, text, null, null, null));
        }

        private async Task ToolAsync(
            CancellationToken token,
            string name,
            JObject input,
            string result,
            bool isError = false,
            string parent = null,
            int delayMs = ToolDelayMs,
            JToken structured = null)
        {
            string id = await StartToolAsync(token, name, input, parent);
            await Task.Delay(delayMs, token);
            FinishTool(id, result, isError, structured);
        }

        private async Task<string> StartToolAsync(CancellationToken token, string name, JObject input, string parent)
        {
            string key = KeyPrefix + (++_sequence);
            string id = ToolIdPrefix + _sequence;
            _sink.BlockStarted(new BlockStartRequest(key, BlockKind.ToolUse, parent, id, name));
            string json = input.ToString(Formatting.None);
            int half = json.Length / 2;
            _sink.ToolInputDelta(key, json.Substring(0, half));
            await Task.Delay(StepDelayMs, token);
            _sink.ToolInputDelta(key, json.Substring(half));
            _sink.BlockFinalized(new BlockFinalizeRequest(key, BlockKind.ToolUse, parent, null, id, name, input));
            return id;
        }

        private void FinishTool(string id, string result, bool isError, JToken structured = null)
        {
            _sink.ToolResult(new ToolResultRequest(id, result, isError, structured));
        }
    }
}
