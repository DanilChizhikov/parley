using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DTech.Parley.Editor.Agents.Claude;
using DTech.Parley.Editor.Agents.Local;
using DTech.Parley.Editor.Secrets;
using DTech.Parley.Editor.Tools;
using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Sessions
{
    internal sealed class ContextSummaryGenerator : IContextSummaryGenerator
    {
        public async Task<string> GenerateAsync(ParleyProfile profile, string prompt,
            CancellationToken cancellationToken)
        {
            using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(3));
            CancellationToken token = timeout.Token;
            token.ThrowIfCancellationRequested();
            if (profile.Kind == ProfileKind.Local)
            {
                string url = string.IsNullOrWhiteSpace(profile.LocalBaseUrl)
                    ? LocalPresets.DefaultBaseUrl(profile.Preset) : profile.LocalBaseUrl;
                SecretStores.Default.TryGet(SecretStores.Key(profile.Id, SecretFields.LocalApiKey), out string key);
                IChatCompletionClient client = new OpenAiCompatClient(url, key);
                RoundStream round = new(new SummarySink(), 0);
                await client.StreamChatAsync(BuildLocalRequest(profile, prompt), round.HandleChunk, token);
                round.Complete(false);
                if (round.Calls.Count > 0)
                {
                    throw new InvalidOperationException("The summary model returned tool calls instead of a summary.");
                }

                return round.Text;
            }

            if (profile.Kind != ProfileKind.ClaudeCode)
            {
                throw new NotSupportedException("This Codex CLI protocol cannot disable all tools for a summary. " +
                    "Choose a Local API or Claude profile to summarize, or transfer Last N Messages to Codex.");
            }

            SummarySink sink = new();
            using IAgentBackend backend = new ClaudeCodeBackend(profile, sink,
                new ToolCatalog(Array.Empty<IParleyTool>()), null, PermissionMode.Default, summaryOnly: true);
            using CancellationTokenRegistration registration = token.Register(() => sink.Completion.TrySetCanceled());
            await backend.StartAsync(token);
            await backend.SendAsync(new UserTurn { Text = prompt }, token);
            return await sink.Completion.Task;
        }

        internal static JObject BuildLocalRequest(ParleyProfile profile, string prompt)
        {
            return new JObject
            {
                ["model"] = profile.Model ?? string.Empty,
                ["messages"] = new JArray { new JObject { ["role"] = "user", ["content"] = prompt } },
                ["stream"] = true,
                ["temperature"] = profile.Temperature,
                ["max_tokens"] = Math.Max(1, profile.MaxTokens),
            };
        }

        private sealed class SummarySink : IAgentSink
        {
            public TaskCompletionSource<string> Completion { get; } = new();
            private readonly Dictionary<string, string> _texts = new();

            public void TextDelta(string key, string delta)
            {
                _texts.TryGetValue(key, out string text);
                _texts[key] = text + delta;
            }

            public void BlockFinalized(BlockFinalizeRequest request)
            {
                if (request.Kind == BlockKind.Text && string.IsNullOrEmpty(request.ParentToolUseId))
                {
                    _texts[request.Key] = request.Text;
                }
            }

            public void TurnCompleted(TurnResult result)
            {
                if (result.IsError || result.Subtype == "interrupted")
                {
                    Completion.TrySetException(new InvalidOperationException("Summary failed: " + result.Subtype));
                }
                else
                {
                    Completion.TrySetResult(string.Join("\n\n", _texts.Values));
                }
            }

            public void RequestRaised(PendingRequest request)
            {
                Completion.TrySetException(new InvalidOperationException("A tool-free summary requested permission."));
            }

            public void AuthRequired(string message) => Completion.TrySetException(new InvalidOperationException(message));
            public void Exited(string reason, bool unexpected) =>
                Completion.TrySetException(new InvalidOperationException("Summary agent exited: " + reason));
            public void BlockStarted(BlockStartRequest request) { }
            public void ToolInputDelta(string key, string partialJson) { }
            public void ToolResult(ToolResultRequest request) { }
            public void RequestCancelled(string requestId) { }
            public void BackgroundTaskChanged(BackgroundTaskInfo task) { }
            public void CapabilitiesChanged(BackendCapabilities capabilities) { }
            public void SessionStarted(SessionInfo info) { }
            public void ModeChanged(PermissionMode mode) { }
            public void ContextUsage(long usedTokens, long maxTokens) { }
            public void UsageReported(UsageReport report) { }
            public void RateLimitsChanged(RateLimits limits) { }
            public void McpStatusChanged(IReadOnlyList<McpServerStatus> servers) { }
            public void SkillsChanged(IReadOnlyList<SkillInfo> skills) { }
            public void Notice(NoticeLevel level, string text) { }
        }
    }
}
