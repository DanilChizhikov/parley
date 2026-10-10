using System;
using System.Collections.Generic;
using DTech.Parley.Editor;
using Newtonsoft.Json.Linq;

namespace DTech.Parley.Tests.EditorMode
{
	internal sealed class RecordingSink : IAgentSink
	{
		public List<string> Events { get; } = new ();
		public Dictionary<string, string> Texts { get; } = new ();
		public List<PendingRequest> Requests { get; } = new ();
		public List<(string id, string text, bool error)> ToolResults { get; } = new ();
		public List<(string id, string name, JObject input)> ToolUses { get; } = new ();
		public List<TurnResult> Turns { get; } = new ();
		public List<string> Notices { get; } = new ();
		public List<(long used, long max)> ContextUsages { get; } = new ();
		public List<List<McpServerStatus>> McpStatuses { get; } = new ();
		public List<List<SkillInfo>> Skills { get; } = new ();
		public SessionInfo Session { get; private set; }
		public BackendCapabilities Capabilities { get; private set; }
		public Action<PendingRequest> OnRequest { get; set; }

		public void CapabilitiesChanged(BackendCapabilities capabilities)
		{
			Capabilities = capabilities;
			Events.Add("caps");
		}

		public void SessionStarted(SessionInfo info)
		{
			Session = info;
			Events.Add("session");
		}

		public void BlockStarted(BlockStartRequest request)
		{
			Events.Add("start:" + request.Kind + ":" + request.Key);
		}

		public void TextDelta(string key, string delta)
		{
			Texts.TryGetValue(key, out string text);
			Texts[key] = text + delta;
		}

		public void ToolInputDelta(string key, string partialJson)
		{
			Events.Add("input:" + key);
		}

		public void BlockFinalized(BlockFinalizeRequest request)
		{
			Events.Add("final:" + request.Kind + ":" + request.Key);
			if (request.Kind == BlockKind.ToolUse)
			{
				ToolUses.Add((request.ToolUseId, request.ToolName, request.Input));
			}
			else
			{
				Texts[request.Key] = request.Text;
			}
		}

		public void ToolResult(ToolResultRequest request)
		{
			ToolResults.Add((request.ToolUseId, request.Text, request.IsError));
		}

		public void RequestRaised(PendingRequest request)
		{
			Requests.Add(request);
			OnRequest?.Invoke(request);
		}

		public void RequestCancelled(string requestId)
		{
			Events.Add("cancelled:" + requestId);
		}

		public void BackgroundTaskChanged(BackgroundTaskInfo task)
		{
			Events.Add("task:" + task.TaskId + ":" + task.Status);
		}

		public void ModeChanged(PermissionMode mode)
		{
			Events.Add("mode:" + mode);
		}

		public void ContextUsage(long usedTokens, long maxTokens)
		{
			ContextUsages.Add((usedTokens, maxTokens));
		}

		public void McpStatusChanged(IReadOnlyList<McpServerStatus> servers)
		{
			McpStatuses.Add(new List<McpServerStatus>(servers));
		}

		public void SkillsChanged(IReadOnlyList<SkillInfo> skills)
		{
			Skills.Add(new List<SkillInfo>(skills));
		}

		public void Notice(NoticeLevel level, string text)
		{
			Notices.Add(level + ":" + text);
		}

		public void TurnCompleted(TurnResult result)
		{
			Turns.Add(result);
		}

		public void AuthRequired(string message)
		{
			Events.Add("auth:" + message);
		}

		public void Exited(string reason, bool unexpected)
		{
			Events.Add("exited");
		}
	}
}