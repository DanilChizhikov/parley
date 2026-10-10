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

namespace DTech.Parley.Editor.Agents.Codex
{
    internal sealed class CodexBackend : IAgentBackend
    {
        private const int InitializeTimeoutMs = 60000;
        private const int RequestTimeoutMs = 30000;
        private const int GracefulExitMs = 3000;
        private const int MaxModelPages = 10;
        private const double ForceStopDelaySeconds = 3.0;
        private const string ClientVersion = "0.1.0";
        private const string ClearCommand = "/clear";
        private const string CompactCommand = "/compact";
        private const string ImplementPlanMessage = "Implement the plan.";
        private const string ToolCallMethod = "item/tool/call";

        private readonly ParleyProfile _profile;
        private readonly IAgentSink _sink;
        private readonly CodexEventMapper _mapper;
        private readonly CodexToolBridge _tools;
        private readonly Dictionary<long, TaskCompletionSource<JToken>> _pendingCalls = new ();
        private readonly Dictionary<string, CodexServerRequest> _openRequests = new ();
        private readonly Dictionary<string, string> _childTurns = new ();
        private readonly List<JObject> _models = new ();
        private readonly CancellationTokenSource _lifetime = new ();

        public bool IsRunning => _process != null && _process.IsRunning && _threadId != null;

        public bool IsBusy { get; private set; }

        public string SessionId => _threadId ?? _resumeThreadId;

        public PermissionMode Mode { get; private set; }

        private ChildProcess _process;
        private Task _starting;
        private PendingRequest _planRequest;
        private string _resumeThreadId;
        private string _threadId;
        private string _turnId;
        private string _model;
        private string _activeModel;
        private string _effort;
        private string _collaborationMode;
        private long _requestCounter;
        private double _interruptRequestedAt;
        private bool _interruptRequested;
        private bool _disposed;

        public CodexBackend(
            ParleyProfile profile,
            IAgentSink sink,
            ToolCatalog unityTools,
            string resumeThreadId,
            PermissionMode mode)
        {
            _profile = profile;
            _sink = sink;
            _resumeThreadId = resumeThreadId;
            _model = string.IsNullOrEmpty(profile.Model) ? null : profile.Model;
            _effort = string.IsNullOrEmpty(profile.Effort) ? null : profile.Effort;
            Mode = CodexPermissions.Normalize(mode);
            _mapper = new CodexEventMapper(sink);
            _tools = new CodexToolBridge(unityTools, () => new ToolContext
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

            _starting = _process != null && _process.IsRunning ? OpenThreadAsync() : StartCoreAsync(token);
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
            if (!IsRunning)
            {
                await StartAsync(token);
            }

            CancelPlanRequest();
            string text = (turn.Text ?? string.Empty).Trim();
            if (!IsBusy && text == ClearCommand)
            {
                await StartThreadAsync();
                _sink.Notice(NoticeLevel.Info, "Started a new Codex thread.");
                _sink.TurnCompleted(new TurnResult { Subtype = "success" });
                return;
            }

            if (!IsBusy && text == CompactCommand)
            {
                await SendRequestAsync("thread/compact/start", new JObject { ["threadId"] = _threadId }, RequestTimeoutMs);
                return;
            }

            JArray input = CodexWire.UserInput(turn);
            if (IsBusy && _turnId != null)
            {
                JObject parameters = new JObject { ["threadId"] = _threadId, ["expectedTurnId"] = _turnId, ["input"] = input };
                await SendRequestAsync("turn/steer", parameters, RequestTimeoutMs);
                return;
            }

            await StartTurnAsync(input);
        }

        public void Interrupt()
        {
            if (_interruptRequested && IsBusy && EditorApplication.timeSinceStartup - _interruptRequestedAt >= ForceStopDelaySeconds)
            {
                ForceStop();
                return;
            }

            foreach (CodexServerRequest server in new List<CodexServerRequest>(_openRequests.Values))
            {
                Respond(server.Request, Decision.Deny("The user interrupted.", true));
                _sink.RequestCancelled(server.Request.Id);
            }

            CancelPlanRequest();
            InterruptTurn();
        }

        public void SetPermissionMode(PermissionMode mode)
        {
            Mode = CodexPermissions.Normalize(mode);
            _sink.ModeChanged(Mode);
        }

        public void SetModel(string model)
        {
            _model = string.IsNullOrEmpty(model) ? null : model;
        }

        public void SetEffort(string effort)
        {
            _effort = string.IsNullOrEmpty(effort) ? null : effort;
        }

        public void Respond(PendingRequest request, Decision decision)
        {
            if (request == null)
            {
                return;
            }

            if (_planRequest != null && request.Id == _planRequest.Id)
            {
                _planRequest = null;
                RespondToPlan(decision);
                return;
            }

            if (!_openRequests.TryGetValue(request.Id, out CodexServerRequest server))
            {
                return;
            }

            _openRequests.Remove(request.Id);
            Write(CodexWire.Response(server.RpcId, CodexApprovals.Result(server, decision)));
            if (decision.Interrupt && server.Method == CodexApprovals.UserInputRequest)
            {
                InterruptTurn();
            }

            if (decision.NextMode.HasValue)
            {
                SetPermissionMode(decision.NextMode.Value);
            }
        }

        public void StopTask(string taskId)
        {
        }

        private static string DescribeAccount(JObject account)
        {
            switch ((string)account?["type"])
            {
                case null:
                    return "Not signed in";
                case "chatgpt":
                    List<string> parts = new () { "ChatGPT" };
                    AddPart(parts, (string)account["email"]);
                    AddPart(parts, (string)account["planType"]);
                    return string.Join(" · ", parts);
                case "apiKey":
                    return "OpenAI API key";
                case "amazonBedrock":
                    return "Amazon Bedrock";
                default:
                    return (string)account["type"];
            }
        }

        private static void AddPart(List<string> parts, string value)
        {
            if (!string.IsNullOrEmpty(value) && value != "unknown")
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

        private static void FinishProcess(ChildProcess process, int processId, string credentialsPath)
        {
            if (!process.WaitForExit(GracefulExitMs))
            {
                process.Kill();
            }

            process.Dispose();
            CodexEnvironment.DeleteCredentials(credentialsPath);
            MainThread.Post(() => ProcessJanitor.Untrack(processId));
        }

        private async Task StartCoreAsync(CancellationToken cancellationToken)
        {
            string executable = await CodexCliLocator.LocateAsync(ParleyUserSettings.instance.CodexCliPathOverride);
            if (executable == null)
            {
                throw new AgentSetupException(
                    "Codex CLI was not found. Install it (npm install -g @openai/codex or brew install codex) or set its path in Preferences > DTech > Parley.");
            }

            string apiKey = null;
            if (_profile.CodexAuthMethod == CodexAuthMethod.ApiKey)
            {
                apiKey = ReadSecret(SecretFields.OpenAiApiKey);
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    throw new AgentSetupException(_profile.Name + ": OpenAI API key is not set.");
                }
            }

            string path = await ShellEnvironment.GetLoginPathAsync();
            cancellationToken.ThrowIfCancellationRequested();
            string environmentPath = ShellEnvironment.Merge(Path.GetDirectoryName(executable), path);
            Version version = CodexEnvironment.UsesManagedHome(_profile) ? await CodexCliLocator.GetParsedVersionAsync(executable, environmentPath) : null;
            cancellationToken.ThrowIfCancellationRequested();
            List<string> arguments = new () { "app-server" };
            CodexEnvironment.AddCredentialOptions(arguments, _profile, version);
            arguments.AddRange(CommandLine.Split(_profile.ExtraArguments));
            ProcessStartInfo startInfo = new ProcessStartInfo(executable, CommandLine.Join(arguments))
            {
                WorkingDirectory = ProjectPaths.Root,
            };

            CodexEnvironment.Apply(startInfo, _profile, environmentPath);
            _process = new ChildProcess(startInfo);
            _process.OnStdoutLine += StdoutLineHandler;
            _process.OnExited += ExitedHandler;
            if (!_process.Start(out string error))
            {
                _process = null;
                throw new InvalidOperationException("Failed to start Codex: " + error);
            }

            ProcessJanitor.Track(_process.ProcessId, _process.ProcessName);
            try
            {
                await InitializeAsync(apiKey);
            }
            catch (Exception)
            {
                StopProcess();
                throw;
            }
        }

        private async Task InitializeAsync(string apiKey)
        {
            JObject parameters = new JObject
            {
                ["clientInfo"] = new JObject { ["name"] = "parley", ["title"] = "Parley", ["version"] = ClientVersion },
                ["capabilities"] = new JObject { ["experimentalApi"] = true },
            };

            await SendRequestAsync("initialize", parameters, InitializeTimeoutMs);
            Write(CodexWire.Notification("initialized"));
            string account = await EnsureAccountAsync(apiKey);
            await LoadModelsAsync();
            _sink.CapabilitiesChanged(BuildCapabilities(account));
            await OpenThreadAsync();
        }

        private async Task<string> EnsureAccountAsync(string apiKey)
        {
            if (apiKey != null)
            {
                JObject login = new JObject { ["type"] = "apiKey", ["apiKey"] = apiKey.Trim() };
                await SendRequestAsync("account/login/start", login, RequestTimeoutMs);
                return DescribeAccount(new JObject { ["type"] = "apiKey" });
            }

            JToken response = await SendRequestAsync("account/read", new JObject { ["refreshToken"] = false }, RequestTimeoutMs);
            JObject account = response["account"] as JObject;
            if (account == null && (bool?)response["requiresOpenaiAuth"] == true)
            {
                _sink.AuthRequired("Codex is not signed in. Run `codex login` in a terminal or switch the profile to an OpenAI API key.");
            }

            return DescribeAccount(account);
        }

        private async Task LoadModelsAsync()
        {
            _models.Clear();
            string cursor = null;
            try
            {
                for (int page = 0; page < MaxModelPages; page++)
                {
                    JObject parameters = new JObject();
                    if (cursor != null)
                    {
                        parameters["cursor"] = cursor;
                    }

                    JToken response = await SendRequestAsync("model/list", parameters, RequestTimeoutMs);
                    AddModels(response["data"] as JArray);
                    cursor = (string)response["nextCursor"];
                    if (string.IsNullOrEmpty(cursor))
                    {
                        break;
                    }
                }
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            {
                _sink.Notice(NoticeLevel.Warning, "Could not list Codex models: " + exception.Message);
            }
        }

        private void AddModels(JArray models)
        {
            if (models == null)
            {
                return;
            }

            foreach (JToken model in models)
            {
                if (model is JObject item && (bool?)item["hidden"] != true)
                {
                    _models.Add(item);
                }
            }
        }

        private BackendCapabilities BuildCapabilities(string account)
        {
            BackendCapabilities capabilities = new BackendCapabilities { Account = account };
            capabilities.Modes.AddRange(CodexPermissions.Supported);
            capabilities.Commands.Add(new SlashCommandInfo { Name = "compact", Description = "Summarize the conversation to free context" });
            capabilities.Commands.Add(new SlashCommandInfo { Name = "clear", Description = "Start a new Codex thread" });
            foreach (JObject model in _models)
            {
                capabilities.Models.Add(new ModelOption
                {
                    Value = (string)model["model"],
                    DisplayName = (string)model["displayName"] ?? (string)model["model"],
                    Description = (string)model["description"],
                });

                if (!(model["supportedReasoningEfforts"] is JArray efforts))
                {
                    continue;
                }

                foreach (JToken effort in efforts)
                {
                    string value = (string)effort["reasoningEffort"];
                    if (!string.IsNullOrEmpty(value) && !capabilities.Efforts.Contains(value))
                    {
                        capabilities.Efforts.Add(value);
                    }
                }
            }

            return capabilities;
        }

        private async Task OpenThreadAsync()
        {
            if (string.IsNullOrEmpty(_resumeThreadId))
            {
                await StartThreadAsync();
                return;
            }

            JToken response;
            try
            {
                response = await SendRequestAsync("thread/resume", ThreadParameters(true), InitializeTimeoutMs);
            }
            catch (InvalidOperationException exception)
            {
                _sink.Notice(NoticeLevel.Warning, "Codex could not resume thread " + _resumeThreadId + " (" + exception.Message
                    + "), so Parley started a new one. Earlier messages stay in this window, but Codex does not remember them.");
                await StartThreadAsync();
                return;
            }

            ApplyThread(response);
        }

        private async Task StartThreadAsync()
        {
            _resumeThreadId = null;
            JToken response = await SendRequestAsync("thread/start", ThreadParameters(false), InitializeTimeoutMs);
            ApplyThread(response);
        }

        private JObject ThreadParameters(bool resume)
        {
            JObject parameters = new JObject
            {
                ["cwd"] = ProjectPaths.Root,
                ["approvalPolicy"] = CodexPermissions.ApprovalPolicy(Mode),
                ["sandbox"] = CodexPermissions.SandboxMode(Mode),
                ["developerInstructions"] = BuildDeveloperInstructions(),
            };

            if (_model != null)
            {
                parameters["model"] = _model;
            }

            if (resume)
            {
                parameters["threadId"] = _resumeThreadId;
                parameters["excludeTurns"] = true;
            }
            else if (_tools.HasTools)
            {
                parameters["dynamicTools"] = _tools.Specs();
            }

            return parameters;
        }

        private void ApplyThread(JToken response)
        {
            string threadId = (string)response["thread"]?["id"];
            if (string.IsNullOrEmpty(threadId))
            {
                throw new InvalidOperationException("Codex did not return a thread.");
            }

            _threadId = threadId;
            _resumeThreadId = threadId;
            _activeModel = (string)response["model"] ?? _activeModel;
            WarnIfModelUnavailable();
            _collaborationMode = (string)(response["collaborationMode"] as JObject)?["mode"];
            _sink.SessionStarted(new SessionInfo
            {
                SessionId = threadId,
                Model = _activeModel,
                Mode = Mode,
                Cwd = (string)response["cwd"] ?? ProjectPaths.Root,
            });
        }

        private void WarnIfModelUnavailable()
        {
            if (_model != null || _models.Count == 0 || string.IsNullOrEmpty(_activeModel))
            {
                return;
            }

            foreach (JObject model in _models)
            {
                if ((string)model["model"] == _activeModel)
                {
                    return;
                }
            }

            _sink.Notice(NoticeLevel.Warning, "Codex is configured to use '" + _activeModel
                + "', which this account does not list. If turns fail, pick another model in the model menu.");
        }

        private string BuildDeveloperInstructions()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("You are running inside the Unity Editor through Parley. Project '").Append(PlayerSettings.productName)
                .Append("', Unity ").Append(Application.unityVersion).Append(", project root ").Append(ProjectPaths.Root).Append('.');
            if (_tools.HasTools)
            {
                builder.Append(" Unity editor tools are available in the '").Append(CodexToolBridge.Namespace)
                    .Append("' tool namespace. After changing C# scripts call its refresh tool and fix the compiler errors it reports.");
            }

            builder.Append(" Do not edit files under Library/ or Temp/, and never change GUIDs in .meta files by hand.");
            string extra = ParleyProjectSettings.instance.AppendSystemPrompt;
            if (!string.IsNullOrWhiteSpace(extra))
            {
                builder.Append("\n\n").Append(extra.Trim());
            }

            return builder.ToString();
        }

        private async Task StartTurnAsync(JArray input)
        {
            MarkBusy();
            try
            {
                JToken response = await SendRequestAsync("turn/start", TurnParameters(input), RequestTimeoutMs);
                if (IsBusy)
                {
                    _turnId = (string)response["turn"]?["id"] ?? _turnId;
                }
            }
            catch (Exception)
            {
                ReleaseBusy();
                throw;
            }
        }

        private JObject TurnParameters(JArray input)
        {
            JObject parameters = new JObject
            {
                ["threadId"] = _threadId,
                ["input"] = input,
                ["approvalPolicy"] = CodexPermissions.ApprovalPolicy(Mode),
                ["sandboxPolicy"] = CodexPermissions.SandboxPolicy(Mode, ParleyProjectSettings.instance.AdditionalDirectories),
            };

            if (_model != null)
            {
                parameters["model"] = _model;
            }

            if (_effort != null)
            {
                parameters["effort"] = _effort;
            }

            string collaborationMode = CodexPermissions.CollaborationMode(Mode);
            string model = _model ?? _activeModel;
            bool switchesMode = collaborationMode == CodexPermissions.PlanCollaboration || _collaborationMode == CodexPermissions.PlanCollaboration;
            if (switchesMode && !string.IsNullOrEmpty(model))
            {
                JObject settings = new JObject { ["model"] = model, ["reasoning_effort"] = _effort, ["developer_instructions"] = null };
                parameters["collaborationMode"] = new JObject { ["mode"] = collaborationMode, ["settings"] = settings };
                _collaborationMode = collaborationMode;
            }

            return parameters;
        }

        private void RespondToPlan(Decision decision)
        {
            if (decision.Allow)
            {
                SetPermissionMode(decision.NextMode ?? PermissionMode.Default);
                _ = SendFollowUpAsync(ImplementPlanMessage);
                return;
            }

            if (!string.IsNullOrWhiteSpace(decision.Message))
            {
                _ = SendFollowUpAsync(decision.Message);
            }
        }

        private async Task SendFollowUpAsync(string text)
        {
            try
            {
                await StartTurnAsync(CodexWire.TextInput(text));
            }
            catch (Exception exception)
            {
                _sink.Notice(NoticeLevel.Error, exception.Message);
            }
        }

        private void RaisePlanApproval(JObject turn)
        {
            string plan = _mapper.PlanText;
            string itemId = _mapper.PlanItemId;
            _mapper.ClearPlan();
            if (Mode != PermissionMode.Plan || string.IsNullOrWhiteSpace(plan) || (string)turn?["status"] != "completed")
            {
                return;
            }

            CancelPlanRequest();
            _planRequest = new PendingRequest
            {
                Id = "plan_" + Guid.NewGuid().ToString("N"),
                ToolName = PendingRequest.ExitPlanModeTool,
                Input = new JObject { ["plan"] = plan },
                ToolUseId = itemId,
            };

            _sink.RequestRaised(_planRequest);
        }

        private void CancelPlanRequest()
        {
            if (_planRequest == null)
            {
                return;
            }

            _sink.RequestCancelled(_planRequest.Id);
            _planRequest = null;
        }

        private void InterruptTurn()
        {
            if (!IsRunning || _interruptRequested)
            {
                return;
            }

            foreach (KeyValuePair<string, string> child in _childTurns)
            {
                FireRequest("turn/interrupt", new JObject { ["threadId"] = child.Key, ["turnId"] = child.Value });
            }

            if (_turnId != null)
            {
                FireRequest("turn/interrupt", new JObject { ["threadId"] = _threadId, ["turnId"] = _turnId });
            }

            _interruptRequested = _turnId != null || _childTurns.Count > 0;
            _interruptRequestedAt = EditorApplication.timeSinceStartup;
        }

        private void ForceStop()
        {
            StopProcess();
            _sink.Notice(NoticeLevel.Warning, "Codex did not stop, so Parley restarted it. The conversation continues with your next message.");
            _mapper.Handle("turn/completed", new JObject { ["turn"] = new JObject { ["status"] = "interrupted" } });
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
            string credentialsPath = CodexEnvironment.ManagedCredentialsPath(_profile);
            Task.Run(() => FinishProcess(process, processId, credentialsPath));
        }

        private void Detach(ChildProcess process)
        {
            process.OnStdoutLine -= StdoutLineHandler;
            process.OnExited -= ExitedHandler;
            _process = null;
            _resumeThreadId = _threadId ?? _resumeThreadId;
            _threadId = null;
            _turnId = null;
            _childTurns.Clear();
            _interruptRequested = false;
            ReleaseBusy();
            foreach (TaskCompletionSource<JToken> completion in _pendingCalls.Values)
            {
                completion.TrySetException(new InvalidOperationException("Codex exited."));
            }

            _pendingCalls.Clear();
            foreach (string requestId in new List<string>(_openRequests.Keys))
            {
                _sink.RequestCancelled(requestId);
            }

            _openRequests.Clear();
            CancelPlanRequest();
        }

        private bool Write(JObject message)
        {
            return _process != null && _process.WriteLine(CodexWire.Serialize(message));
        }

        private void FireRequest(string method, JObject parameters)
        {
            _ = SendRequestSafeAsync(method, parameters);
        }

        private async Task SendRequestSafeAsync(string method, JObject parameters)
        {
            try
            {
                await SendRequestAsync(method, parameters, RequestTimeoutMs);
            }
            catch (Exception exception)
            {
                _sink.Notice(NoticeLevel.Warning, method + " failed: " + exception.Message);
            }
        }

        private async Task<JToken> SendRequestAsync(string method, JObject parameters, int timeoutMs)
        {
            long id = ++_requestCounter;
            TaskCompletionSource<JToken> completion = new TaskCompletionSource<JToken>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingCalls[id] = completion;
            if (!Write(CodexWire.Request(id, method, parameters)))
            {
                _pendingCalls.Remove(id);
                throw new InvalidOperationException("Codex is not running.");
            }

            Task finished = await Task.WhenAny(completion.Task, Task.Delay(timeoutMs));
            if (finished != completion.Task)
            {
                _pendingCalls.Remove(id);
                throw new TimeoutException(method + " timed out.");
            }

            return await completion.Task;
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

        private void HandleResponse(JObject message)
        {
            JToken id = message["id"];
            if (id == null || id.Type != JTokenType.Integer || !_pendingCalls.TryGetValue((long)id, out TaskCompletionSource<JToken> completion))
            {
                return;
            }

            _pendingCalls.Remove((long)id);
            if (message["error"] is JObject error)
            {
                completion.TrySetException(new InvalidOperationException((string)error["message"] ?? "Codex request failed."));
            }
            else
            {
                completion.TrySetResult(message["result"] ?? new JObject());
            }
        }

        private void HandleServerRequest(JObject message)
        {
            JToken rpcId = message["id"];
            string method = (string)message["method"];
            JObject parameters = message["params"] as JObject ?? new JObject();
            if (method == ToolCallMethod)
            {
                _ = HandleToolCallAsync(rpcId, parameters);
                return;
            }

            if (!CodexApprovals.IsSupported(method))
            {
                Write(CodexWire.Error(rpcId, -32601, "Unsupported request: " + method));
                return;
            }

            CodexServerRequest server = new CodexServerRequest { RpcId = rpcId, Method = method, Params = parameters };
            JObject item = _mapper.FindItem((string)parameters["itemId"]);
            server.Request = CodexApprovals.Parse(server, item);
            _openRequests[server.Request.Id] = server;
            _sink.RequestRaised(server.Request);
        }

        private async Task HandleToolCallAsync(JToken rpcId, JObject parameters)
        {
            JObject response;
            try
            {
                response = await _tools.CallAsync(parameters, _lifetime.Token);
            }
            catch (Exception exception)
            {
                Write(CodexWire.Error(rpcId, -32603, exception.Message));
                return;
            }

            Write(CodexWire.Response(rpcId, response));
        }

        private void HandleNotification(string method, JObject parameters)
        {
            string threadId = (string)parameters["threadId"];
            bool child = threadId != null && _threadId != null && threadId != _threadId;
            switch (method)
            {
                case "turn/started":
                    if (child)
                    {
                        _childTurns[threadId] = (string)parameters["turn"]?["id"];
                        return;
                    }

                    _turnId = (string)parameters["turn"]?["id"];
                    MarkBusy();
                    break;
                case "turn/completed":
                    if (child)
                    {
                        _childTurns.Remove(threadId);
                        return;
                    }

                    break;
                case "thread/tokenUsage/updated":
                    if (child)
                    {
                        return;
                    }

                    break;
                case "serverRequest/resolved":
                    string requestId = CodexWire.RequestKey(parameters["requestId"]);
                    if (requestId != null && _openRequests.Remove(requestId))
                    {
                        _sink.RequestCancelled(requestId);
                    }

                    break;
            }

            if (method != "turn/completed")
            {
                _mapper.Handle(method, parameters);
                return;
            }

            try
            {
                _mapper.Handle(method, parameters);
            }
            finally
            {
                _turnId = null;
                _interruptRequested = false;
                ReleaseBusy();
                RaisePlanApproval(parameters["turn"] as JObject);
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
                string method = (string)message["method"];
                if (method == null)
                {
                    HandleResponse(message);
                }
                else if (message["id"] != null)
                {
                    HandleServerRequest(message);
                }
                else
                {
                    HandleNotification(method, message["params"] as JObject ?? new JObject());
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Parley] Failed to handle Codex message: " + exception.Message + "\n" + line);
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
            CodexEnvironment.DeleteCredentials(CodexEnvironment.ManagedCredentialsPath(_profile));
            if (_disposed)
            {
                return;
            }

            string lower = stderr.ToLowerInvariant();
            if (lower.Contains("not logged in") || lower.Contains("unauthorized") || lower.Contains("401") || lower.Contains("api key"))
            {
                _sink.AuthRequired(LastLines(stderr, 6));
            }

            _sink.Exited("Codex exited with code " + code + (stderr.Length > 0 ? ":\n" + LastLines(stderr, 12) : "."), true);
        }
    }
}