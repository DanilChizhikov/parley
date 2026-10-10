using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DTech.Parley.Editor.Secrets;
using DTech.Parley.Editor.Settings;
using DTech.Parley.Editor.Tools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DTech.Parley.Editor.Agents.Local
{
	internal sealed class LocalAgentBackend : IAgentBackend
	{
		private const string ClearCommand = "/clear";
		private const string CompactCommand = "/compact";

		private readonly ParleyProfile _profile;
		private readonly IAgentSink _sink;
		private readonly ToolCatalog _catalog;
		private readonly PermissionGate _gate;
		private readonly ToolContext _context;
		private readonly List<JObject> _history = new ();
		private readonly Queue<UserTurn> _queued = new ();
		private readonly Dictionary<string, TaskCompletionSource<Decision>> _waiting = new ();
		private readonly ContextBudget _budget = new ();
		private readonly Func<string, string, IChatCompletionClient> _clientFactory;

		public bool IsRunning => _client != null;

		public bool IsBusy => _running;

		public string SessionId { get; }

		public PermissionMode Mode { get; private set; }

		public IReadOnlyList<JObject> History => _history;

		private IChatCompletionClient _client;
		private string _model;
		private CancellationTokenSource _turn;
		private bool _running;
		private int _blockCounter;
		private int _lastPromptChars;
		private RoundStream _round;

		public LocalAgentBackend(
			ParleyProfile profile,
			IAgentSink sink,
			ToolCatalog catalog,
			string sessionId,
			IEnumerable<JObject> history,
			PermissionMode mode,
			Func<string, string, IChatCompletionClient> clientFactory = null)
		{
			_profile = profile;
			_sink = sink;
			_catalog = catalog;
			_model = profile.Model;
			SessionId = string.IsNullOrEmpty(sessionId) ? Guid.NewGuid().ToString("N") : sessionId;
			Mode = mode == PermissionMode.Auto ? PermissionMode.Default : mode;
			_clientFactory = clientFactory ?? ((url, key) => new OpenAiCompatClient(url, key));
			string root = ProjectPaths.Root;
			_gate = new PermissionGate(root, () => ParleyProjectSettings.instance.AdditionalDirectories, () => ParleyUserSettings.instance.RulesFor(root));
			_context = new ToolContext
			{
				ProjectRoot = root,
				AdditionalDirectories = ParleyProjectSettings.instance.AdditionalDirectories,
				GetMode = () => Mode,
				SetMode = SetPermissionMode,
				AskUser = AskAsync,
			};

			if (history != null)
			{
				foreach (JObject message in history)
				{
					if (message != null)
					{
						_history.Add(message);
					}
				}
			}
		}

		public void Dispose()
		{
			_turn?.Cancel();
			CancelWaiting();
		}

		public async Task StartAsync(CancellationToken token)
		{
			if (_client != null)
			{
				return;
			}

			string apiKey = SecretStores.Default.TryGet(SecretStores.Key(_profile.Id, SecretFields.LocalApiKey), out string key) ? key : null;
			string baseUrl = string.IsNullOrWhiteSpace(_profile.LocalBaseUrl) ? LocalPresets.DefaultBaseUrl(_profile.Preset) : _profile.LocalBaseUrl;
			_client = _clientFactory(baseUrl, apiKey);
			BackendCapabilities capabilities = new BackendCapabilities
			{
				Account = LocalPresets.DisplayName(_profile.Preset) + " · " + baseUrl,
			};

			capabilities.Modes.AddRange(new[] { PermissionMode.Default, PermissionMode.AcceptEdits, PermissionMode.Plan, PermissionMode.BypassPermissions, PermissionMode.DontAsk });
			capabilities.Commands.Add(new SlashCommandInfo { Name = "compact", Description = "Summarize the conversation to free context" });
			capabilities.Commands.Add(new SlashCommandInfo { Name = "clear", Description = "Forget the conversation history" });
			try
			{
				List<string> models = await _client.ListModelsAsync(token);
				foreach (string model in models)
				{
					capabilities.Models.Add(new ModelOption { Value = model, DisplayName = model });
				}

				if (string.IsNullOrEmpty(_model) && models.Count > 0)
				{
					_model = models[0];
				}
			}
			catch (Exception exception) when (!(exception is OperationCanceledException))
			{
				_sink.Notice(NoticeLevel.Warning, "Could not list models at " + baseUrl + ": " + exception.Message + ". Is the server running?");
			}

			_sink.CapabilitiesChanged(capabilities);
			_sink.SessionStarted(new SessionInfo { SessionId = SessionId, Model = _model, Mode = Mode, Cwd = ProjectPaths.Root });
		}

		public async Task SendAsync(UserTurn turn, CancellationToken token)
		{
			await StartAsync(token);
			string text = (turn.Text ?? string.Empty).Trim();
			if (!_running && text == ClearCommand)
			{
				_history.Clear();
				_sink.Notice(NoticeLevel.Info, "Conversation history cleared.");
				_sink.TurnCompleted(new TurnResult { Subtype = "success" });
				return;
			}

			if (!_running && text == CompactCommand)
			{
				_ = RunAsync(true);
				return;
			}

			_queued.Enqueue(turn);
			if (!_running)
			{
				_ = RunAsync(false);
			}
		}

		public void Interrupt()
		{
			_turn?.Cancel();
		}

		public void SetPermissionMode(PermissionMode mode)
		{
			Mode = mode == PermissionMode.Auto ? PermissionMode.Default : mode;
			_sink.ModeChanged(Mode);
		}

		public void SetModel(string model)
		{
			_model = string.IsNullOrEmpty(model) ? _model : model;
		}

		public void SetEffort(string effort)
		{
		}

		public void Respond(PendingRequest request, Decision decision)
		{
			if (request != null && _waiting.TryGetValue(request.Id, out TaskCompletionSource<Decision> completion))
			{
				_waiting.Remove(request.Id);
				completion.TrySetResult(decision);
			}
		}

		public void StopTask(string taskId)
		{
		}

		private async Task RunAsync(bool compact)
		{
			_running = true;
			_turn = new CancellationTokenSource();
			CancellationToken cancellationToken = _turn.Token;
			ReloadGuard.Acquire(this);
			Stopwatch stopwatch = Stopwatch.StartNew();
			TurnResult result = new TurnResult { Subtype = "success" };
			try
			{
				if (compact)
				{
					await CompactAsync(result, cancellationToken);
				}
				else
				{
					await LoopAsync(result, cancellationToken);
				}
			}
			catch (OperationCanceledException)
			{
				result.Subtype = "interrupted";
				DropQueued();
				RepairAfterInterrupt();
				_sink.Notice(NoticeLevel.Info, "Interrupted.");
			}
			catch (Exception exception)
			{
				result.IsError = true;
				result.Subtype = "error";
				result.Errors.Add(exception.Message);
				DropQueued();
				RepairAfterInterrupt();
				_sink.Notice(NoticeLevel.Error, exception.Message);
			}
			finally
			{
				CancelWaiting();
				_running = false;
				_turn.Dispose();
				_turn = null;
				ReloadGuard.Release(this);
				result.DurationMs = stopwatch.ElapsedMilliseconds;
				_sink.TurnCompleted(result);
			}

			if (_queued.Count > 0 && !result.IsError)
			{
				_ = RunAsync(false);
			}
		}

		private async Task LoopAsync(TurnResult result, CancellationToken cancellationToken)
		{
			int maxTurns = Mathf.Max(1, _profile.MaxTurns);
			for (int round = 0; round < maxTurns; round++)
			{
				cancellationToken.ThrowIfCancellationRequested();
				while (_queued.Count > 0)
				{
					_history.Add(BuildUserMessage(_queued.Dequeue()));
				}

				RoundStream output = await StreamRoundAsync(BuildBody(true), cancellationToken);
				result.InputTokens += output.PromptTokens;
				result.OutputTokens += output.CompletionTokens;
				result.NumTurns = round + 1;
				JObject assistant = new JObject { ["role"] = "assistant", ["content"] = _profile.TextToolCalls ? output.RawText : output.Text };
				if (output.Calls.Count > 0 && !_profile.TextToolCalls)
				{
					JArray toolCalls = new JArray();
					foreach (AccumulatedToolCall call in output.Calls)
					{
						toolCalls.Add(new JObject
						{
							["id"] = call.Id,
							["type"] = "function",
							["function"] = new JObject { ["name"] = call.Name, ["arguments"] = call.Arguments.ToString() },
						});
					}

					assistant["tool_calls"] = toolCalls;
				}

				_history.Add(assistant);
				_round = null;
				if (output.Calls.Count == 0)
				{
					if (_queued.Count > 0)
					{
						continue;
					}

					return;
				}

				StringBuilder textResponses = new StringBuilder();
				foreach (AccumulatedToolCall call in output.Calls)
				{
					ToolResult toolResult = await ExecuteCallAsync(call, cancellationToken);
					_sink.ToolResult(new ToolResultRequest(call.Id, toolResult.Text, toolResult.IsError, toolResult.Structured));
					if (_profile.TextToolCalls)
					{
						textResponses.Append("<tool_response name=\"").Append(call.Name).Append("\">\n").Append(toolResult.Text).AppendLine("\n</tool_response>");
					}
					else
					{
						_history.Add(new JObject { ["role"] = "tool", ["tool_call_id"] = call.Id, ["content"] = toolResult.Text });
					}
				}

				if (textResponses.Length > 0)
				{
					_history.Add(new JObject { ["role"] = "user", ["content"] = textResponses.ToString().TrimEnd() });
				}
			}

			result.Subtype = "error_max_turns";
			_sink.Notice(NoticeLevel.Warning, "Stopped after " + maxTurns + " model calls (profile limit).");
		}

		private async Task CompactAsync(TurnResult result, CancellationToken cancellationToken)
		{
			if (_history.Count == 0)
			{
				_sink.Notice(NoticeLevel.Info, "Nothing to compact.");
				return;
			}

			_history.Add(new JObject
			{
				["role"] = "user",
				["content"] = "Summarize our conversation so far for your own future reference: the goal, decisions made, files touched (with paths), "
					+ "current state and open tasks. Be complete but concise. Reply with the summary only.",
			});

			RoundStream output = await StreamRoundAsync(BuildBody(false), cancellationToken);
			result.InputTokens += output.PromptTokens;
			result.OutputTokens += output.CompletionTokens;
			_round = null;
			_history.Clear();
			_history.Add(new JObject { ["role"] = "user", ["content"] = "Summary of our earlier conversation:\n\n" + output.Text });
			_history.Add(new JObject { ["role"] = "assistant", ["content"] = "Understood. I'll continue from this summary." });
			_sink.Notice(NoticeLevel.Info, "Conversation compacted.");
		}

		private JObject BuildBody(bool includeTools)
		{
			int window = Mathf.Max(2048, _profile.ContextWindow);
			int maxTokens = Mathf.Clamp(_profile.MaxTokens, 256, window / 2);
			SystemPromptRequest promptRequest = new SystemPromptRequest(Mode, _catalog, _profile.TextToolCalls, ParleyProjectSettings.instance, Application.unityVersion);
			string system = SystemPromptBuilder.Build(promptRequest);
			JArray tools = includeTools && !_profile.TextToolCalls && _catalog.Tools.Count > 0 ? SystemPromptBuilder.FunctionTools(_catalog) : null;
			int fixedChars = system.Length + (tools == null ? 0 : tools.ToString(Formatting.None).Length);
			int budget = window - maxTokens - _budget.Tokens(fixedChars) - 256;
			List<JObject> messages = _budget.Fit(_history, Mathf.Max(1024, (int)(budget * 0.9)), out int estimate);
			_sink.ContextUsage(estimate + _budget.Tokens(fixedChars), window);
			JArray array = new JArray { new JObject { ["role"] = "system", ["content"] = system } };
			int messageChars = 0;
			foreach (JObject message in messages)
			{
				array.Add(message);
				messageChars += ContextBudget.EstimateChars(message);
			}

			_lastPromptChars = fixedChars + messageChars;
			JObject body = new JObject
			{
				["model"] = _model ?? string.Empty,
				["messages"] = array,
				["stream"] = true,
				["stream_options"] = new JObject { ["include_usage"] = true },
				["temperature"] = _profile.Temperature,
				["max_tokens"] = maxTokens,
			};

			if (tools != null)
			{
				body["tools"] = tools;
			}

			return body;
		}

		private JObject BuildUserMessage(UserTurn turn)
		{
			StringBuilder text = new StringBuilder(turn.Text ?? string.Empty);
			List<ChatAttachment> images = new ();
			foreach (ChatAttachment attachment in turn.Attachments)
			{
				if (attachment.Kind == AttachmentKind.Text)
				{
					text.Append("\n\n<attachment name=\"").Append(attachment.Label).Append("\">\n").Append(attachment.Text).Append("\n</attachment>");
				}
				else if (_profile.Vision)
				{
					images.Add(attachment);
				}
				else
				{
					text.Append("\n\n[image '").Append(attachment.Label).Append("' omitted: vision is off for this profile]");
				}
			}

			if (images.Count == 0)
			{
				return new JObject { ["role"] = "user", ["content"] = text.ToString() };
			}

			JArray parts = new JArray { new JObject { ["type"] = "text", ["text"] = text.ToString() } };
			foreach (ChatAttachment image in images)
			{
				parts.Add(new JObject
				{
					["type"] = "image_url",
					["image_url"] = new JObject { ["url"] = "data:" + (image.MediaType ?? "image/png") + ";base64," + image.Base64 },
				});
			}

			return new JObject { ["role"] = "user", ["content"] = parts };
		}

		private async Task<RoundStream> StreamRoundAsync(JObject body, CancellationToken cancellationToken)
		{
			RoundStream round = new RoundStream(_sink, ++_blockCounter);
			_round = round;
			await _client.StreamChatAsync(body, round.HandleChunk, cancellationToken);
			round.Complete(_profile.TextToolCalls);
			_budget.Calibrate(_lastPromptChars, round.PromptTokens);
			return round;
		}

		private async Task<ToolResult> ExecuteCallAsync(AccumulatedToolCall call, CancellationToken cancellationToken)
		{
			if (!_catalog.TryGet(call.Name, out IParleyTool tool))
			{
				return ToolResult.Error("Unknown tool '" + call.Name + "'. Available tools: " + string.Join(", ", ToolNames()));
			}

			JObject input = call.ParseArguments();
			if (input == null)
			{
				return ToolResult.Error("Arguments are not valid JSON: " + call.Arguments);
			}

			GateResult gate = _gate.Evaluate(tool, input, Mode);
			if (gate.Verdict == GateVerdict.Deny)
			{
				return ToolResult.Error(gate.Reason);
			}

			if (gate.Verdict == GateVerdict.Ask)
			{
				AllowRule suggested = PermissionGate.SuggestRule(tool, input, ProjectPaths.Root);
				Decision decision = await AskAsync(new PendingRequest
				{
					Id = Guid.NewGuid().ToString("N"),
					ToolName = tool.Name,
					Input = input,
					Suggestions = PermissionGate.ToSuggestions(suggested),
					ToolUseId = call.Id,
					Title = tool.Summarize(input),
					DecisionReason = gate.Reason,
					BlockedPath = gate.Reason != null ? tool.TargetPath(input) : null,
				}, cancellationToken);

				if (!decision.Allow)
				{
					if (decision.Interrupt)
					{
						_turn?.Cancel();
					}

					return ToolResult.Error(string.IsNullOrEmpty(decision.Message) ? "The user denied this action." : "The user denied this action: " + decision.Message);
				}

				if (decision.Remember)
				{
					ParleyUserSettings.instance.AddRule(suggested);
				}

				input = decision.UpdatedInput ?? input;
			}

			_context.ToolUseId = call.Id;
			try
			{
				return await tool.ExecuteAsync(input, _context, cancellationToken);
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception exception)
			{
				return ToolResult.Error(tool.Name + " failed: " + exception.Message);
			}
		}

		private Task<Decision> AskAsync(PendingRequest request, CancellationToken cancellationToken)
		{
			TaskCompletionSource<Decision> completion = new TaskCompletionSource<Decision>(TaskCreationOptions.RunContinuationsAsynchronously);
			_waiting[request.Id] = completion;
			cancellationToken.Register(() => MainThread.Post(() =>
			{
				if (_waiting.Remove(request.Id))
				{
					_sink.RequestCancelled(request.Id);
					completion.TrySetCanceled();
				}
			}));

			_sink.RequestRaised(request);
			return completion.Task;
		}

		private void CancelWaiting()
		{
			foreach (KeyValuePair<string, TaskCompletionSource<Decision>> pair in new List<KeyValuePair<string, TaskCompletionSource<Decision>>>(_waiting))
			{
				_sink.RequestCancelled(pair.Key);
				pair.Value.TrySetCanceled();
			}

			_waiting.Clear();
		}

		private void DropQueued()
		{
			int dropped = _queued.Count;
			_queued.Clear();
			if (dropped > 0)
			{
				_sink.Notice(NoticeLevel.Warning, dropped + " queued message(s) were not sent.");
			}
		}

		private void RepairAfterInterrupt()
		{
			string partialText = _round?.RawText;
			_round = null;
			if (!string.IsNullOrEmpty(partialText))
			{
				_history.Add(new JObject { ["role"] = "assistant", ["content"] = partialText + "\n[interrupted by the user]" });
			}

			for (int i = _history.Count - 1; i >= 0; i--)
			{
				JObject message = _history[i];
				if ((string)message["role"] != "assistant" || !(message["tool_calls"] is JArray calls))
				{
					continue;
				}

				HashSet<string> answered = new ();
				for (int j = i + 1; j < _history.Count; j++)
				{
					if ((string)_history[j]["role"] == "tool")
					{
						answered.Add((string)_history[j]["tool_call_id"]);
					}
				}

				foreach (JToken call in calls)
				{
					string id = (string)call["id"];
					if (!answered.Contains(id))
					{
						_history.Add(new JObject { ["role"] = "tool", ["tool_call_id"] = id, ["content"] = "Interrupted by the user before this tool ran." });
					}
				}

				break;
			}
		}

		private IEnumerable<string> ToolNames()
		{
			foreach (IParleyTool tool in _catalog.Tools)
			{
				yield return tool.Name;
			}
		}
	}
}