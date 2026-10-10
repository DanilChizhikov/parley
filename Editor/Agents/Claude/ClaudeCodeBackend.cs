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
		private const string AllowBypassFlag = "--allow-dangerously-skip-permissions";
		private const string RestrictedFlag = "--restricted";
		private const string RestrictedVariable = "CLAUDE_CODE_RESTRICTED";

		private static readonly string[] Efforts = { "low", "medium", "high", "xhigh", "max" };
		private static readonly string[] RestrictedTruthyValues = { "1", "true", "yes", "on" };

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

		public bool HasPendingRestart => _restartPending;

		private ChildProcess _process;
		private Task _starting;
		private string _resumeSessionId;
		private string _model;
		private string _effort;
		private bool _restartPending;
		private bool _recovering;
		private UserTurn _lastTurn;
		private McpConfiguration _mcp = new ();
		private JObject _passthroughMcpServers = new ();
		private bool _mcpStatusUnavailable;
		private bool _mcpReady;
		private bool _disposed;
		private bool _contextUsageUnavailable;
		private int _requestCounter;

		public ClaudeCodeBackend(
			ParleyProfile profile,
			IAgentSink sink,
			ToolCatalog unityTools,
			string resumeSessionId,
			PermissionMode mode)
		{
			_profile = profile;
			_sink = sink;
			_unityTools = unityTools;
			_resumeSessionId = resumeSessionId;
			_model = profile.Model;
			_effort = profile.Effort;
			Mode = mode;
			_mapper = new ClaudeStreamMapper(sink);
			_bridge = new SdkMcpBridge(unityTools, () => new ToolContext
			{
				AdditionalDirectories = ParleyProjectSettings.instance.AdditionalDirectories,
				GetMode = () => Mode,
			});
		}

		public static bool IsRestrictedMode(string extraArguments, string restrictedVariable)
		{
			foreach (string argument in CommandLine.Split(extraArguments))
			{
				if (argument == RestrictedFlag)
				{
					return true;
				}
			}

			string value = restrictedVariable?.Trim();
			if (string.IsNullOrEmpty(value))
			{
				return false;
			}

			foreach (string truthy in RestrictedTruthyValues)
			{
				if (string.Equals(value, truthy, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}

			return false;
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

		public void SetMcpConfiguration(McpConfiguration configuration)
		{
			bool externalChanged = !_mcp.DisabledExternal.SetEquals(configuration.DisabledExternal);
			bool serversChanged = ClaudeWire.McpServersFingerprint(_mcp) != ClaudeWire.McpServersFingerprint(configuration);
			_mcp = configuration;
			if (!IsRunning)
			{
				return;
			}

			if (externalChanged)
			{
				_restartPending = true;
			}

			if (_mcpReady)
			{
				_ = serversChanged ? ApplyMcpServersAsync() : RefreshMcpStatusAsync();
			}
		}

		public void RefreshMcpStatus()
		{
			_ = RefreshMcpStatusAsync();
		}

		private static string WriteRunFile(string name, JObject content)
		{
			string path = Path.Combine(ProjectPaths.RunFolder, name);
			File.WriteAllText(path, content.ToString(Formatting.Indented), new UTF8Encoding(false));
			return path;
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

		private static BackendCapabilities ParseCapabilities(JObject response, bool allowBypass)
		{
			BackendCapabilities capabilities = new BackendCapabilities();
			capabilities.Modes.AddRange(new[] { PermissionMode.Default, PermissionMode.AcceptEdits, PermissionMode.Plan, PermissionMode.Auto });
			if (allowBypass)
			{
				capabilities.Modes.Add(PermissionMode.BypassPermissions);
			}

			capabilities.Modes.Add(PermissionMode.DontAsk);

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
				throw new AgentSetupException(
					"Claude Code CLI was not found. Install it (https://code.claude.com/docs/en/setup) or set its path in Preferences > DTech > Parley.");
			}

			string path = await ShellEnvironment.GetLoginPathAsync();
			cancellationToken.ThrowIfCancellationRequested();
			AuthEnvironment auth = AuthEnvironmentBuilder.Build(_profile, ReadSecret);
			if (!auth.IsValid)
			{
				throw new AgentSetupException(_profile.Name + ": " + string.Join(" ", auth.Problems));
			}

			bool restricted = IsRestrictedMode(_profile.ExtraArguments, Environment.GetEnvironmentVariable(RestrictedVariable));
			ProcessStartInfo startInfo = new ProcessStartInfo(executable, CommandLine.Join(BuildArguments(auth, restricted)))
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

			ProcessJanitor.Track(_process.ProcessId, _process.ProcessName);
			try
			{
				JObject response = await SendControlAsync(new JObject { ["subtype"] = "initialize", ["hooks"] = null }, InitializeTimeoutMs);
				_sink.CapabilitiesChanged(ParseCapabilities(response, !restricted));
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
				return;
			}

			JObject status = await ReadMcpStatusAsync();
			_passthroughMcpServers = ClaudeWire.PassthroughMcpServers(status);
			_mcpReady = true;
			if (_mcp.Servers.Count > 0)
			{
				await ApplyMcpServersAsync();
			}
			else if (status != null)
			{
				_sink.McpStatusChanged(ClaudeWire.ParseMcpStatus(status, _mcp));
			}
		}

		private List<string> BuildArguments(AuthEnvironment auth, bool restricted)
		{
			PermissionMode launchMode = restricted && Mode == PermissionMode.BypassPermissions ? PermissionMode.Default : Mode;
			List<string> arguments = new ()
			{
				"-p",
				"--input-format", "stream-json",
				"--output-format", "stream-json",
				"--verbose",
				"--include-partial-messages",
				"--permission-prompt-tool", "stdio",
				"--permission-mode", PermissionModes.ToWire(launchMode),
			};

			if (!restricted)
			{
				arguments.Add(AllowBypassFlag);
			}

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

			if (_mcp.DisabledExternal.Count > 0)
			{
				arguments.Add("--disallowedTools");
				arguments.Add(ClaudeWire.McpDenyRules(_mcp.DisabledExternal));
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

			arguments.AddRange(CommandLine.Split(_profile.ExtraArguments));
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
			_mcpReady = false;
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
			if (_contextUsageUnavailable)
			{
				return;
			}

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
			catch (Exception)
			{
				_contextUsageUnavailable = IsRunning;
			}
		}

		private async Task ApplyMcpServersAsync()
		{
			try
			{
				JObject request = new JObject
				{
					["subtype"] = "mcp_set_servers",
					["servers"] = ClaudeWire.McpServers(_mcp, _passthroughMcpServers, _unityTools.Tools.Count > 0),
				};

				JObject response = await SendControlAsync(request, InitializeTimeoutMs);
				if (response["errors"] is JObject errors)
				{
					foreach (JProperty error in errors.Properties())
					{
						_sink.Notice(NoticeLevel.Warning, "MCP server '" + error.Name + "': " + error.Value);
					}
				}
			}
			catch (Exception exception)
			{
				_sink.Notice(NoticeLevel.Warning, "Could not update MCP servers: " + exception.Message);
			}

			await RefreshMcpStatusAsync();
		}

		private async Task RefreshMcpStatusAsync()
		{
			JObject response = await ReadMcpStatusAsync();
			if (response != null)
			{
				_sink.McpStatusChanged(ClaudeWire.ParseMcpStatus(response, _mcp));
			}
		}

		private async Task<JObject> ReadMcpStatusAsync()
		{
			if (!IsRunning || _mcpStatusUnavailable)
			{
				return null;
			}

			try
			{
				return await SendControlAsync(new JObject { ["subtype"] = "mcp_status" }, ControlTimeoutMs);
			}
			catch (Exception exception)
			{
				_mcpStatusUnavailable = IsRunning;
				Debug.LogWarning("[Parley] Could not read MCP status: " + exception.Message);
				return null;
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

				if (type == "result" && _resumeSessionId != null && ClaudeWire.IsMissingConversation(message))
				{
					RecoverFromMissingConversation();
					return;
				}

				_mapper.Handle(message);
				if (type == "result")
				{
					ReleaseBusy();
					_ = RefreshContextUsageAsync();
					_ = RefreshMcpStatusAsync();
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

			if (_resumeSessionId != null && stderr.Contains(ClaudeWire.MissingConversation))
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