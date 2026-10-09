using System;
using System.Threading;
using System.Threading.Tasks;
using DTech.Parley.Editor.Tools;
using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Agents.Codex
{
    internal sealed class CodexToolBridge
    {
        public const string Namespace = "unity";

        private readonly ToolCatalog _catalog;
        private readonly Func<ToolContext> _contextFactory;

        public bool HasTools => _catalog.Tools.Count > 0;

        public CodexToolBridge(ToolCatalog catalog, Func<ToolContext> contextFactory)
        {
            _catalog = catalog;
            _contextFactory = contextFactory;
        }

        public static string DisplayName(string tool)
        {
            return ToolCatalog.UnityLocalPrefix + tool;
        }

        public JArray Specs()
        {
            JArray tools = new JArray();
            foreach (IParleyTool tool in _catalog.Tools)
            {
                tools.Add(new JObject
                {
                    ["type"] = "function",
                    ["name"] = tool.Name,
                    ["description"] = tool.Description,
                    ["inputSchema"] = tool.InputSchema,
                });
            }

            if (tools.Count == 0)
            {
                return new JArray();
            }

            return new JArray
            {
                new JObject
                {
                    ["type"] = "namespace",
                    ["name"] = Namespace,
                    ["description"] = "Unity Editor tools provided by Parley: console, compilation, scene hierarchy, inspection, selection and project info.",
                    ["tools"] = tools,
                },
            };
        }

        public async Task<JObject> CallAsync(JObject parameters, CancellationToken cancellationToken)
        {
            string name = (string)parameters["tool"];
            string space = (string)parameters["namespace"];
            if ((!string.IsNullOrEmpty(space) && space != Namespace) || !_catalog.TryGet(name, out IParleyTool tool))
            {
                return Response("Unknown tool: " + name, false);
            }

            JObject arguments = parameters["arguments"] as JObject ?? new JObject();
            ToolResult result;
            try
            {
                result = await tool.ExecuteAsync(arguments, _contextFactory(), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                result = ToolResult.Error("Cancelled.");
            }
            catch (Exception exception)
            {
                result = ToolResult.Error(tool.Name + " failed: " + exception.Message);
            }

            return Response(result.Text, !result.IsError);
        }

        private static JObject Response(string text, bool success)
        {
            return new JObject
            {
                ["contentItems"] = new JArray { new JObject { ["type"] = "inputText", ["text"] = text ?? string.Empty } },
                ["success"] = success,
            };
        }
    }
}