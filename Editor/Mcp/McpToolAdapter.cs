using System.Threading;
using System.Threading.Tasks;
using DTech.Parley.Editor.Tools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Mcp
{
    internal sealed class McpToolAdapter : IParleyTool
    {
        private const int MaxSummaryChars = 120;

        private readonly McpClient _client;
        private readonly string _toolName;

        public string Name { get; }

        public string Description { get; }

        public JObject InputSchema { get; }

        public ToolKind Kind { get; }

        public McpToolAdapter(
            McpClient client,
            string toolName,
            string description,
            JObject inputSchema)
        {
            _client = client;
            _toolName = toolName;
            Name = McpServerResolver.ToolName(client.Name, toolName);
            Description = string.IsNullOrWhiteSpace(description) ? "Tool '" + toolName + "' from MCP server '" + client.Name + "'." : description;
            InputSchema = inputSchema ?? new JObject { ["type"] = "object", ["properties"] = new JObject() };
            Kind = ToolKind.External;
        }

        public string Summarize(JObject input)
        {
            string arguments = input == null || !input.HasValues ? string.Empty : input.ToString(Formatting.None);
            string summary = arguments.Length == 0 ? _toolName : _toolName + " " + arguments;
            return summary.Length > MaxSummaryChars ? summary.Substring(0, MaxSummaryChars) + "…" : summary;
        }

        public string TargetPath(JObject input)
        {
            return null;
        }

        public Task<ToolResult> ExecuteAsync(JObject input, ToolContext context, CancellationToken cancellationToken)
        {
            return _client.CallToolAsync(_toolName, input, cancellationToken);
        }
    }
}
