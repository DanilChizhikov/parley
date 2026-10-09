using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace DTech.Parley.Editor.Tools.UnityTools
{
    internal sealed class ConsoleRefreshTool : IParleyTool
    {
        private const int StartGraceMs = 2500;
        private const int TimeoutMs = 180000;

        public string Name => "refresh";

        public string Description =>
            "Refresh the AssetDatabase so Unity imports changed files and recompiles scripts, wait for compilation, then report compiler errors. Call after editing C# files.";

        public JObject InputSchema { get; } = new SchemaBuilder().Build();

        public ToolKind Kind => ToolKind.ReadOnly;

        public string Summarize(JObject input) => "Refresh assets and compile";

        public string TargetPath(JObject input) => null;

        public async Task<ToolResult> ExecuteAsync(JObject input, ToolContext context, CancellationToken cancellationToken)
        {
            int finishedBefore = CompileWatcher.FinishedCount;
            AssetDatabase.Refresh();
            int waited = 0;
            bool started = false;
            while (waited < TimeoutMs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (CompileWatcher.IsCompiling)
                {
                    started = true;
                }

                if (CompileWatcher.FinishedCount > finishedBefore && !EditorApplication.isCompiling)
                {
                    break;
                }

                if (!started && waited >= StartGraceMs && !EditorApplication.isUpdating)
                {
                    break;
                }

                await Task.Delay(200, cancellationToken);
                waited += 200;
            }

            StringBuilder builder = new StringBuilder();
            builder.AppendLine(started || CompileWatcher.FinishedCount > finishedBefore ? "Scripts recompiled." : "No script changes to compile.");
            if (ReloadGuard.IsLocked)
            {
                builder.AppendLine("Note: assembly reload is deferred until this turn ends, so new code is not loaded into the editor yet.");
            }

            builder.Append(ConsoleFormat.CompileReport(false));
            return ToolResult.Ok(builder.ToString());
        }
    }
}