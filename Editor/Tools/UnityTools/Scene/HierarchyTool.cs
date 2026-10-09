using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DTech.Parley.Editor.Tools.UnityTools
{
    internal sealed class HierarchyTool : IParleyTool
    {
        public string Name => "hierarchy";

        public string Description =>
            "Show the GameObject hierarchy of the open scenes (or the open prefab) with components. Paths look like Scene:Root/Child.";

        public JObject InputSchema { get; } = new SchemaBuilder()
            .String("root", "Optional hierarchy path to start from, e.g. 'Main:Player/Camera'.")
            .Integer("depth", "How many levels to descend (default 4).")
            .Integer("limit", "Maximum number of objects to list (default 400).")
            .Boolean("components", "List component types (default true).")
            .Build();

        public ToolKind Kind => ToolKind.ReadOnly;

        public string Summarize(JObject input) => "Hierarchy " + ToolInput.String(input, "root", string.Empty);

        public string TargetPath(JObject input) => null;

        public Task<ToolResult> ExecuteAsync(JObject input, ToolContext context, CancellationToken cancellationToken)
        {
            int depth = Mathf.Clamp(ToolInput.Int(input, "depth", 4), 0, 32);
            int limit = Mathf.Clamp(ToolInput.Int(input, "limit", 400), 1, 5000);
            bool components = ToolInput.Bool(input, "components", true);
            string root = ToolInput.String(input, "root");
            StringBuilder builder = new StringBuilder();
            int count = 0;
            if (!string.IsNullOrEmpty(root))
            {
                GameObject start = UnityObjectPaths.FindGameObject(root);
                if (start == null)
                {
                    return Task.FromResult(ToolResult.Error("No GameObject at '" + root + "'."));
                }

                Append(builder, start.transform, 0, depth, components, limit, ref count);
            }
            else
            {
                string scene = null;
                foreach (GameObject gameObject in UnityObjectPaths.Roots())
                {
                    if (scene != gameObject.scene.name)
                    {
                        scene = gameObject.scene.name;
                        builder.Append("# Scene ").AppendLine(string.IsNullOrEmpty(scene) ? "(prefab)" : scene);
                    }

                    Append(builder, gameObject.transform, 0, depth, components, limit, ref count);
                }
            }

            if (count >= limit)
            {
                builder.AppendLine("... (limit reached)");
            }

            return Task.FromResult(ToolResult.Ok(builder.Length == 0 ? "No open scene objects." : builder.ToString().TrimEnd()));
        }

        private static void Append(StringBuilder builder, Transform transform, int level, int depth, bool components, int limit, ref int count)
        {
            if (count >= limit)
            {
                return;
            }

            count++;
            builder.Append(' ', level * 2).Append("- ").Append(transform.name);
            if (!transform.gameObject.activeSelf)
            {
                builder.Append(" (inactive)");
            }

            if (components)
            {
                builder.Append(" [");
                Component[] list = transform.GetComponents<Component>();
                for (int i = 0; i < list.Length; i++)
                {
                    builder.Append(i > 0 ? ", " : string.Empty).Append(list[i] == null ? "Missing script" : list[i].GetType().Name);
                }

                builder.Append(']');
            }

            builder.AppendLine();
            if (level >= depth)
            {
                if (transform.childCount > 0)
                {
                    builder.Append(' ', (level + 1) * 2).Append("... ").Append(transform.childCount).AppendLine(" children");
                }

                return;
            }

            foreach (Transform child in transform)
            {
                Append(builder, child, level + 1, depth, components, limit, ref count);
            }
        }
    }
}