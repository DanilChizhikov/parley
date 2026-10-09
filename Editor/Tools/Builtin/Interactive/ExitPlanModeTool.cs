using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Tools.Builtin
{
	internal sealed class ExitPlanModeTool : IParleyTool
	{
		public string Name => PendingRequest.ExitPlanModeTool;

		public string Description =>
			"Present your implementation plan (markdown) for user approval when you are in plan mode and ready to code.";

		public JObject InputSchema { get; } = new SchemaBuilder()
			.String("plan", "The plan in markdown: context, steps, files to change, verification.", true)
			.Build();

		public ToolKind Kind => ToolKind.Interactive;

		public string Summarize(JObject input) => "Plan ready for review";

		public string TargetPath(JObject input) => null;

		public async Task<ToolResult> ExecuteAsync(JObject input, ToolContext context, CancellationToken cancellationToken)
		{
			if (string.IsNullOrWhiteSpace(ToolInput.String(input, "plan")))
			{
				return ToolResult.Error("plan is required.");
			}

			PendingRequest request = new PendingRequest
			{
				Id = Guid.NewGuid().ToString("N"),
				ToolName = PendingRequest.ExitPlanModeTool,
				Input = input,
				ToolUseId = context.ToolUseId,
			};

			Decision decision = await context.AskUser(request, cancellationToken);
			if (!decision.Allow)
			{
				string feedback = string.IsNullOrWhiteSpace(decision.Message) ? "No feedback given." : decision.Message;
				return ToolResult.Error("The user wants to keep planning. Feedback: " + feedback + " Revise the plan and call ExitPlanMode again.");
			}

			context.SetMode(decision.NextMode ?? PermissionMode.Default);
			return ToolResult.Ok("User has approved your plan. You can now start coding. Start with updating your todo list if applicable.");
		}
	}
}