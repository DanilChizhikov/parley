using System;
using System.Collections.Generic;
using System.Text;
using DTech.Parley.Editor.Agents.Claude;
using DTech.Parley.Editor.Secrets;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Mcp
{
    internal static class McpServerResolver
    {
        public const string ToolNamePrefix = "mcp__";

        private const string ToolNameSeparator = "__";
        private const int MaxNameLength = 48;
        private const int MaxToolNameLength = 64;
        private const int ToolNameHashLength = 8;
        private const uint FnvOffset = 2166136261;
        private const uint FnvPrime = 16777619;

        public static bool IsValidName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length > MaxNameLength || name == SdkMcpBridge.ServerName)
            {
                return false;
            }

            foreach (char character in name)
            {
                if (!IsNameCharacter(character))
                {
                    return false;
                }
            }

            return true;
        }

        public static string Normalize(string name)
        {
            StringBuilder builder = new StringBuilder(name?.Length ?? 0);
            foreach (char character in name ?? string.Empty)
            {
                builder.Append(IsNameCharacter(character) ? character : '_');
            }

            return builder.ToString();
        }

        public static string ToolPrefix(string serverName)
        {
            return ToolNamePrefix + Normalize(serverName) + ToolNameSeparator;
        }

        public static string ToolName(string serverName, string toolName)
        {
            string name = ToolPrefix(serverName) + Normalize(toolName);
            if (name.Length <= MaxToolNameLength)
            {
                return name;
            }

            uint hash = FnvOffset;
            foreach (char character in serverName + "/" + toolName)
            {
                hash = (hash ^ character) * FnvPrime;
            }

            return name.Substring(0, MaxToolNameLength - ToolNameHashLength - 1) + "_" + hash.ToString("x8");
        }

        public static McpServerLaunch Resolve(McpServerDefinition definition, ISecretStore store)
        {
            McpServerLaunch launch = new McpServerLaunch
            {
                Name = definition.Name,
                Transport = definition.Transport,
                Command = (definition.Command ?? string.Empty).Trim(),
                Url = (definition.Url ?? string.Empty).Trim(),
            };

            if (definition.Transport == McpTransport.Stdio)
            {
                launch.Arguments.AddRange(CommandLine.Split(definition.Arguments));
            }

            Dictionary<string, string> target = definition.Transport == McpTransport.Stdio ? launch.Environment : launch.Headers;
            foreach (McpVariable variable in definition.ActiveVariables())
            {
                string key = (variable.Key ?? string.Empty).Trim();
                if (key.Length == 0)
                {
                    continue;
                }

                string value = variable.Value ?? string.Empty;
                if (variable.IsSecret)
                {
                    value = store.TryGet(SecretStores.Key(definition.SecretOwner, variable.SecretField), out string secret) ? secret : string.Empty;
                }

                target[key] = value;
            }

            return launch;
        }

        public static string Fingerprint(McpConfiguration configuration)
        {
            JArray servers = new JArray();
            foreach (McpServerLaunch server in configuration.Servers)
            {
                servers.Add(Fingerprint(server));
            }

            List<string> disabled = new (configuration.DisabledExternal);
            disabled.Sort(StringComparer.Ordinal);
            return new JObject { ["servers"] = servers, ["disabled"] = new JArray(disabled.ToArray()) }.ToString(Formatting.None);
        }

        public static string Fingerprint(McpServerLaunch server)
        {
            return new JObject
            {
                ["name"] = server.Name,
                ["transport"] = (int)server.Transport,
                ["command"] = server.Command,
                ["args"] = new JArray(server.Arguments.ToArray()),
                ["env"] = JObject.FromObject(server.Environment),
                ["url"] = server.Url,
                ["headers"] = JObject.FromObject(server.Headers),
            }.ToString(Formatting.None);
        }

        private static bool IsNameCharacter(char character)
        {
            return character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_' or '-';
        }
    }
}
