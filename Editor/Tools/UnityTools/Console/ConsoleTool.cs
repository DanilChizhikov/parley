using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Tools.UnityTools
{
    internal sealed class ConsoleTool : IParleyTool
    {
        public string Name => "console";

        public string Description =>
            "Read the Unity Editor Console: errors, exceptions, warnings and logs, newest last. Use it to check runtime or editor errors.";

        public JObject InputSchema { get; } = new SchemaBuilder()
            .Enum("severity", "Which entries to return. Default: errors_and_warnings.", new[] { "errors", "errors_and_warnings", "all" })
            .Integer("limit", "Maximum number of entries (default 50, max 300).")
            .String("filter", "Only entries whose message contains this text (case-insensitive).")
            .Boolean("include_stack", "Include stack traces (default false).")
            .Build();

        public ToolKind Kind => ToolKind.ReadOnly;

        public string Summarize(JObject input) => "Console " + ToolInput.String(input, "severity", "errors_and_warnings");

        public string TargetPath(JObject input) => null;

        public Task<ToolResult> ExecuteAsync(JObject input, ToolContext context, CancellationToken cancellationToken)
        {
            string severity = ToolInput.String(input, "severity", "errors_and_warnings");
            int limit = UnityEngine.Mathf.Clamp(ToolInput.Int(input, "limit", 50), 1, 300);
            string filter = ToolInput.String(input, "filter");
            bool stack = ToolInput.Bool(input, "include_stack", false);
            List<ConsoleEntry> entries = ConsoleReader.Read(limit * 4, severity == "all", severity != "errors", true);
            return Task.FromResult(ToolResult.Ok(ConsoleFormat.Format(entries, filter, stack, limit)));
        }
    }
}