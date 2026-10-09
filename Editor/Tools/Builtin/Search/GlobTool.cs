using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Tools.Builtin
{
    internal sealed class GlobTool : IParleyTool
    {
        private const int Limit = 250;

        public string Name => "Glob";

        public string Description =>
            "Find files by glob pattern (e.g. '**/*.cs', 'Assets/**/Player*.prefab'). Results are sorted by modification time, newest first.";

        public JObject InputSchema { get; } = new SchemaBuilder()
            .String("pattern", "Glob pattern relative to path.", true)
            .String("path", "Folder to search (default: project root).")
            .Build();

        public ToolKind Kind => ToolKind.ReadOnly;

        public string Summarize(JObject input) => "Glob " + ToolInput.String(input, "pattern");

        public string TargetPath(JObject input) => ToolPaths.Resolve(input, "path");

        public Task<ToolResult> ExecuteAsync(JObject input, ToolContext context, CancellationToken cancellationToken)
        {
            string pattern = ToolInput.String(input, "pattern", string.Empty).Trim();
            string root = ToolPaths.Resolve(input, "path") ?? context.ProjectRoot;
            if (pattern.Length == 0)
            {
                return Task.FromResult(ToolResult.Error("pattern is required."));
            }

            if (!Directory.Exists(root))
            {
                return Task.FromResult(ToolResult.Error("Folder does not exist: " + root));
            }

            Regex regex = GlobMatcher.ToRegex(pattern, CommandLine.IsWindows);
            List<(string path, DateTime time)> matches = new ();
            foreach (string file in GlobMatcher.EnumerateFiles(root, cancellationToken))
            {
                if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) && !pattern.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (regex.IsMatch(GlobMatcher.Relative(root, file)))
                {
                    matches.Add((file, File.GetLastWriteTimeUtc(file)));
                }
            }

            matches.Sort((left, right) => right.time.CompareTo(left.time));
            if (matches.Count == 0)
            {
                return Task.FromResult(ToolResult.Ok("No files found."));
            }

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < matches.Count && i < Limit; i++)
            {
                builder.AppendLine(matches[i].path.Replace('\\', '/'));
            }

            if (matches.Count > Limit)
            {
                builder.Append("... ").Append(matches.Count - Limit).Append(" more. Narrow the pattern.");
            }

            return Task.FromResult(ToolResult.Ok(builder.ToString().TrimEnd()));
        }
    }
}