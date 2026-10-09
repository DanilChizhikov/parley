using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor
{
    internal sealed class PendingRequest
    {
        public const string AskUserQuestionTool = "AskUserQuestion";
        public const string ExitPlanModeTool = "ExitPlanMode";
        public const string EnterPlanModeTool = "EnterPlanMode";

        public RequestKind Kind => ToolName switch
        {
            AskUserQuestionTool => RequestKind.Question,
            ExitPlanModeTool => RequestKind.PlanApproval,
            EnterPlanModeTool => RequestKind.EnterPlan,
            _ => RequestKind.ToolPermission,
        };

        public string Id { get; set; }
        public string ToolName { get; set; }
        public JObject Input { get; set; } = new ();
        public JArray Suggestions { get; set; }
        public string ToolUseId { get; set; }
        public string AgentId { get; set; }
        public string Title { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string DecisionReason { get; set; }
        public string BlockedPath { get; set; }
    }
}