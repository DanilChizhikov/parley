using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Tools.Builtin
{
    internal sealed class EnterPlanModeTool : IParleyTool
    {
        public string Name => PendingRequest.EnterPlanModeTool;

        public string Description =>
            "Ask to switch into plan mode before a non-trivial implementation: explore with read-only tools, then present a plan with ExitPlanMode.";

        public JObject InputSchema { get; } = new SchemaBuilder().Build();

        public ToolKind Kind => ToolKind.Interactive;

        public string Summarize(JObject input) => "Enter plan mode";

        public string TargetPath(JObject input) => null;

        public async Task<ToolResult> ExecuteAsync(JObject input, ToolContext context, CancellationToken cancellationToken)
        {
            PendingRequest request = new PendingRequest
            {
                Id = Guid.NewGuid().ToString("N"),
                ToolName = PendingRequest.EnterPlanModeTool,
                Input = input ?? new JObject(),
                ToolUseId = context.ToolUseId,
            };

            Decision decision = await context.AskUser(request, cancellationToken);
            if (!decision.Allow)
            {
                return ToolResult.Ok("The user declined plan mode. Continue with the task directly.");
            }

            context.SetMode(PermissionMode.Plan);
            return ToolResult.Ok("Entered plan mode. Explore with read-only tools only, ask clarifying questions with AskUserQuestion, then call ExitPlanMode with your plan.");
        }
    }
}