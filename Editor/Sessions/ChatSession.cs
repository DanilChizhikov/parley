using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DTech.Parley.Editor.Agents.Claude;
using DTech.Parley.Editor.Agents.Codex;
using DTech.Parley.Editor.Agents.Local;
using DTech.Parley.Editor.Mcp;
using DTech.Parley.Editor.Secrets;
using DTech.Parley.Editor.Settings;
using DTech.Parley.Editor.Skills;
using DTech.Parley.Editor.Tools;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace DTech.Parley.Editor.Sessions
{
	internal sealed class ChatSession : IAgentSink, IDisposable
	{
		public event Action<TranscriptEntry> OnEntryAdded;
		public event Action<TranscriptEntry, TranscriptBlock> OnBlockAdded;
		public event Action<TranscriptBlock> OnBlockChanged;
		public event Action<PendingRequest> OnRequestRaised;
		public event Action OnStateChanged;

		private const double DeltaFlushIntervalSeconds = 0.05;

		private readonly Dictionary<string, TranscriptBlock> _blocksByKey = new ();
		private readonly Dictionary<string, TranscriptBlock> _blocksByToolId = new ();
		private readonly Dictionary<string, TranscriptBlock> _requestBlocks = new ();
		private readonly Dictionary<string, PendingRequest> _requests = new ();
		private readonly Dictionary<string, TodoItem> _pendingTaskCreates = new ();
		private readonly Dictionary<TranscriptBlock, StringBuilder> _pendingText = new ();
		private readonly Dictionary<TranscriptBlock, StringBuilder> _pendingInput = new ();
		private readonly List<TranscriptBlock> _flushedBlocks = new ();
		private readonly List<TodoItem> _todos = new ();
		private readonly Dictionary<string, BackgroundTaskInfo> _tasks = new ();
		private readonly List<McpServerStatus> _mcpStatuses = new ();
		private readonly List<SkillInfo> _skills = new ();
		private readonly CancellationTokenSource _lifetime = new ();

		public ParleyProfile Profile { get; }

		public SessionRecord Record { get; }

		public IAgentBackend Backend { get; }

		public BackendCapabilities Capabilities { get; private set; } = new ();

		public SessionInfo Info { get; private set; }

		public PermissionMode Mode => Record.Mode;

		public string Model { get; private set; }

		public string Effort { get; private set; }

		public bool IsBusy => _sending || Backend.IsBusy;

		public bool IsStarting { get; private set; }

		public TurnResult LastTurn { get; private set; }

		public long ContextUsed { get; private set; }

		public long ContextMax { get; private set; }

		public string AuthMessage { get; private set; }

		public IReadOnlyList<TodoItem> Todos => _todos;

		public IReadOnlyCollection<BackgroundTaskInfo> Tasks => _tasks.Values;

		public IReadOnlyCollection<PendingRequest> OpenRequests => _requests.Values;

		public IReadOnlyList<McpServerStatus> McpStatuses => _mcpStatuses;

		public IReadOnlyList<SkillInfo> Skills => _skills;

		private TranscriptEntry _currentAssistant;
		private bool _sending;
		private bool _disposed;
		private bool _deltaFlushScheduled;
		private double _lastDeltaFlush;
		private string _appliedMcpFingerprint;
		private string _appliedSkillFingerprint;
		private SkillConfiguration _skillConfiguration = new ();

		public ChatSession(ParleyProfile profile, SessionRecord record)
		{
			Profile = profile;
			bool isNew = record == null;
			Record = record ?? new SessionRecord { Mode = ParleyUserSettings.instance.DefaultMode };
			if (Record.Kind != profile.Kind)
			{
				Record.BackendSessionId = null;
				Record.LocalHistory = null;
			}

			Record.ProfileId = profile.Id;
			Record.Kind = profile.Kind;
			Model = profile.Model;
			Effort = profile.Effort;
			ExpireUnresolvedRequests();
			Backend = profile.Kind switch
			{
				ProfileKind.ClaudeCode => new ClaudeCodeBackend(profile, this, ToolCatalog.CreateForMcp(ParleyProjectSettings.instance), Record.BackendSessionId, Record.Mode),
				ProfileKind.Codex => new CodexBackend(profile, this, ToolCatalog.CreateForMcp(ParleyProjectSettings.instance), Record.BackendSessionId, Record.Mode),
				ProfileKind.Local => new LocalAgentBackend(profile, this, ToolCatalog.CreateForLocalAgent(ParleyProjectSettings.instance), Record.BackendSessionId, Record.LocalHistory, Record.Mode),
				_ => throw new ArgumentOutOfRangeException(),
			};

			Record.Mode = Backend.Mode;
			InitializeMcp();
			ApplyMcp();
			InitializeSkills(isNew);
			ApplySkills();
		}

		public void Dispose()
		{
			if (_disposed)
			{
				return;
			}

			Save();
			_disposed = true;
			_lifetime.Cancel();
			Backend.Dispose();
			_lifetime.Dispose();
			OnEntryAdded = null;
			OnBlockAdded = null;
			OnBlockChanged = null;
			OnRequestRaised = null;
			OnStateChanged = null;
		}

		public async Task EnsureStartedAsync()
		{
			if (Backend.IsRunning || IsStarting)
			{
				return;
			}

			IsStarting = true;
			RaiseState();
			try
			{
				await Backend.StartAsync(_lifetime.Token);
			}
			catch (OperationCanceledException)
			{
			}
			catch (Exception exception)
			{
				ReportStartFailure(exception);
			}
			finally
			{
				IsStarting = false;
				RaiseState();
			}
		}

		public async Task SendAsync(string text, List<ChatAttachment> attachments)
		{
			UserTurn turn = new UserTurn { Text = text ?? string.Empty, Attachments = attachments ?? new List<ChatAttachment>() };
			List<SkillInvocation> autoSkills = null;
			if (Record.PendingAutoSkills != null && Record.PendingAutoSkills.Count > 0)
			{
				autoSkills = new List<SkillInvocation>(Record.PendingAutoSkills);
				Record.PendingAutoSkills.Clear();
				AddAutoSkills(turn, autoSkills);
			}

			AddUserEntry(turn);
			if (Record.Entries.Count <= 2 || Record.Title == SessionRecord.DefaultTitle)
			{
				Record.Title = SessionStore.MakeTitle(turn.Text);
			}

			AuthMessage = null;
			_sending = true;
			RaiseState();
			try
			{
				await Backend.SendAsync(turn, _lifetime.Token);
			}
			catch (OperationCanceledException)
			{
				RestoreAutoSkills(autoSkills);
			}
			catch (Exception exception)
			{
				RestoreAutoSkills(autoSkills);
				ReportStartFailure(exception);
			}
			finally
			{
				_sending = false;
				RaiseState();
			}
		}

		public void Interrupt()
		{
			Backend.Interrupt();
		}

		public void SetMode(PermissionMode mode)
		{
			Backend.SetPermissionMode(mode);
		}

		public void SetModel(string model)
		{
			Model = model;
			Backend.SetModel(model);
			RaiseState();
		}

		public void SetEffort(string effort)
		{
			Effort = effort;
			Backend.SetEffort(effort);
			RaiseState();
		}

		public void StopTask(string taskId)
		{
			Backend.StopTask(taskId);
		}

		public bool IsMcpServerEnabled(string definitionId)
		{
			return Record.EnabledMcpServerIds.Contains(definitionId);
		}

		public bool IsExternalMcpServerEnabled(string name)
		{
			return !Record.DisabledExternalMcpServers.Contains(name);
		}

		public void SetMcpServerEnabled(string definitionId, bool enabled)
		{
			if (enabled == IsMcpServerEnabled(definitionId))
			{
				return;
			}

			if (enabled)
			{
				Record.EnabledMcpServerIds.Add(definitionId);
			}
			else
			{
				Record.EnabledMcpServerIds.Remove(definitionId);
			}

			ApplyMcp();
			Save();
		}

		public void SetExternalMcpServerEnabled(string name, bool enabled)
		{
			if (enabled == IsExternalMcpServerEnabled(name))
			{
				return;
			}

			if (enabled)
			{
				Record.DisabledExternalMcpServers.Remove(name);
			}
			else
			{
				Record.DisabledExternalMcpServers.Add(name);
			}

			ApplyMcp();
			Save();
		}

		public void ReapplyMcp()
		{
			ApplyMcp();
		}

		public void RefreshMcpStatus()
		{
			Backend.RefreshMcpStatus();
		}

		public McpServerStatus FindMcpStatus(string name, bool external)
		{
			foreach (McpServerStatus status in _mcpStatuses)
			{
				if (status.IsExternal == external && status.Name == name)
				{
					return status;
				}
			}

			return null;
		}

		public bool IsLibrarySkillEnabled(string name)
		{
			return Record.EnabledLibrarySkills.Contains(name);
		}

		public bool IsExternalSkillEnabled(string name)
		{
			return !Record.DisabledExternalSkills.Contains(name);
		}

		public bool IsSkillEnabled(string name)
		{
			return _skillConfiguration.IsEnabled(name);
		}

		public void SetLibrarySkillEnabled(string name, bool enabled)
		{
			if (enabled == IsLibrarySkillEnabled(name))
			{
				return;
			}

			if (enabled)
			{
				Record.EnabledLibrarySkills.Add(name);
			}
			else
			{
				Record.EnabledLibrarySkills.Remove(name);
			}

			ApplySkills();
			Save();
		}

		public void SetExternalSkillEnabled(string name, bool enabled)
		{
			if (enabled == IsExternalSkillEnabled(name))
			{
				return;
			}

			if (enabled)
			{
				Record.DisabledExternalSkills.Remove(name);
			}
			else
			{
				Record.DisabledExternalSkills.Add(name);
			}

			ApplySkills();
			Save();
		}

		public void RenameLibrarySkill(string previousName, string name)
		{
			int index = Record.EnabledLibrarySkills.IndexOf(previousName);
			if (index >= 0)
			{
				Record.EnabledLibrarySkills[index] = name;
			}

			ApplySkills();
			Save();
		}

		public void ReapplySkills()
		{
			ApplySkills();
		}

		public void RefreshSkills()
		{
			Backend.RefreshSkills();
		}

		public void SkipPendingAutoSkill(string name)
		{
			if (Record.PendingAutoSkills != null && Record.PendingAutoSkills.RemoveAll(skill => skill.Name == name) > 0)
			{
				Save();
				RaiseState();
			}
		}

		public void ClearAuthMessage()
		{
			AuthMessage = null;
			RaiseState();
		}

		public PendingRequest FindRequest(string requestId)
		{
			return requestId != null && _requests.TryGetValue(requestId, out PendingRequest request) ? request : null;
		}

		public void Respond(string requestId, Decision decision, string resolution)
		{
			if (!_requests.TryGetValue(requestId, out PendingRequest request))
			{
				return;
			}

			_requests.Remove(requestId);
			Backend.Respond(request, decision);
			if (_requestBlocks.TryGetValue(requestId, out TranscriptBlock block))
			{
				block.Resolution = resolution ?? (decision.Allow ? "Allowed" : "Denied");
				OnBlockChanged?.Invoke(block);
			}

			RaiseState();
		}

		public void Save()
		{
			if (_disposed)
			{
				return;
			}

			FlushDeltas();
			Record.BackendSessionId = Backend.SessionId;
			if (Backend is LocalAgentBackend local)
			{
				Record.LocalHistory = new List<JObject>(local.History);
			}

			SessionStore.Save(Record);
			if (Record.Entries.Count > 0)
			{
				ParleyUserSettings.instance.LastSessionId = Record.Id;
			}
		}

		private void InitializeMcp()
		{
			Record.DisabledExternalMcpServers ??= new List<string>();
			if (Record.EnabledMcpServerIds != null)
			{
				return;
			}

			Record.EnabledMcpServerIds = new List<string>();
			foreach (McpServerDefinition definition in ParleyUserSettings.instance.McpServers)
			{
				if (definition.EnabledByDefault)
				{
					Record.EnabledMcpServerIds.Add(definition.Id);
				}
			}
		}

		private void ApplyMcp()
		{
			McpConfiguration configuration = BuildMcpConfiguration();
			string fingerprint = McpServerResolver.Fingerprint(configuration);
			if (fingerprint != _appliedMcpFingerprint)
			{
				_appliedMcpFingerprint = fingerprint;
				Backend.SetMcpConfiguration(configuration);
			}

			RaiseState();
		}

		private McpConfiguration BuildMcpConfiguration()
		{
			McpConfiguration configuration = new McpConfiguration();
			ParleyUserSettings settings = ParleyUserSettings.instance;
			ISecretStore store = SecretStores.Default;
			Record.EnabledMcpServerIds.RemoveAll(id => settings.FindMcpServer(id) == null);
			foreach (string id in Record.EnabledMcpServerIds)
			{
				configuration.Servers.Add(McpServerResolver.Resolve(settings.FindMcpServer(id), store));
			}

			if (Profile.Kind != ProfileKind.Local)
			{
				foreach (string name in Record.DisabledExternalMcpServers)
				{
					if (!configuration.HasServer(name))
					{
						configuration.DisabledExternal.Add(name);
					}
				}
			}

			return configuration;
		}

		private void InitializeSkills(bool isNew)
		{
			ParleyUserSettings settings = ParleyUserSettings.instance;
			Record.DisabledExternalSkills ??= new List<string>();
			if (Record.EnabledLibrarySkills == null)
			{
				Record.EnabledLibrarySkills = new List<string>();
				foreach (string name in SkillLibrary.Names())
				{
					if (settings.IsLibrarySkillDefault(name))
					{
						Record.EnabledLibrarySkills.Add(name);
					}
				}
			}

			if (!isNew)
			{
				return;
			}

			Record.PendingAutoSkills = new List<SkillInvocation>();
			foreach (AutoSkill skill in settings.AutoSkills)
			{
				if (!string.IsNullOrWhiteSpace(skill.Name))
				{
					Record.PendingAutoSkills.Add(new SkillInvocation { Name = skill.Name.Trim(), Arguments = skill.Arguments ?? string.Empty });
				}
			}
		}

		private void ApplySkills()
		{
			_skillConfiguration = BuildSkillConfiguration();
			string fingerprint = _skillConfiguration.Fingerprint();
			if (fingerprint != _appliedSkillFingerprint)
			{
				_appliedSkillFingerprint = fingerprint;
				Backend.SetSkillConfiguration(_skillConfiguration);
			}

			RaiseState();
		}

		private SkillConfiguration BuildSkillConfiguration()
		{
			SkillConfiguration configuration = new SkillConfiguration { LibraryRoot = SkillLibrary.Root, LibraryFolder = SkillLibrary.Folder };
			foreach (string name in SkillLibrary.Names())
			{
				configuration.LibraryNames.Add(name);
			}

			Record.EnabledLibrarySkills.RemoveAll(name => !configuration.LibraryNames.Contains(name));
			foreach (string name in configuration.LibraryNames)
			{
				if (!Record.EnabledLibrarySkills.Contains(name))
				{
					configuration.Disabled.Add(name);
				}
			}

			foreach (string name in Record.DisabledExternalSkills)
			{
				if (!configuration.LibraryNames.Contains(name))
				{
					configuration.Disabled.Add(name);
				}
			}

			return configuration;
		}

		private void AddAutoSkills(UserTurn turn, List<SkillInvocation> skills)
		{
			List<string> commands = new ();
			foreach (SkillInvocation skill in skills)
			{
				if (IsSkillEnabled(skill.Name))
				{
					turn.Skills.Add(new SkillInvocation { Name = skill.Name, Arguments = skill.Arguments });
					commands.Add(skill.ToCommand());
				}
			}

			if (commands.Count > 0)
			{
				AddNotice(NoticeLevel.Info, "Auto skills: " + string.Join(", ", commands));
			}
		}

		private void RestoreAutoSkills(List<SkillInvocation> skills)
		{
			if (skills != null && Record.PendingAutoSkills != null && Record.PendingAutoSkills.Count == 0)
			{
				Record.PendingAutoSkills.AddRange(skills);
			}
		}

		private void ReportStartFailure(Exception exception)
		{
			AddNotice(NoticeLevel.Error, exception.Message);
			if (exception is AgentSetupException)
			{
				AuthMessage = exception.Message;
			}
		}

		private void AddUserEntry(UserTurn turn)
		{
			_currentAssistant = null;
			TranscriptEntry entry = new TranscriptEntry { Role = EntryRole.User };
			if (!string.IsNullOrEmpty(turn.Text))
			{
				entry.Blocks.Add(new TranscriptBlock { Kind = BlockKind.Text, Text = turn.Text, IsFinished = true });
			}

			foreach (ChatAttachment attachment in turn.Attachments)
			{
				entry.Blocks.Add(attachment.Kind == AttachmentKind.Image
					? new TranscriptBlock { Kind = BlockKind.Image, Text = attachment.Label, MediaType = attachment.MediaType, Data = attachment.Base64, IsFinished = true }
					: new TranscriptBlock { Kind = BlockKind.Notice, Text = "Attached: " + attachment.Label, IsFinished = true });
			}

			Record.Entries.Add(entry);
			OnEntryAdded?.Invoke(entry);
		}

		private void AddAssistantBlock(TranscriptBlock block)
		{
			if (_currentAssistant == null)
			{
				_currentAssistant = new TranscriptEntry { Role = EntryRole.Assistant };
				Record.Entries.Add(_currentAssistant);
				OnEntryAdded?.Invoke(_currentAssistant);
			}

			_currentAssistant.Blocks.Add(block);
			OnBlockAdded?.Invoke(_currentAssistant, block);
		}

		private void AddNotice(NoticeLevel level, string text)
		{
			if (string.IsNullOrWhiteSpace(text))
			{
				return;
			}

			_currentAssistant = null;
			TranscriptEntry entry = new TranscriptEntry { Role = EntryRole.Notice };
			entry.Blocks.Add(new TranscriptBlock { Kind = BlockKind.Notice, Level = level, Text = text.Trim(), IsFinished = true });
			Record.Entries.Add(entry);
			OnEntryAdded?.Invoke(entry);
		}

		private void ExpireUnresolvedRequests()
		{
			foreach (TranscriptEntry entry in Record.Entries)
			{
				foreach (TranscriptBlock block in entry.Blocks)
				{
					if (block.Kind == BlockKind.Request && string.IsNullOrEmpty(block.Resolution))
					{
						block.Resolution = "Expired";
					}

					if (block.Kind == BlockKind.ToolUse && !block.IsFinished)
					{
						block.IsFinished = true;
					}
				}
			}
		}

		private void TrackToolUse(TranscriptBlock block)
		{
			JObject input = block.Input;
			switch (block.ToolName)
			{
				case "TodoWrite":
					if (input["todos"] is JArray todos)
					{
						_todos.Clear();
						int index = 0;
						foreach (JToken todo in todos)
						{
							_todos.Add(new TodoItem
							{
								Id = (string)todo["id"] ?? (index++).ToString(),
								Content = (string)todo["content"],
								Status = (string)todo["status"] ?? "pending",
								ActiveForm = (string)todo["activeForm"],
							});
						}

						RaiseState();
					}

					break;
				case "TaskCreate":
					_pendingTaskCreates[block.ToolUseId ?? string.Empty] = new TodoItem
					{
						Content = (string)input["subject"] ?? (string)input["description"],
						ActiveForm = (string)input["activeForm"] ?? (string)input["active_form"],
						Status = "pending",
					};

					break;
				case "TaskUpdate":
					string taskId = (string)input["taskId"] ?? (string)input["id"] ?? (string)input["task_id"];
					TodoItem item = _todos.Find(todo => todo.Id == taskId);
					if (item == null)
					{
						break;
					}

					string status = (string)input["status"];
					if (status == "deleted")
					{
						_todos.Remove(item);
					}
					else
					{
						item.Status = status ?? item.Status;
						item.ActiveForm = (string)input["activeForm"] ?? (string)input["active_form"] ?? item.ActiveForm;
						item.Content = (string)input["subject"] ?? item.Content;
					}

					RaiseState();
					break;
				case PendingRequest.ExitPlanModeTool:
					string plan = (string)input["plan"];
					if (!string.IsNullOrEmpty(plan))
					{
						Record.LatestPlan = plan;
						RaiseState();
					}

					break;
			}
		}

		private void TrackToolResult(TranscriptBlock block)
		{
			if (block.ToolName != "TaskCreate" || block.IsError || !_pendingTaskCreates.TryGetValue(block.ToolUseId ?? string.Empty, out TodoItem item))
			{
				return;
			}

			_pendingTaskCreates.Remove(block.ToolUseId ?? string.Empty);
			string id = ((block.StructuredResult as JObject)?["task"] as JObject)?["id"]?.ToString();
			if (string.IsNullOrEmpty(id))
			{
				return;
			}

			item.Id = id;
			_todos.Add(item);
			RaiseState();
		}

		private bool ContainsSkill(string name)
		{
			foreach (SkillInfo skill in _skills)
			{
				if (skill.Name == name)
				{
					return true;
				}
			}

			return false;
		}

		private void RaiseState()
		{
			OnStateChanged?.Invoke();
		}

		private void QueueDelta(Dictionary<TranscriptBlock, StringBuilder> pending, TranscriptBlock block, string delta)
		{
			if (!pending.TryGetValue(block, out StringBuilder buffer))
			{
				buffer = new StringBuilder();
				pending[block] = buffer;
			}

			buffer.Append(delta);
			if (!_deltaFlushScheduled)
			{
				_deltaFlushScheduled = true;
				EditorApplication.update += DeltaFlushTick;
			}
		}

		private void DeltaFlushTick()
		{
			if (EditorApplication.timeSinceStartup - _lastDeltaFlush >= DeltaFlushIntervalSeconds)
			{
				FlushDeltas();
			}
		}

		private void FlushDeltas()
		{
			if (_deltaFlushScheduled)
			{
				_deltaFlushScheduled = false;
				EditorApplication.update -= DeltaFlushTick;
			}

			_lastDeltaFlush = EditorApplication.timeSinceStartup;
			if (_pendingText.Count == 0 && _pendingInput.Count == 0)
			{
				return;
			}

			foreach (KeyValuePair<TranscriptBlock, StringBuilder> pair in _pendingText)
			{
				pair.Key.Text += pair.Value.ToString();
				_flushedBlocks.Add(pair.Key);
			}

			foreach (KeyValuePair<TranscriptBlock, StringBuilder> pair in _pendingInput)
			{
				pair.Key.PartialInputJson += pair.Value.ToString();
				_flushedBlocks.Add(pair.Key);
			}

			_pendingText.Clear();
			_pendingInput.Clear();
			try
			{
				foreach (TranscriptBlock block in _flushedBlocks)
				{
					OnBlockChanged?.Invoke(block);
				}
			}
			finally
			{
				_flushedBlocks.Clear();
			}
		}

		private void RefreshAssetsIfIdle()
		{
			if (!Backend.IsBusy)
			{
				AssetDatabase.Refresh();
			}
		}

		void IAgentSink.CapabilitiesChanged(BackendCapabilities capabilities)
		{
			Capabilities = capabilities ?? new BackendCapabilities();
			RaiseState();
		}

		void IAgentSink.SessionStarted(SessionInfo info)
		{
			Info = info;
			if (!string.IsNullOrEmpty(info.Model))
			{
				Model = info.Model;
			}

			Record.Mode = info.Mode;
			if (Capabilities.Commands.Count == 0)
			{
				foreach (string command in info.SlashCommands)
				{
					Capabilities.Commands.Add(new SlashCommandInfo { Name = command });
				}
			}

			RaiseState();
		}

		void IAgentSink.BlockStarted(BlockStartRequest request)
		{
			if (_blocksByKey.ContainsKey(request.Key))
			{
				return;
			}

			TranscriptBlock block = new TranscriptBlock
			{
				Kind = request.Kind,
				ParentToolUseId = request.ParentToolUseId,
				ToolUseId = request.ToolUseId,
				ToolName = request.ToolName,
			};

			_blocksByKey[request.Key] = block;
			if (!string.IsNullOrEmpty(request.ToolName))
			{
				_blocksByToolId[request.ToolUseId] = block;
			}

			AddAssistantBlock(block);
		}

		void IAgentSink.TextDelta(string key, string delta)
		{
			if (string.IsNullOrEmpty(delta))
			{
				return;
			}

			if (!_blocksByKey.TryGetValue(key, out TranscriptBlock block))
			{
				block = new TranscriptBlock { Kind = BlockKind.Text };
				_blocksByKey[key] = block;
				AddAssistantBlock(block);
			}

			QueueDelta(_pendingText, block, delta);
		}

		void IAgentSink.ToolInputDelta(string key, string partialJson)
		{
			if (_blocksByKey.TryGetValue(key, out TranscriptBlock block) && !string.IsNullOrEmpty(partialJson))
			{
				QueueDelta(_pendingInput, block, partialJson);
			}
		}

		void IAgentSink.BlockFinalized(BlockFinalizeRequest request)
		{
			FlushDeltas();
			if (!_blocksByKey.TryGetValue(request.Key, out TranscriptBlock block))
			{
				block = new TranscriptBlock { Kind = request.Kind, ParentToolUseId = request.ParentToolUseId };
				_blocksByKey[request.Key] = block;
				AddAssistantBlock(block);
			}

			if (request.Kind == BlockKind.ToolUse)
			{
				block.ToolUseId = request.ToolUseId;
				block.ToolName = request.ToolName;
				block.Input = request.Input ?? new JObject();
				block.PartialInputJson = null;
				if (!string.IsNullOrEmpty(request.ToolUseId))
				{
					_blocksByToolId[request.ToolUseId] = block;
				}

				try
				{
					TrackToolUse(block);
				}
				catch (Exception exception)
				{
					UnityEngine.Debug.LogWarning($"[Parley] Could not read {request.ToolName} input: {exception.Message}");
				}
			}
			else if (!string.IsNullOrEmpty(request.Text) || string.IsNullOrEmpty(block.Text))
			{
				block.Text = request.Text ?? string.Empty;
			}

			block.IsFinished = request.Kind != BlockKind.ToolUse;
			OnBlockChanged?.Invoke(block);
		}

		void IAgentSink.ToolResult(ToolResultRequest request)
		{
			if (string.IsNullOrEmpty(request.ToolUseId) || !_blocksByToolId.TryGetValue(request.ToolUseId, out TranscriptBlock block))
			{
				return;
			}

			block.Result = request.Text;
			block.IsError = request.IsError;
			block.StructuredResult = request.Structured;
			block.IsFinished = true;
			TrackToolResult(block);
			OnBlockChanged?.Invoke(block);
		}

		void IAgentSink.RequestRaised(PendingRequest request)
		{
			_requests[request.Id] = request;
			TranscriptBlock block = new TranscriptBlock
			{
				Kind = BlockKind.Request,
				RequestId = request.Id,
				ToolName = request.ToolName,
				ToolUseId = request.ToolUseId,
				Input = request.Input,
				ParentToolUseId = null,
			};

			_requestBlocks[request.Id] = block;
			if (request.Kind == RequestKind.PlanApproval)
			{
				string plan = (string)request.Input["plan"];
				if (!string.IsNullOrEmpty(plan))
				{
					Record.LatestPlan = plan;
				}
			}

			AddAssistantBlock(block);
			OnRequestRaised?.Invoke(request);
			RaiseState();
		}

		void IAgentSink.RequestCancelled(string requestId)
		{
			if (!_requests.Remove(requestId))
			{
				return;
			}

			if (_requestBlocks.TryGetValue(requestId, out TranscriptBlock block) && string.IsNullOrEmpty(block.Resolution))
			{
				block.Resolution = "Cancelled";
				OnBlockChanged?.Invoke(block);
			}

			RaiseState();
		}

		void IAgentSink.BackgroundTaskChanged(BackgroundTaskInfo task)
		{
			if (string.IsNullOrEmpty(task.TaskId))
			{
				return;
			}

			if (_tasks.TryGetValue(task.TaskId, out BackgroundTaskInfo existing))
			{
				existing.Status = task.Status ?? existing.Status;
				existing.Description = task.Description ?? existing.Description;
				existing.LastToolName = task.LastToolName ?? existing.LastToolName;
				existing.Summary = task.Summary ?? existing.Summary;
				existing.TaskType = task.TaskType ?? existing.TaskType;
			}
			else
			{
				_tasks[task.TaskId] = task;
			}

			RaiseState();
		}

		void IAgentSink.ModeChanged(PermissionMode mode)
		{
			Record.Mode = mode;
			RaiseState();
		}

		void IAgentSink.ContextUsage(long usedTokens, long maxTokens)
		{
			ContextUsed = usedTokens;
			ContextMax = maxTokens;
			RaiseState();
		}

		void IAgentSink.McpStatusChanged(IReadOnlyList<McpServerStatus> servers)
		{
			_mcpStatuses.Clear();
			_mcpStatuses.AddRange(servers);
			if (Profile.Kind != ProfileKind.Local)
			{
				foreach (string name in Record.DisabledExternalMcpServers)
				{
					if (FindMcpStatus(name, true) == null)
					{
						_mcpStatuses.Add(new McpServerStatus { Name = name, State = McpConnectionState.Disabled, IsExternal = true });
					}
				}
			}

			RaiseState();
		}

		void IAgentSink.SkillsChanged(IReadOnlyList<SkillInfo> skills)
		{
			_skills.Clear();
			_skills.AddRange(skills);
			foreach (string name in Record.DisabledExternalSkills)
			{
				if (!ContainsSkill(name))
				{
					_skills.Add(new SkillInfo { Name = name });
				}
			}

			RaiseState();
		}

		void IAgentSink.Notice(NoticeLevel level, string text)
		{
			AddNotice(level, text);
		}

		void IAgentSink.TurnCompleted(TurnResult result)
		{
			LastTurn = result;
			if (result.CostUsd.HasValue)
			{
				Record.CostUsd = result.CostUsd.Value;
			}

			_currentAssistant = null;
			Save();
			EditorApplication.delayCall -= RefreshAssetsIfIdle;
			EditorApplication.delayCall += RefreshAssetsIfIdle;
			RaiseState();
		}

		void IAgentSink.AuthRequired(string message)
		{
			AuthMessage = string.IsNullOrWhiteSpace(message) ? "Sign-in required." : message;
			RaiseState();
		}

		void IAgentSink.Exited(string reason, bool unexpected)
		{
			if (unexpected)
			{
				AddNotice(NoticeLevel.Error, reason);
			}

			RaiseState();
		}
	}
}