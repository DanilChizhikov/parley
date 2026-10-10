using System.Collections.Generic;
using System.Text;
using DTech.Parley.Editor.Mcp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Agents.Claude
{
	internal static class ClaudeWire
	{
		public const string MissingConversation = "No conversation found with session ID";

		public static bool IsMissingConversation(JObject result)
		{
			if ((bool?)result?["is_error"] != true)
			{
				return false;
			}

			if (result["errors"] is JArray errors)
			{
				foreach (JToken error in errors)
				{
					if (error.ToString().Contains(MissingConversation))
					{
						return true;
					}
				}
			}

			JToken text = result["result"];
			return text != null && text.Type == JTokenType.String && ((string)text).Contains(MissingConversation);
		}

		public static string Serialize(JObject message)
		{
			return message.ToString(Formatting.None);
		}

		public static JObject UserMessage(UserTurn turn, string sessionId)
		{
			return new JObject
			{
				["type"] = "user",
				["message"] = new JObject
				{
					["role"] = "user",
					["content"] = BuildContent(turn),
				},
				["parent_tool_use_id"] = null,
				["session_id"] = sessionId ?? string.Empty,
			};
		}

		public static JArray BuildContent(UserTurn turn)
		{
			JArray content = new JArray();
			StringBuilder text = new StringBuilder(turn.Text ?? string.Empty);
			foreach (ChatAttachment attachment in turn.Attachments)
			{
				if (attachment.Kind == AttachmentKind.Text)
				{
					text.Append("\n\n<attachment name=\"").Append(attachment.Label).Append("\">\n").Append(attachment.Text).Append("\n</attachment>");
				}
			}

			if (text.Length > 0)
			{
				content.Add(new JObject { ["type"] = "text", ["text"] = text.ToString() });
			}

			foreach (ChatAttachment attachment in turn.Attachments)
			{
				if (attachment.Kind == AttachmentKind.Image)
				{
					content.Add(new JObject
					{
						["type"] = "image",
						["source"] = new JObject
						{
							["type"] = "base64",
							["media_type"] = attachment.MediaType ?? "image/png",
							["data"] = attachment.Base64,
						},
					});
				}
			}

			return content;
		}

		public static JObject ControlRequest(string requestId, JObject request)
		{
			return new JObject
			{
				["type"] = "control_request",
				["request_id"] = requestId,
				["request"] = request,
			};
		}

		public static JObject ControlSuccess(string requestId, JToken response)
		{
			return new JObject
			{
				["type"] = "control_response",
				["response"] = new JObject
				{
					["subtype"] = "success",
					["request_id"] = requestId,
					["response"] = response ?? new JObject(),
				},
			};
		}

		public static JObject ControlError(string requestId, string error)
		{
			return new JObject
			{
				["type"] = "control_response",
				["response"] = new JObject
				{
					["subtype"] = "error",
					["request_id"] = requestId,
					["error"] = error,
				},
			};
		}

		public static JObject PermissionResult(Decision decision, JObject originalInput)
		{
			if (decision.Allow)
			{
				JObject allow = new JObject
				{
					["behavior"] = "allow",
					["updatedInput"] = decision.UpdatedInput ?? originalInput ?? new JObject(),
				};

				if (decision.UpdatedPermissions != null && decision.UpdatedPermissions.Count > 0)
				{
					allow["updatedPermissions"] = decision.UpdatedPermissions;
				}

				return allow;
			}

			JObject deny = new JObject
			{
				["behavior"] = "deny",
				["message"] = string.IsNullOrEmpty(decision.Message) ? "The user denied this action." : decision.Message,
			};

			if (decision.Interrupt)
			{
				deny["interrupt"] = true;
			}

			return deny;
		}

		public static PendingRequest ParsePermissionRequest(string requestId, JObject request)
		{
			return new PendingRequest
			{
				Id = requestId,
				ToolName = (string)request["tool_name"] ?? "Unknown",
				Input = request["input"] as JObject ?? new JObject(),
				Suggestions = request["permission_suggestions"] as JArray,
				ToolUseId = (string)request["tool_use_id"],
				AgentId = (string)request["agent_id"],
				Title = (string)request["title"],
				DisplayName = (string)request["display_name"],
				Description = (string)request["description"],
				DecisionReason = request["decision_reason"]?.Type == JTokenType.String ? (string)request["decision_reason"] : request["decision_reason"]?.ToString(),
				BlockedPath = (string)request["blocked_path"],
			};
		}

		public static JObject McpServers(McpConfiguration configuration, JObject passthrough, bool includeUnity)
		{
			JObject servers = passthrough == null ? new JObject() : (JObject)passthrough.DeepClone();
			if (includeUnity)
			{
				servers[SdkMcpBridge.ServerName] = new JObject { ["type"] = "sdk", ["name"] = SdkMcpBridge.ServerName };
			}

			foreach (McpServerLaunch server in configuration.Servers)
			{
				servers[server.Name] = server.Transport == McpTransport.Stdio
					? new JObject
					{
						["type"] = "stdio",
						["command"] = server.Command,
						["args"] = new JArray(server.Arguments.ToArray()),
						["env"] = JObject.FromObject(server.Environment),
					}
					: new JObject
					{
						["type"] = "http",
						["url"] = server.Url,
						["headers"] = JObject.FromObject(server.Headers),
					};
			}

			return servers;
		}

		public static string McpServersFingerprint(McpConfiguration configuration)
		{
			return McpServers(configuration, null, false).ToString(Formatting.None);
		}

		public static JObject PassthroughMcpServers(JObject status)
		{
			JObject servers = new JObject();
			if (!(status?["mcpServers"] is JArray entries))
			{
				return servers;
			}

			foreach (JToken entry in entries)
			{
				string name = (string)entry["name"];
				if (string.IsNullOrEmpty(name) || (string)entry["scope"] != "dynamic" || (string)entry["source"] == "sdk" || !(entry["config"] is JObject config))
				{
					continue;
				}

				JObject copy = (JObject)config.DeepClone();
				copy.Remove("scope");
				servers[name] = copy;
			}

			return servers;
		}

		public static string McpDenyRules(IEnumerable<string> serverNames)
		{
			List<string> rules = new ();
			foreach (string name in serverNames)
			{
				rules.Add(McpServerResolver.ToolNamePrefix + McpServerResolver.Normalize(name));
			}

			return string.Join(",", rules);
		}

		public static List<McpServerStatus> ParseMcpStatus(JObject response, McpConfiguration configuration)
		{
			List<McpServerStatus> statuses = new ();
			if (!(response?["mcpServers"] is JArray servers))
			{
				return statuses;
			}

			foreach (JToken server in servers)
			{
				string name = (string)server["name"];
				if (string.IsNullOrEmpty(name) || (string)server["source"] == "sdk")
				{
					continue;
				}

				bool external = (string)server["scope"] != "dynamic" || !configuration.HasServer(name);
				McpServerStatus status = new McpServerStatus
				{
					Name = name,
					IsExternal = external,
					State = McpState((string)server["status"]),
					Error = (string)server["error"],
					ToolCount = server["tools"] is JArray tools ? tools.Count : -1,
				};

				if (external && configuration.DisabledExternal.Contains(name))
				{
					status.State = McpConnectionState.Disabled;
				}

				statuses.Add(status);
			}

			return statuses;
		}

		public static string ToolResultText(JToken content)
		{
			if (content == null || content.Type == JTokenType.Null)
			{
				return string.Empty;
			}

			if (content.Type == JTokenType.String)
			{
				return (string)content;
			}

			if (content is JArray blocks)
			{
				StringBuilder builder = new StringBuilder();
				foreach (JToken block in blocks)
				{
					string type = (string)block["type"];
					if (type == "text")
					{
						if (builder.Length > 0)
						{
							builder.Append('\n');
						}

						builder.Append((string)block["text"]);
					}
					else if (type == "image")
					{
						builder.Append(builder.Length > 0 ? "\n" : string.Empty).Append("[image]");
					}
				}

				return builder.ToString();
			}

			return content.ToString(Formatting.Indented);
		}

		private static McpConnectionState McpState(string status)
		{
			return status switch
			{
				"connected" => McpConnectionState.Connected,
				"failed" => McpConnectionState.Failed,
				"needs-auth" => McpConnectionState.NeedsAuth,
				"disabled" => McpConnectionState.Disabled,
				_ => McpConnectionState.Pending,
			};
		}
	}
}
