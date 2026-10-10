using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Agents.Codex
{
    internal static class CodexMcp
    {
        public const string StatusListMethod = "mcpServerStatus/list";
        public const string StartupStatusNotification = "mcpServer/startupStatus/updated";

        private const string ServersKey = "mcp_servers.";
        private const string EnabledSuffix = ".enabled";

        public static JObject Config(McpConfiguration configuration)
        {
            if (configuration.Servers.Count == 0 && configuration.DisabledExternal.Count == 0)
            {
                return null;
            }

            JObject config = new JObject();
            foreach (McpServerLaunch server in configuration.Servers)
            {
                config[ServersKey + server.Name] = server.Transport == McpTransport.Stdio
                    ? new JObject
                    {
                        ["command"] = server.Command,
                        ["args"] = new JArray(server.Arguments.ToArray()),
                        ["env"] = JObject.FromObject(server.Environment),
                        ["cwd"] = ProjectPaths.Root,
                    }
                    : new JObject
                    {
                        ["url"] = server.Url,
                        ["http_headers"] = JObject.FromObject(server.Headers),
                    };
            }

            foreach (string name in configuration.DisabledExternal)
            {
                config[ServersKey + name + EnabledSuffix] = false;
            }

            return config;
        }

        public static JObject StatusParameters(string threadId, string cursor)
        {
            JObject parameters = new JObject { ["threadId"] = threadId, ["detail"] = "toolsAndAuthOnly" };
            if (cursor != null)
            {
                parameters["cursor"] = cursor;
            }

            return parameters;
        }

        public static void ParseStatus(JToken result, McpConfiguration configuration, List<McpServerStatus> statuses)
        {
            if (!(result?["data"] is JArray servers))
            {
                return;
            }

            foreach (JToken server in servers)
            {
                string name = (string)server["name"];
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                statuses.Add(new McpServerStatus
                {
                    Name = name,
                    IsExternal = !configuration.HasServer(name),
                    State = State(server["runtimeStatus"]?.Type == JTokenType.String ? (string)server["runtimeStatus"] : null),
                    Error = (string)server["toolsError"],
                    ToolCount = server["tools"] is JObject tools ? tools.Count : -1,
                });
            }
        }

        private static McpConnectionState State(string status)
        {
            return status switch
            {
                "connected" => McpConnectionState.Connected,
                "authenticationRequired" => McpConnectionState.NeedsAuth,
                "failed" => McpConnectionState.Failed,
                "cancelled" => McpConnectionState.Failed,
                "disabled" => McpConnectionState.Disabled,
                _ => McpConnectionState.Pending,
            };
        }
    }
}
