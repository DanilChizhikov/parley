using System;
using System.Threading;
using System.Threading.Tasks;
using DTech.Parley.Editor.Tools;
using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Orchestration
{
    internal sealed class DelegateToAgentTool : IParleyTool
    {
        private readonly Func<DelegationRequest, string, string> _submit;
        public string Name => "delegate_to_agent";
        public string Description => "Delegate a subtask to an allowed skill in a child chat. Returns a receipt; " +
            "finish this turn and wait for the child result, which will arrive in a new turn. " +
            "Use request_id to retry the same request without spawning another child.";
        public ToolKind Kind => ToolKind.ReadOnly;
        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["target_skill"] = new JObject { ["type"] = "string" },
                ["task_summary"] = new JObject { ["type"] = "string" },
                ["context_mode"] = new JObject
                {
                    ["type"] = "string", ["enum"] = new JArray("summary", "last_messages"),
                },
                ["request_id"] = new JObject { ["type"] = "string" },
            },
            ["required"] = new JArray("target_skill", "task_summary", "context_mode"),
            ["additionalProperties"] = false,
        };

        public DelegateToAgentTool(Func<DelegationRequest, string> submit)
            : this((request, _) => submit(request)) { }
        public DelegateToAgentTool(Func<DelegationRequest, string, string> submit) { _submit = submit; }
        public string Summarize(JObject input) => "Delegate to " + (string)input?["target_skill"];
        public string TargetPath(JObject input) => null;

        public Task<ToolResult> ExecuteAsync(JObject input, ToolContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return Task.FromResult(ToolResult.Ok(_submit(DelegationRequest.Parse(input, context.ToolUseId),
                    context.TurnCorrelationId)));
            }
            catch (ArgumentException exception)
            {
                return Task.FromResult(ToolResult.Error(exception.Message));
            }
            catch (InvalidOperationException exception)
            {
                return Task.FromResult(ToolResult.Error(exception.Message));
            }
        }
    }
}
