using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Agents.Claude
{
	internal sealed class ClaudeStreamMapper
	{
		private static readonly Regex CommandTags = new ("</?(local-command-stdout|local-command-stderr|command-name|command-message|command-args|bash-stdout|bash-stderr)>");

		private readonly IAgentSink _sink;
		private readonly Dictionary<string, string> _currentMessage = new ();
		private readonly Dictionary<string, int> _finalBlocks = new ();

		public string SessionId { get; private set; }

		public string ConfirmedSessionId { get; private set; }

		private bool _assistantTextThisTurn;

		public ClaudeStreamMapper(IAgentSink sink)
		{
			_sink = sink;
		}

		public static string Key(string parent, string messageId, int index)
		{
			return (parent ?? string.Empty) + "|" + messageId + "|" + index;
		}

		public static string CleanCommandText(string text)
		{
			return CommandTags.Replace(text ?? string.Empty, string.Empty).Trim();
		}

		public void Reset()
		{
			SessionId = null;
			ConfirmedSessionId = null;
			_currentMessage.Clear();
			_finalBlocks.Clear();
			_assistantTextThisTurn = false;
		}

		public void Handle(JObject message)
		{
			string sessionId = (string)message["session_id"];
			if (!string.IsNullOrEmpty(sessionId))
			{
				SessionId = sessionId;
			}

			switch ((string)message["type"])
			{
				case "system":
					HandleSystem(message);
					break;
				case "stream_event":
					HandleStreamEvent(message);
					break;
				case "assistant":
					HandleAssistant(message);
					break;
				case "user":
					HandleUser(message);
					break;
				case "result":
					HandleResult(message);
					break;
				case "rate_limit_event":
					HandleRateLimit(message);
					break;
				case "auth_status":
					HandleAuthStatus(message);
					break;
			}
		}

		private static void AddStrings(JToken token, List<string> target)
		{
			if (!(token is JArray array))
			{
				return;
			}

			foreach (JToken item in array)
			{
				string value = item.Type == JTokenType.String ? (string)item : (string)item["name"] ?? item.ToString();
				if (!string.IsNullOrEmpty(value))
				{
					target.Add(value);
				}
			}
		}

		private static string NullIfEmpty(string value)
		{
			return string.IsNullOrEmpty(value) ? null : value;
		}

		private void HandleSystem(JObject message)
		{
			string subtype = (string)message["subtype"];
			switch (subtype)
			{
				case "init":
					SessionInfo info = new SessionInfo
					{
						SessionId = SessionId,
						Model = (string)message["model"],
						Mode = PermissionModes.FromWire((string)message["permissionMode"]),
						Cwd = (string)message["cwd"],
						ApiKeySource = (string)message["apiKeySource"],
					};

					AddStrings(message["tools"], info.Tools);
					AddStrings(message["slash_commands"], info.SlashCommands);
					if (message["mcp_servers"] is JArray servers)
					{
						foreach (JToken server in servers)
						{
							info.McpServers.Add((string)server["name"] + " (" + (string)server["status"] + ")");
						}
					}

					_sink.SessionStarted(info);
					break;
				case "task_started":
				case "task_progress":
				case "task_notification":
				case "task_updated":
					_sink.BackgroundTaskChanged(ParseTask(subtype, message));
					break;
				case "compact_boundary":
					_sink.Notice(NoticeLevel.Info, "Conversation compacted.");
					break;
				case "api_retry":
					_sink.Notice(NoticeLevel.Warning, "API retry " + (string)message["attempt"] + "/" + (string)message["max_retries"] + ": " + (string)message["error"]);
					break;
			}
		}

		private void HandleStreamEvent(JObject message)
		{
			if (!(message["event"] is JObject streamEvent))
			{
				return;
			}

			string parent = (string)message["parent_tool_use_id"] ?? string.Empty;
			switch ((string)streamEvent["type"])
			{
				case "message_start":
					_currentMessage[parent] = (string)streamEvent["message"]?["id"] ?? string.Empty;
					break;
				case "content_block_start":
				{
					JObject block = streamEvent["content_block"] as JObject;
					string key = Key(parent, CurrentMessage(parent), (int?)streamEvent["index"] ?? 0);
					string type = (string)block?["type"];
					if (type == "text")
					{
						_sink.BlockStarted(new BlockStartRequest(key, BlockKind.Text, NullIfEmpty(parent), null, null));
						string initial = (string)block["text"];
						if (!string.IsNullOrEmpty(initial))
						{
							_sink.TextDelta(key, initial);
						}
					}
					else if (type == "thinking" || type == "redacted_thinking")
					{
						_sink.BlockStarted(new BlockStartRequest(key, BlockKind.Thinking, NullIfEmpty(parent), null, null));
					}
					else if (type == "tool_use" || type == "server_tool_use")
					{
						_sink.BlockStarted(new BlockStartRequest(key, BlockKind.ToolUse, NullIfEmpty(parent), (string)block["id"], (string)block["name"]));
					}

					break;
				}
				case "content_block_delta":
				{
					string key = Key(parent, CurrentMessage(parent), (int?)streamEvent["index"] ?? 0);
					JObject delta = streamEvent["delta"] as JObject;
					switch ((string)delta?["type"])
					{
						case "text_delta":
							_sink.TextDelta(key, (string)delta["text"]);
							break;
						case "thinking_delta":
							_sink.TextDelta(key, (string)delta["thinking"]);
							break;
						case "input_json_delta":
							_sink.ToolInputDelta(key, (string)delta["partial_json"]);
							break;
					}

					break;
				}
			}
		}

		private void HandleAssistant(JObject message)
		{
			JObject body = message["message"] as JObject;
			string parent = (string)message["parent_tool_use_id"] ?? string.Empty;
			string messageId = (string)body?["id"] ?? string.Empty;
			if (body?["content"] is JArray content)
			{
				foreach (JToken token in content)
				{
					if (!(token is JObject block))
					{
						continue;
					}

					_finalBlocks.TryGetValue(messageId, out int index);
					_finalBlocks[messageId] = index + 1;
					string key = Key(parent, messageId, index);
					switch ((string)block["type"])
					{
						case "text":
							_sink.BlockFinalized(new BlockFinalizeRequest(key, BlockKind.Text, NullIfEmpty(parent), (string)block["text"], null, null, null));
							if (parent.Length == 0)
							{
								_assistantTextThisTurn = true;
							}

							break;
						case "thinking":
							_sink.BlockFinalized(new BlockFinalizeRequest(key, BlockKind.Thinking, NullIfEmpty(parent), (string)block["thinking"], null, null, null));
							break;
						case "redacted_thinking":
							_sink.BlockFinalized(new BlockFinalizeRequest(key, BlockKind.Thinking, NullIfEmpty(parent), "[redacted thinking]", null, null, null));
							break;
						case "tool_use":
						case "server_tool_use":
							JObject input = block["input"] as JObject ?? new JObject();
							_sink.BlockFinalized(new BlockFinalizeRequest(key, BlockKind.ToolUse, NullIfEmpty(parent), null, (string)block["id"], (string)block["name"], input));
							break;
					}
				}
			}

			string error = (string)message["error"];
			if (string.IsNullOrEmpty(error))
			{
				return;
			}

			if (error == "authentication_failed" || error == "oauth_org_not_allowed")
			{
				_sink.AuthRequired("Claude Code reports: " + error + ". Sign in again or pick another profile.");
			}
			else
			{
				_sink.Notice(NoticeLevel.Error, "Claude Code reports: " + error);
			}
		}

		private void HandleUser(JObject message)
		{
			if ((bool?)message["isSynthetic"] == true)
			{
				return;
			}

			string parent = (string)message["parent_tool_use_id"];
			JToken content = message["message"]?["content"];
			if (content is JArray blocks)
			{
				int results = 0;
				foreach (JToken block in blocks)
				{
					if ((string)block["type"] == "tool_result")
					{
						results++;
					}
				}

				StringBuilder text = new StringBuilder();
				foreach (JToken block in blocks)
				{
					string type = (string)block["type"];
					if (type == "tool_result")
					{
						string toolUseId = (string)block["tool_use_id"];
						string resultText = ClaudeWire.ToolResultText(block["content"]);
						bool isError = (bool?)block["is_error"] == true;
						JToken structured = results == 1 ? message["tool_use_result"] : null;
						_sink.ToolResult(new ToolResultRequest(toolUseId, resultText, isError, structured));
					}
					else if (type == "text" && results == 0)
					{
						text.Append((string)block["text"]);
					}
				}

				if (text.Length > 0 && string.IsNullOrEmpty(parent))
				{
					EmitUserNotice(text.ToString());
				}
			}
			else if (content != null && content.Type == JTokenType.String && string.IsNullOrEmpty(parent))
			{
				EmitUserNotice((string)content);
			}
		}

		private void HandleResult(JObject message)
		{
			TurnResult result = new TurnResult
			{
				IsError = (bool?)message["is_error"] == true,
				Subtype = (string)message["subtype"],
				ResultText = message["result"]?.Type == JTokenType.String ? (string)message["result"] : null,
				DurationMs = (long?)message["duration_ms"] ?? 0,
				NumTurns = (int?)message["num_turns"] ?? 0,
				CostUsd = (double?)message["total_cost_usd"],
			};

			if (message["usage"] is JObject usage)
			{
				result.InputTokens = (long?)usage["input_tokens"] ?? 0;
				result.OutputTokens = (long?)usage["output_tokens"] ?? 0;
				result.CacheReadTokens = (long?)usage["cache_read_input_tokens"] ?? 0;
				result.CacheCreationTokens = (long?)usage["cache_creation_input_tokens"] ?? 0;
			}

			if (message["permission_denials"] is JArray denials)
			{
				result.PermissionDenials = denials.Count;
			}

			AddStrings(message["errors"], result.Errors);
			if (!string.IsNullOrEmpty(SessionId) && (!result.IsError || result.NumTurns > 0))
			{
				ConfirmedSessionId = SessionId;
			}

			if (!_assistantTextThisTurn && !result.IsError && !string.IsNullOrWhiteSpace(result.ResultText))
			{
				_sink.Notice(NoticeLevel.Info, CleanCommandText(result.ResultText));
			}

			if (result.IsError)
			{
				string detail = result.Errors.Count > 0 ? string.Join("\n", result.Errors) : result.ResultText ?? result.Subtype;
				_sink.Notice(NoticeLevel.Error, "Turn ended with " + result.Subtype + (string.IsNullOrEmpty(detail) ? string.Empty : ": " + detail));
			}

			_assistantTextThisTurn = false;
			_sink.TurnCompleted(result);
		}

		private void HandleRateLimit(JObject message)
		{
			JObject info = message["rate_limit_info"] as JObject;
			string status = (string)info?["status"];
			if (string.IsNullOrEmpty(status) || status == "allowed")
			{
				return;
			}

			string resets = (string)info["resetsAt"];
			_sink.Notice(NoticeLevel.Warning, "Rate limit: " + status + (string.IsNullOrEmpty(resets) ? string.Empty : " (resets at " + resets + ")"));
		}

		private void HandleAuthStatus(JObject message)
		{
			string error = (string)message["error"];
			if (!string.IsNullOrEmpty(error))
			{
				_sink.AuthRequired(error);
				return;
			}

			if (message["output"] is JArray output && output.Count > 0)
			{
				_sink.Notice(NoticeLevel.Info, string.Join("\n", output.ToObject<string[]>()));
			}
		}

		private void EmitUserNotice(string text)
		{
			string cleaned = CleanCommandText(text);
			if (cleaned.Length > 0)
			{
				_sink.Notice(NoticeLevel.Info, cleaned);
			}
		}

		private BackgroundTaskInfo ParseTask(string subtype, JObject message)
		{
			JObject patch = message["patch"] as JObject;
			string status = subtype switch
			{
				"task_notification" => (string)message["status"],
				"task_updated" => (string)patch?["status"],
				_ => "running",
			};

			return new BackgroundTaskInfo
			{
				TaskId = (string)message["task_id"],
				Description = (string)message["description"] ?? (string)patch?["description"],
				TaskType = (string)message["task_type"],
				Status = string.IsNullOrEmpty(status) ? "running" : status,
				LastToolName = (string)message["last_tool_name"],
				Summary = (string)message["summary"],
				ToolUseId = (string)message["tool_use_id"],
			};
		}

		private string CurrentMessage(string parent)
		{
			return _currentMessage.TryGetValue(parent, out string id) ? id : string.Empty;
		}
	}
}