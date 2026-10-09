using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Tools.UnityTools
{
    internal sealed class CompileStatusTool : IParleyTool
    {
        public string Name => "compile_status";

        public string Description => "Report whether Unity is compiling scripts and list current C# compiler errors and warnings.";

        public JObject InputSchema { get; } = new SchemaBuilder()
            .Boolean("include_warnings", "Include compiler warnings (default false).")
            .Build();

        public ToolKind Kind => ToolKind.ReadOnly;

        public string Summarize(JObject input) => "Compile status";

        public string TargetPath(JObject input) => null;

        public Task<ToolResult> ExecuteAsync(JObject input, ToolContext context, CancellationToken cancellationToken)
        {
            bool warnings = ToolInput.Bool(input, "include_warnings", false);
            return Task.FromResult(ToolResult.Ok(ConsoleFormat.CompileReport(warnings)));
        }
    }
}