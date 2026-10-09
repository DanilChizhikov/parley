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
            HierarchyWriter writer = new HierarchyWriter(depth, components, limit);
            if (!string.IsNullOrEmpty(root))
            {
                GameObject start = UnityObjectPaths.FindGameObject(root);
                if (start == null)
                {
                    return Task.FromResult(ToolResult.Error("No GameObject at '" + root + "'."));
                }

                writer.Append(start.transform, 0);
            }
            else
            {
                string scene = null;
                foreach (GameObject gameObject in UnityObjectPaths.Roots())
                {
                    if (scene != gameObject.scene.name)
                    {
                        scene = gameObject.scene.name;
                        writer.Builder.Append("# Scene ").AppendLine(string.IsNullOrEmpty(scene) ? "(prefab)" : scene);
                    }

                    writer.Append(gameObject.transform, 0);
                }
            }

            if (writer.LimitReached)
            {
                writer.Builder.AppendLine("... (limit reached)");
            }

            return Task.FromResult(ToolResult.Ok(writer.Builder.Length == 0 ? "No open scene objects." : writer.Builder.ToString().TrimEnd()));
        }

        private sealed class HierarchyWriter
        {
            private readonly int _depth;
            private readonly bool _components;
            private readonly int _limit;

            public StringBuilder Builder { get; } = new ();
            public bool LimitReached => _count >= _limit;

            private int _count;

            public HierarchyWriter(int depth, bool components, int limit)
            {
                _depth = depth;
                _components = components;
                _limit = limit;
            }

            public void Append(Transform transform, int level)
            {
                if (LimitReached)
                {
                    return;
                }

                _count++;
                Builder.Append(' ', level * 2).Append("- ").Append(transform.name);
                if (!transform.gameObject.activeSelf)
                {
                    Builder.Append(" (inactive)");
                }

                if (_components)
                {
                    Builder.Append(" [");
                    Component[] list = transform.GetComponents<Component>();
                    for (int i = 0; i < list.Length; i++)
                    {
                        Builder.Append(i > 0 ? ", " : string.Empty).Append(list[i] == null ? "Missing script" : list[i].GetType().Name);
                    }

                    Builder.Append(']');
                }

                Builder.AppendLine();
                if (level >= _depth)
                {
                    if (transform.childCount > 0)
                    {
                        Builder.Append(' ', (level + 1) * 2).Append("... ").Append(transform.childCount).AppendLine(" children");
                    }

                    return;
                }

                foreach (Transform child in transform)
                {
                    Append(child, level + 1);
                }
            }
        }
    }
}