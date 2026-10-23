using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Sessions
{
	internal sealed class UsageTracker
	{
		public const string MainAgent = "main";

		private const string SyntheticModel = "<synthetic>";
		private const int PromptLimit = 300;
		private const int TargetLimit = 120;
		private static readonly string[] TargetFields = { "file_path", "notebook_path", "path", "pattern", "command", "url", "query", "description", "prompt" };
		private static readonly string[] SubagentTools = { "Agent", "Task" };

		private readonly Dictionary<string, UsageCall> _calls = new ();
		private readonly Dictionary<string, UsageCall> _currentCalls = new ();
		private readonly Dictionary<string, UsageTool> _tools = new ();
		private readonly string _projectRoot;

		private UsageLog _log;
		private double? _costBaseline;

		public UsageTracker(string projectRoot)
		{
			_projectRoot = string.IsNullOrEmpty(projectRoot) ? null : projectRoot.Replace('\\', '/').TrimEnd('/') + "/";
		}

		public void Bind(UsageLog log)
		{
			_log = log;
			_calls.Clear();
			_currentCalls.Clear();
			_tools.Clear();
			_costBaseline = null;
			foreach (UsageTurn turn in log.Turns)
			{
				foreach (UsageCall call in turn.Calls)
				{
					if (!string.IsNullOrEmpty(call.Id))
					{
						_calls[call.Id] = call;
					}
				}
			}
		}

		public void BeginTurn(string prompt, DateTime nowUtc)
		{
			_currentCalls.Clear();
			_tools.Clear();
			_log.Turns.Add(new UsageTurn
			{
				Index = _log.Turns.Count + 1,
				StartedUtc = nowUtc,
				Prompt = Truncate(prompt, PromptLimit),
			});
		}

		public bool Report(UsageReport report, string fallbackModel, DateTime nowUtc)
		{
			if (report.Model == SyntheticModel || IsEmpty(report))
			{
				return false;
			}

			string agent = AgentOf(report.ParentToolUseId);
			string id = string.IsNullOrEmpty(report.CallId) ? "call-" + Guid.NewGuid().ToString("N").Substring(0, 12) : report.CallId;
			if (!_calls.TryGetValue(id, out UsageCall call))
			{
				call = new UsageCall { Id = id, TimeUtc = nowUtc, Agent = agent };
				_calls[id] = call;
				CurrentTurn(nowUtc).Calls.Add(call);
			}

			call.Model = string.IsNullOrEmpty(report.Model) ? call.Model ?? (string.IsNullOrEmpty(fallbackModel) ? null : fallbackModel) : report.Model;
			call.Input = Math.Max(call.Input, report.InputTokens);
			call.CacheWrite = Math.Max(call.CacheWrite, report.CacheCreationTokens);
			call.CacheRead = Math.Max(call.CacheRead, report.CacheReadTokens);
			call.Output = Math.Max(call.Output, report.OutputTokens);
			call.Reasoning = Math.Max(call.Reasoning, report.ReasoningTokens);
			_currentCalls[agent] = call;
			return true;
		}

		public void BlockFinalized(BlockFinalizeRequest request)
		{
			if (_log == null || _log.Turns.Count == 0)
			{
				return;
			}

			string agent = AgentOf(request.ParentToolUseId);
			_currentCalls.TryGetValue(agent, out UsageCall call);
			if (request.Kind == BlockKind.Thinking)
			{
				if (call != null)
				{
					call.ThinkingChars += request.Text?.Length ?? 0;
				}

				return;
			}

			if (request.Kind != BlockKind.ToolUse || string.IsNullOrEmpty(request.ToolUseId) || _tools.ContainsKey(request.ToolUseId))
			{
				return;
			}

			UsageTool tool = new UsageTool
			{
				Id = request.ToolUseId,
				Name = request.ToolName,
				Agent = agent,
				CallId = call?.Id,
				Target = Target(request.Input),
				InputChars = request.Input == null ? 0 : request.Input.ToString(Formatting.None).Length,
			};

			_tools[tool.Id] = tool;
			_log.Turns[_log.Turns.Count - 1].Tools.Add(tool);
			if (Array.IndexOf(SubagentTools, request.ToolName) >= 0)
			{
				_log.AgentLabels[tool.Id] = SubagentLabel(request.Input);
			}
		}

		public void ToolResult(ToolResultRequest request)
		{
			if (string.IsNullOrEmpty(request.ToolUseId) || !_tools.TryGetValue(request.ToolUseId, out UsageTool tool))
			{
				return;
			}

			tool.ResultChars = request.Text?.Length ?? 0;
			tool.IsError = request.IsError;
		}

		public void CompleteTurn(TurnResult result)
		{
			if (_log == null || _log.Turns.Count == 0)
			{
				return;
			}

			UsageTurn turn = _log.Turns[_log.Turns.Count - 1];
			turn.DurationMs += result.DurationMs;
			if (result.CostUsd.HasValue)
			{
				double value = result.CostUsd.Value;
				double cost = _costBaseline.HasValue && value >= _costBaseline.Value ? value - _costBaseline.Value : value;
				turn.CostUsd = (turn.CostUsd ?? 0) + cost;
				_costBaseline = value;
			}
		}

		private UsageTurn CurrentTurn(DateTime nowUtc)
		{
			if (_log.Turns.Count == 0)
			{
				BeginTurn(null, nowUtc);
			}

			return _log.Turns[_log.Turns.Count - 1];
		}

		private string Target(JObject input)
		{
			if (input == null)
			{
				return null;
			}

			foreach (string field in TargetFields)
			{
				if (input[field] is JValue value && value.Type == JTokenType.String)
				{
					string target = RelativePath((string)value);
					if (input["offset"] != null || input["limit"] != null)
					{
						target += " [" + input["offset"] + "+" + input["limit"] + "]";
					}

					return Truncate(target, TargetLimit);
				}
			}

			return null;
		}

		private string RelativePath(string text)
		{
			string normalized = text.Replace('\\', '/');
			return _projectRoot != null && normalized.StartsWith(_projectRoot, StringComparison.Ordinal) ? normalized.Substring(_projectRoot.Length) : text;
		}

		private static string SubagentLabel(JObject input)
		{
			string type = (string)input?["subagent_type"];
			string description = (string)input?["description"];
			if (string.IsNullOrEmpty(type))
			{
				return string.IsNullOrEmpty(description) ? "subagent" : description;
			}

			return string.IsNullOrEmpty(description) ? type : type + ": " + description;
		}

		private static string AgentOf(string parentToolUseId)
		{
			return string.IsNullOrEmpty(parentToolUseId) ? MainAgent : parentToolUseId;
		}

		private static bool IsEmpty(UsageReport report)
		{
			return report.InputTokens == 0 && report.CacheCreationTokens == 0 && report.CacheReadTokens == 0 && report.OutputTokens == 0 &&
				report.ReasoningTokens == 0;
		}

		private static string Truncate(string text, int limit)
		{
			if (string.IsNullOrEmpty(text))
			{
				return text;
			}

			string single = text.Replace('\n', ' ').Replace('\r', ' ').Trim();
			return single.Length <= limit ? single : single.Substring(0, limit - 1) + "…";
		}
	}
}
