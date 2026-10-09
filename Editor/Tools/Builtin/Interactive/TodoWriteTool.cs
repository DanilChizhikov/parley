using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Tools.Builtin
{
    internal sealed class TodoWriteTool : IParleyTool
    {
        public string Name => "TodoWrite";

        public string Description =>
            "Create or update the task list for multi-step work. Send the full list every time; keep exactly one item in_progress while working.";

        public JObject InputSchema { get; } = new SchemaBuilder()
            .Add("todos", new JObject
            {
                ["type"] = "array",
                ["description"] = "The full, updated todo list.",
                ["items"] = new SchemaBuilder()
                    .String("content", "Imperative description, e.g. 'Fix the login bug'.", true)
                    .Enum("status", "Item status.", new[] { "pending", "in_progress", "completed" }, true)
                    .String("activeForm", "Present continuous form shown while in progress, e.g. 'Fixing the login bug'.", true)
                    .Build(),
            }, true)
            .Build();

        public ToolKind Kind => ToolKind.Interactive;

        public string Summarize(JObject input) => "Update todos";

        public string TargetPath(JObject input) => null;

        public Task<ToolResult> ExecuteAsync(JObject input, ToolContext context, CancellationToken cancellationToken)
        {
            if (!(input["todos"] is JArray))
            {
                return Task.FromResult(ToolResult.Error("todos must be an array."));
            }

            return Task.FromResult(ToolResult.Ok("Todos have been modified successfully. Keep using the todo list to track your progress."));
        }
    }
}