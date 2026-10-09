using System.Threading;
using System.Threading.Tasks;
using DTech.Parley.Editor.Tools;
using Newtonsoft.Json.Linq;

namespace DTech.Parley.Tests.EditorMode
{
    internal sealed class EchoTool : IParleyTool
    {
        public string Name => "echo";

        public string Description => "Echo";

        public JObject InputSchema { get; } = new SchemaBuilder().String("text", "Text", true).Build();

        public ToolKind Kind => ToolKind.ReadOnly;

        public string Summarize(JObject input) => "echo";

        public string TargetPath(JObject input) => null;

        public Task<ToolResult> ExecuteAsync(JObject input, ToolContext context, CancellationToken cancellationToken)
        {
            return Task.FromResult(ToolResult.Ok((string)input["text"]));
        }
    }
}