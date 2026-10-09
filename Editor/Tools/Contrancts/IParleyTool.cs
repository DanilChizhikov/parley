using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Tools
{
    internal interface IParleyTool
    {
        string Name { get; }
        string Description { get; }
        JObject InputSchema { get; }
        ToolKind Kind { get; }

        string Summarize(JObject input);
        string TargetPath(JObject input);
        Task<ToolResult> ExecuteAsync(JObject input, ToolContext context, CancellationToken cancellationToken);
    }
}