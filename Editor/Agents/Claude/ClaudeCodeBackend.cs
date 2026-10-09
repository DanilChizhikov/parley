using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DTech.Parley.Editor.Secrets;
using DTech.Parley.Editor.Settings;
using DTech.Parley.Editor.Tools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace DTech.Parley.Editor.Agents.Claude
{
	internal sealed class ClaudeCodeBackend : IAgentBackend
	{
		private const int InitializeTimeoutMs = 60000;
		private const int ControlTimeoutMs = 30000;
		private const int GracefulExitMs = 3000;
		private const string MissingConversation = "No conversation found with session ID";

		private static readonly string[] Efforts = { "low", "medium", "high", "xhigh", "max" };

		private readonly ParleyProfile _profile;
		private readonly IAgentSink _sink;
		private readonly ClaudeStreamMapper _mapper;
		private readonly SdkMcpBridge _bridge;
		private readonly ToolCatalog _unityTools;
		private readonly Dictionary<string, TaskCompletionSource<JObject>> _pendingControl = new ();
		private readonly Dictionary<string, PendingRequest> _openRequests = new ();
		private readonly CancellationTokenSource _lifetime = new ();

		public bool IsRunning => _process != null && _process.IsRunning;

		public bool IsBusy { get; private set; }

		public string SessionId => _mapper.ConfirmedSessionId ?? _resumeSessionId;

		public PermissionMode Mode { get; private set; }

		private ChildProcess _process;
		private Task _starting;
		private string _resumeSessionId;
		private string _model;
		private string _effort;
		private bool _restartPending;
		private bool _recovering;
		private UserTurn _lastTurn;
		private bool _disposed;
		private int _requestCounter;

		public ClaudeCodeBackend(ClaudeCodeRequest request)
		{
			_profile = request.Profile;
			_sink = request.Sink;
			_unityTools = request.UnityTools;
			_resumeSessionId = request.ResumeSessionId;
			_model = request.Profile.Model;
			_effort = request.Profile.Effort;
			Mode = request.Mode;
			_mapper = new ClaudeStreamMapper(request.Sink);
			_bridge = new SdkMcpBridge(request.UnityTools, () => new ToolContext
			{
				AdditionalDirectories = ParleyProjectSettings.instance.AdditionalDirectories,
				GetMode = () => Mode,
			});
		}

		public void Dispose()
		{
			if (_disposed)
			{
				return;
			}

			_disposed = true;
			_lifetime.Cancel();
			StopProcess();
		}

		public async Task StartAsync(CancellationToken token)
		{
			if (IsRunning)
			{
				return;
			}

			if (_starting != null)
			{
				await _starting;
				return;
			}

			_starting = StartCoreAsync(token);
			try
			{
				await _starting;
			}
			finally
			{
				_starting = null;
			}
		}

		public async Task SendAsync(UserTurn turn, CancellationToken token)
		{
			if (_restartPending && IsRunning && !IsBusy)
			{
				_resumeSessionId = SessionId;
				StopProcess();
				_restartPending = false;
			}

			if (!IsRunning)
			{
				await StartAsync(token);
			}

			if (!Write(ClaudeWire.UserMessage(turn, _mapper.SessionId)))
			{
				throw new InvalidOperationException("Claude Code is not accepting input.");
			}

			_lastTurn = turn;
			MarkBusy();
		}

		public void Interrupt()
		{
			foreach (PendingRequest request in new List<PendingRequest>(_openRequests.Values))
			{
				Respond(request, Decision.Deny("The user interrupted.", true));
				_sink.RequestCancelled(request.Id);
			}

			if (IsRunning)
			{
				FireControl(new JObject { ["subtype"] = "interrupt" });
			}
		}

		public void SetPermissionMode(PermissionMode mode)
		{
			Mode = mode;
			if (IsRunning)
			{
				FireControl(new JObject { ["subtype"] = "set_permission_mode", ["mode"] = PermissionModes.ToWire(mode) });
			}

			_sink.ModeChanged(mode);
		}

		public void SetModel(string model)
		{
			_model = string.IsNullOrEmpty(model) ? null : model;
			if (IsRunning)
			{
				FireControl(new JObject { ["subtype"] = "set_model", ["model"] = _model ?? "default" });
			}
		}

		public void SetEffort(string effort)
		{
			effort = string.IsNullOrEmpty(effort) ? null : effort;
			if (effort == _effort)
			{
				return;
			}

			_effort = effort;
			_restartPending = IsRunning;
		}

		public void Respond(PendingRequest request, Decision decision)
		{
			if (request == null || !_openRequests.Remove(request.Id))
			{
				return;
			}

			Write(ClaudeWire.ControlSuccess(request.Id, ClaudeWire.PermissionResult(decision, request.Input)));
			if (decision.NextMode.HasValue)
			{
				SetPermissionMode(decision.NextMode.Value);
			}
		}

		public void StopTask(string taskId)
		{
			if (IsRunning && !string.IsNullOrEmpty(taskId))
			{
				FireControl(new JObject { ["subtype"] = "stop_task", ["task_id"] = taskId });
			}
		}

		private static string WriteRunFile(string name, JObject content)
		{
			string path = Path.Combine(ProjectPaths.RunFolder, name);
			File.WriteAllText(path, content.ToString(Formatting.Indented), new UTF8Encoding(false));
			return path;
		}

		private static IEnumerable<string> SplitExtraArguments(string text)
		{
			if (string.IsNullOrWhiteSpace(text))
			{
				yield break;
			}

			StringBuilder current = new StringBuilder();
			bool quoted = false;
			foreach (char character in text)
			{
				if (character == '"')
				{
					quoted = !quoted;
					continue;
				}

				if (char.IsWhiteSpace(character) && !quoted)
				{
					if (current.Length > 0)
					{
						yield return current.ToString();
						current.Clear();
					}

					continue;
				}

				current.Append(character);
			}

			if (current.Length > 0)
			{
				yield return current.ToString();
			}
		}

		private static void FinishProcess(ChildProcess process, int processId)
		{
			if (!process.WaitForExit(GracefulExitMs))
			{
				process.Kill();
			}

			process.Dispose();
			MainThread.Post(() => ProcessJanitor.Untrack(processId));
		}

		private static BackendCapabilities ParseCapabilities(JObject response)
		{
			BackendCapabilities capabilities = new BackendCapabilities();
			capabilities.Modes.AddRange(new[]
			{
				PermissionMode.Default, PermissionMode.AcceptEdits, PermissionMode.Plan, PermissionMode.Auto, PermissionMode.BypassPermissions, PermissionMode.DontAsk,
			});

			capabilities.Efforts.AddRange(Efforts);
			if (response?["models"] is JArray models)
			{
				foreach (JToken model in models)
				{
					capabilities.Models.Add(new ModelOption
					{
						Value = (string)model["value"],
						DisplayName = (string)model["displayName"] ?? (string)model["value"],
						Description = (string)model["description"],
					});
				}
			}

			if (response?["commands"] is JArray commands)
			{
				foreach (JToken command in commands)
				{
					capabilities.Commands.Add(new SlashCommandInfo
					{
						Name = (string)command["name"],
						Description = (string)command["description"],
						ArgumentHint = (string)command["argumentHint"],
					});
				}
			}

			if (response?["account"] is JObject account)
			{
				List<string> parts = new ();
				AddPart(parts, (string)account["email"]);
				AddPart(parts, (string)account["organization"]);
				AddPart(parts, (string)account["subscriptionType"]);
				AddPart(parts, (string)account["apiKeySource"]);
				string provider = (string)account["apiProvider"];
				AddPart(parts, provider == "firstParty" ? null : provider);
				capabilities.Account = string.Join(" · ", parts);
			}

			return capabilities;
		}

		private static void AddPart(List<string> parts, string value)
		{
			if (!string.IsNullOrEmpty(value) && value != "none")
			{
				parts.Add(value);
			}
		}

		private static string LastLines(string text, int count)
		{
			string[] lines = text.Split('\n');
			int start = Math.Max(0, lines.Length - count);
			return string.Join("\n", lines, start, lines.Length - start).Trim();
		}

		private async Task StartCoreAsync(CancellationToken cancellationToken)
		{
			string executable = await ClaudeCliLocator.LocateAsync(ParleyUserSettings.instance.CliPathOverride);
			if (executable == null)
			{
				throw new InvalidOperationException(
					"Claude Code CLI was not found. Install it (https://code.claude.com/docs/en/setup) or set its path in Preferences > DTech > Parley.");
			}

			string path = await ShellEnvironment.GetLoginPathAsync();
			cancellationToken.ThrowIfCancellationRequested();
			AuthEnvironment auth = AuthEnvironmentBuilder.Build(_profile, ReadSecret);
			if (!auth.IsValid)
			{
				throw new InvalidOperationException(_profile.Name + ": " + string.Join(" ", auth.Problems));
			}

			ProcessStartInfo startInfo = new ProcessStartInfo(executable, CommandLine.Join(BuildArguments(auth)))
			{
				WorkingDirectory = ProjectPaths.Root,
			};

			string environmentPath = ShellEnvironment.Merge(Path.GetDirectoryName(executable), path);
			ApplyEnvironment(startInfo, auth, environmentPath);
			_process = new ChildProcess(startInfo);
			_process.OnStdoutLine += StdoutLineHandler;
			_process.OnExited += ExitedHandler;
			if (!_process.Start(out string error))
			{
				_process = null;
				throw new InvalidOperationException("Failed to start Claude Code: " + error);
			}

			ProcessJanitor.Track(_process.ProcessId);
			try
			{
				JObject response = await SendControlAsync(new JObject { ["subtype"] = "initialize", ["hooks"] = null }, InitializeTimeoutMs);
				_sink.CapabilitiesChanged(ParseCapabilities(response));
				string currentMode = (string)response["current_permission_mode"];
				if (!string.IsNullOrEmpty(currentMode))
				{
					Mode = PermissionModes.FromWire(currentMode);
					_sink.ModeChanged(Mode);
				}
			}
			catch (TimeoutException)
			{
				_sink.Notice(NoticeLevel.Warning, "Claude Code did not answer the initialize request; continuing without model and command lists.");
			}
		}

		private List<string> BuildArguments(AuthEnvironment auth)
		{
			List<string> arguments = new ()
			{
				"-p",
				"--input-format", "stream-json",
				"--output-format", "stream-json",
				"--verbose",
				"--include-partial-messages",
				"--permission-prompt-tool", "stdio",
				"--permission-mode", PermissionModes.ToWire(Mode),
			};

			if (!string.IsNullOrEmpty(_model))
			{
				arguments.Add("--model");
				arguments.Add(_model);
			}

			if (!string.IsNullOrEmpty(_effort))
			{
				arguments.Add("--effort");
				arguments.Add(_effort);
			}

			if (!string.IsNullOrEmpty(_resumeSessionId))
			{
				arguments.Add("--resume=" + _resumeSessionId);
			}

			if (_unityTools.Tools.Count > 0)
			{
				arguments.Add("--mcp-config");
				arguments.Add(WriteRunFile("mcp-" + _profile.Id + ".json", SdkMcpBridge.McpConfig()));
				string[] readOnly = _bridge.ReadOnlyToolNames();
				if (readOnly.Length > 0)
				{
					arguments.Add("--allowedTools");
					arguments.Add(string.Join(",", readOnly));
				}
			}

			if (auth.Settings.Count > 0)
			{
				arguments.Add("--settings");
				arguments.Add(WriteRunFile("settings-" + _profile.Id + ".json", auth.Settings));
			}

			arguments.Add("--append-system-prompt");
			arguments.Add(BuildSystemPromptAppendix());
			foreach (string directory in ParleyProjectSettings.instance.AdditionalDirectories)
			{
				if (!string.IsNullOrWhiteSpace(directory))
				{
					arguments.Add("--add-dir");
					arguments.Add(ProjectPaths.Resolve(directory));
				}
			}

			arguments.AddRange(SplitExtraArguments(_profile.ExtraArguments));
			return arguments;
		}

		private string BuildSystemPromptAppendix()
		{
			StringBuilder builder = new StringBuilder();
			builder.Append("You are running inside the Unity Editor through Parley. Project '").Append(PlayerSettings.productName)
				.Append("', Unity ").Append(Application.unityVersion).Append(", project root ").Append(ProjectPaths.Root).Append('.');
			if (_unityTools.Tools.Count > 0)
			{
				builder.Append(" Unity editor tools are available from the MCP server 'unity' (")
					.Append(string.Join(", ", ToolNames())).Append("). After changing C# scripts call ")
					.Append(SdkMcpBridge.ToolPrefix).Append("refresh and fix the compiler errors it reports.");
			}

			builder.Append(" Do not edit files under Library/ or Temp/, and never change GUIDs in .meta files by hand.");
			string extra = ParleyProjectSettings.instance.AppendSystemPrompt;
			if (!string.IsNullOrWhiteSpace(extra))
			{
				builder.Append("\n\n").Append(extra.Trim());
			}

			return builder.ToString();
		}

		private IEnumerable<string> ToolNames()
		{
			foreach (IParleyTool tool in _unityTools.Tools)
			{
				yield return SdkMcpBridge.ToolPrefix + tool.Name;
			}
		}

		private void ApplyEnvironment(ProcessStartInfo startInfo, AuthEnvironment auth, string path)
		{
			startInfo.Environment["PATH"] = path;
			startInfo.Environment.Remove("CLAUDECODE");
			foreach (string variable in auth.Removed)
			{
				startInfo.Environment.Remove(variable);
			}

			foreach (KeyValuePair<string, string> variable in auth.Variables)
			{
				startInfo.Environment[variable.Key] = variable.Value;
			}
		}

		private string ReadSecret(string field)
		{
			return SecretStores.Default.TryGet(SecretStores.Key(_profile.Id, field), out string secret) ? secret : null;
		}

		private void StopProcess()
		{
			ChildProcess process = _process;
			if (process == null)
			{
				return;
			}

			Detach(process);
			process.CloseInput();
			int processId = process.ProcessId;
			Task.Run(() => FinishProcess(process, processId));
		}

		private void Detach(ChildProcess process)
		{
			process.OnStdoutLine -= StdoutLineHandler;
			process.OnExited -= ExitedHandler;
			_process = null;
			ReleaseBusy();
			foreach (TaskCompletionSource<JObject> completion in _pendingControl.Values)
			{
				completion.TrySetException(new InvalidOperationException("Claude Code exited."));
			}

			_pendingControl.Clear();
			foreach (string requestId in new List<string>(_openRequests.Keys))
			{
				_sink.RequestCancelled(requestId);
			}

			_openRequests.Clear();
		}

		private bool Write(JObject message)
		{
			return _process != null && _process.WriteLine(ClaudeWire.Serialize(message));
		}

		private void FireControl(JObject request)
		{
			_ = SendControlSafeAsync(request);
		}

		private async Task SendControlSafeAsync(JObject request)
		{
			try
			{
				await SendControlAsync(request, ControlTimeoutMs);
			}
			catch (Exception exception)
			{
				_sink.Notice(NoticeLevel.Warning, (string)request["subtype"] + " failed: " + exception.Message);
			}
		}

		private async Task<JObject> SendControlAsync(JObject request, int timeoutMs)
		{
			string requestId = "req_" + (++_requestCounter) + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
			TaskCompletionSource<JObject> completion = new TaskCompletionSource<JObject>(TaskCreationOptions.RunContinuationsAsynchronously);
			_pendingControl[requestId] = completion;
			if (!Write(ClaudeWire.ControlRequest(requestId, request)))
			{
				_pendingControl.Remove(requestId);
				throw new InvalidOperationException("Claude Code is not running.");
			}

			Task finished = await Task.WhenAny(completion.Task, Task.Delay(timeoutMs));
			if (finished != completion.Task)
			{
				_pendingControl.Remove(requestId);
				throw new TimeoutException((string)request["subtype"] + " timed out.");
			}

			return await completion.Task;
		}

		private async Task RefreshContextUsageAsync()
		{
			try
			{
				JObject usage = await SendControlAsync(new JObject { ["subtype"] = "get_context_usage", ["detail"] = "summary" }, ControlTimeoutMs);
				long total = (long?)usage["totalTokens"] ?? 0;
				long max = (long?)usage["maxTokens"] ?? 0;
				if (max > 0)
				{
					_sink.ContextUsage(total, max);
				}
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
			}
		}

		private void RecoverFromMissingConversation()
		{
			if (_recovering)
			{
				return;
			}

			_recovering = true;
			string missing = _resumeSessionId;
			_resumeSessionId = null;
			_mapper.Reset();
			StopProcess();
			_sink.Notice(NoticeLevel.Warning, "Claude Code no longer has session " + (missing ?? "?")
				+ ", so Parley started a new one. Earlier messages stay in this window, but Claude does not remember them.");
			_ = ResendAfterRecoveryAsync();
		}

		private async Task ResendAfterRecoveryAsync()
		{
			try
			{
				await StartAsync(_lifetime.Token);
				if (_lastTurn != null && Write(ClaudeWire.UserMessage(_lastTurn, null)))
				{
					MarkBusy();
				}
			}
			catch (OperationCanceledException)
			{
			}
			catch (Exception exception)
			{
				_sink.Notice(NoticeLevel.Error, exception.Message);
				_sink.TurnCompleted(new TurnResult { IsError = true, Subtype = "error" });
			}
			finally
			{
				_recovering = false;
			}
		}

		private void MarkBusy()
		{
			if (IsBusy)
			{
				return;
			}

			IsBusy = true;
			ReloadGuard.Acquire(this);
		}

		private void ReleaseBusy()
		{
			IsBusy = false;
			ReloadGuard.Release(this);
		}

		private void HandleControlRequest(JObject message)
		{
			string requestId = (string)message["request_id"];
			JObject request = message["request"] as JObject ?? new JObject();
			switch ((string)request["subtype"])
			{
				case "can_use_tool":
					if (_openRequests.ContainsKey(requestId))
					{
						return;
					}

					PendingRequest pending = ClaudeWire.ParsePermissionRequest(requestId, request);
					_openRequests[requestId] = pending;
					_sink.RequestRaised(pending);
					break;
				case "mcp_message":
					_ = HandleMcpAsync(requestId, request);
					break;
				case "hook_callback":
					Write(ClaudeWire.ControlSuccess(requestId, new JObject { ["continue"] = true }));
					break;
				default:
					Write(ClaudeWire.ControlError(requestId, "Unsupported control request: " + (string)request["subtype"]));
					break;
			}
		}

		private async Task HandleMcpAsync(string requestId, JObject request)
		{
			JObject message = request["message"] as JObject ?? new JObject();
			JObject response;
			try
			{
				response = await _bridge.HandleAsync((string)request["server_name"], message, _lifetime.Token);
			}
			catch (Exception exception)
			{
				response = SdkMcpBridge.Error(message["id"], -32603, exception.Message);
			}

			response ??= new JObject { ["jsonrpc"] = "2.0", ["result"] = new JObject() };
			Write(ClaudeWire.ControlSuccess(requestId, new JObject { ["mcp_response"] = response }));
		}

		private void HandleControlResponse(JObject message)
		{
			JObject wrapper = message["response"] as JObject;
			string requestId = (string)wrapper?["request_id"];
			if (wrapper?["pending_permission_requests"] is JArray pendingRequests)
			{
				foreach (JToken pending in pendingRequests)
				{
					if (pending is JObject request)
					{
						HandleControlRequest(request);
					}
				}
			}

			if (requestId == null || !_pendingControl.TryGetValue(requestId, out TaskCompletionSource<JObject> completion))
			{
				return;
			}

			_pendingControl.Remove(requestId);
			if ((string)wrapper["subtype"] == "error")
			{
				completion.TrySetException(new InvalidOperationException((string)wrapper["error"] ?? "control request failed"));
			}
			else
			{
				completion.TrySetResult(wrapper["response"] as JObject ?? new JObject());
			}
		}

		private void StdoutLineHandler(string line)
		{
			if (string.IsNullOrWhiteSpace(line) || line[0] != '{')
			{
				return;
			}

			JObject message;
			try
			{
				message = JObject.Parse(line);
			}
			catch (JsonException)
			{
				return;
			}

			try
			{
				string type = (string)message["type"];
				switch (type)
				{
					case "control_request":
						HandleControlRequest(message);
						return;
					case "control_response":
						HandleControlResponse(message);
						return;
					case "control_cancel_request":
						string cancelled = (string)message["request_id"];
						if (cancelled != null && _openRequests.Remove(cancelled))
						{
							_sink.RequestCancelled(cancelled);
						}

						return;
					case "keep_alive":
						return;
					case "assistant":
					case "stream_event":
						MarkBusy();
						break;
					case "system":
						if ((string)message["subtype"] == "init")
						{
							Mode = PermissionModes.FromWire((string)message["permissionMode"]);
						}

						break;
				}

				if (type == "result" && line.Contains(MissingConversation))
				{
					RecoverFromMissingConversation();
					return;
				}

				_mapper.Handle(message);
				if (type == "result")
				{
					ReleaseBusy();
					_ = RefreshContextUsageAsync();
				}
			}
			catch (Exception exception)
			{
				Debug.LogWarning("[Parley] Failed to handle Claude Code message: " + exception.Message + "\n" + line);
			}
		}

		private void ExitedHandler(int code)
		{
			ChildProcess process = _process;
			if (process == null)
			{
				return;
			}

			ProcessJanitor.Untrack(process.ProcessId);
			string stderr = process.RecentStderr;
			Detach(process);
			process.Dispose();
			if (_disposed)
			{
				return;
			}

			if (stderr.Contains(MissingConversation))
			{
				RecoverFromMissingConversation();
				return;
			}

			string lower = stderr.ToLowerInvariant();
			if (lower.Contains("login") || lower.Contains("api key") || lower.Contains("authenticat") || lower.Contains("unauthorized"))
			{
				_sink.AuthRequired(LastLines(stderr, 6));
			}

			_sink.Exited("Claude Code exited with code " + code + (stderr.Length > 0 ? ":\n" + LastLines(stderr, 12) : "."), true);
		}
	}
}