using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DTech.Parley.Editor.Tools;
using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Agents.Claude
{
	internal sealed class SdkMcpBridge
	{
		public const string ServerName = "unity";
		public const string ToolPrefix = "mcp__" + ServerName + "__";

		private const string DefaultProtocolVersion = "2025-06-18";

		private readonly ToolCatalog _catalog;
		private readonly Func<ToolContext> _contextFactory;

		public SdkMcpBridge(ToolCatalog catalog, Func<ToolContext> contextFactory)
		{
			_catalog = catalog;
			_contextFactory = contextFactory;
		}

		public static JObject McpConfig()
		{
			return new JObject
			{
				["mcpServers"] = new JObject
				{
					[ServerName] = new JObject
					{
						["type"] = "sdk",
						["name"] = ServerName,
					},
				},
			};
		}

		public static JObject Error(JToken id, int code, string message)
		{
			return new JObject
			{
				["jsonrpc"] = "2.0",
				["id"] = id?.DeepClone(),
				["error"] = new JObject { ["code"] = code, ["message"] = message },
			};
		}

		public string[] ReadOnlyToolNames()
		{
			List<string> names = new ();
			foreach (IParleyTool tool in _catalog.Tools)
			{
				if (tool.Kind == ToolKind.ReadOnly)
				{
					names.Add(ToolPrefix + tool.Name);
				}
			}

			return names.ToArray();
		}

		public async Task<JObject> HandleAsync(string serverName, JObject message, CancellationToken cancellationToken)
		{
			JToken id = message["id"];
			string method = (string)message["method"];
			if (serverName != ServerName)
			{
				return Error(id, -32601, "Server '" + serverName + "' not found");
			}

			if (id == null || id.Type == JTokenType.Null || string.IsNullOrEmpty(method))
			{
				return null;
			}

			try
			{
				switch (method)
				{
					case "initialize":
						return Result(id, new JObject
						{
							["protocolVersion"] = (string)message["params"]?["protocolVersion"] ?? DefaultProtocolVersion,
							["capabilities"] = new JObject { ["tools"] = new JObject() },
							["serverInfo"] = new JObject { ["name"] = ServerName, ["version"] = "0.1.0" },
						});
					case "ping":
						return Result(id, new JObject());
					case "tools/list":
						return Result(id, new JObject { ["tools"] = ListTools() });
					case "tools/call":
						return Result(id, await CallAsync(message["params"] as JObject, cancellationToken));
					default:
						return Error(id, -32601, "Method not found: " + method);
				}
			}
			catch (Exception exception)
			{
				return Error(id, -32603, exception.Message);
			}
		}

		private static JObject Result(JToken id, JObject result)
		{
			return new JObject
			{
				["jsonrpc"] = "2.0",
				["id"] = id.DeepClone(),
				["result"] = result,
			};
		}

		private static JObject ToolResponse(string text, bool isError)
		{
			return new JObject
			{
				["content"] = new JArray { new JObject { ["type"] = "text", ["text"] = text ?? string.Empty } },
				["isError"] = isError,
			};
		}

		private JArray ListTools()
		{
			JArray tools = new JArray();
			foreach (IParleyTool tool in _catalog.Tools)
			{
				tools.Add(new JObject
				{
					["name"] = tool.Name,
					["description"] = tool.Description,
					["inputSchema"] = tool.InputSchema,
					["annotations"] = new JObject { ["readOnlyHint"] = tool.Kind == ToolKind.ReadOnly },
				});
			}

			return tools;
		}

		private async Task<JObject> CallAsync(JObject parameters, CancellationToken cancellationToken)
		{
			string name = (string)parameters?["name"];
			if (!_catalog.TryGet(name, out IParleyTool tool))
			{
				return ToolResponse("Unknown tool: " + name, true);
			}

			JObject arguments = parameters["arguments"] as JObject ?? new JObject();
			ToolResult result;
			try
			{
				result = await tool.ExecuteAsync(arguments, _contextFactory(), cancellationToken);
			}
			catch (OperationCanceledException)
			{
				result = ToolResult.Error("Cancelled.");
			}
			catch (Exception exception)
			{
				result = ToolResult.Error(tool.Name + " failed: " + exception.Message);
			}

			return ToolResponse(result.Text, result.IsError);
		}
	}
}