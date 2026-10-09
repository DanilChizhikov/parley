using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace DTech.Parley.Editor.Tools.UnityTools
{
    internal sealed class SelectionTool : IParleyTool
    {
        public string Name => "selection";

        public string Description => "List the objects currently selected in the Unity Editor (assets with paths, scene objects with hierarchy paths).";

        public JObject InputSchema { get; } = new SchemaBuilder().Build();

        public ToolKind Kind => ToolKind.ReadOnly;

        public string Summarize(JObject input) => "Editor selection";

        public string TargetPath(JObject input) => null;

        public Task<ToolResult> ExecuteAsync(JObject input, ToolContext context, CancellationToken cancellationToken)
        {
            Object[] selected = Selection.objects;
            if (selected.Length == 0)
            {
                return Task.FromResult(ToolResult.Ok("Nothing is selected."));
            }

            StringBuilder builder = new StringBuilder();
            foreach (Object target in selected)
            {
                builder.Append("- ").Append(UnityObjectPaths.Describe(target)).Append(" [instanceId ").Append(target.GetInstanceID()).AppendLine("]");
            }

            return Task.FromResult(ToolResult.Ok(builder.ToString().TrimEnd()));
        }
    }
}