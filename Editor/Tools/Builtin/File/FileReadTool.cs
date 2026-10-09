using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Tools.Builtin
{
    internal sealed class FileReadTool : IParleyTool
    {
        private const int DefaultLimit = 2000;
        private const int MaxLineLength = 2000;
        private const long MaxBytes = 4 * 1024 * 1024;

        public string Name => "Read";

        public string Description =>
            "Read a text file. Returns lines prefixed with line numbers (cat -n format). Use offset/limit for large files. Always read a file before editing it.";

        public JObject InputSchema { get; } = new SchemaBuilder()
            .String("file_path", "Absolute path, or path relative to the project root.", true)
            .Integer("offset", "1-based line to start from.")
            .Integer("limit", "Number of lines to read (default 2000).")
            .Build();

        public ToolKind Kind => ToolKind.ReadOnly;

        public string Summarize(JObject input) => "Read " + ToolInput.String(input, "file_path");

        public string TargetPath(JObject input) => ToolPaths.Resolve(input);

        public Task<ToolResult> ExecuteAsync(JObject input, ToolContext context, CancellationToken cancellationToken)
        {
            string path = ToolPaths.Resolve(input);
            if (path == null)
            {
                return Task.FromResult(ToolResult.Error("file_path is required."));
            }

            if (Directory.Exists(path))
            {
                return Task.FromResult(ToolResult.Error(path + " is a directory. Use Glob to list files."));
            }

            if (!File.Exists(path))
            {
                return Task.FromResult(ToolResult.Error("File does not exist: " + path));
            }

            if (new FileInfo(path).Length > MaxBytes && !input.ContainsKey("offset"))
            {
                return Task.FromResult(ToolResult.Error("File is larger than 4 MB. Read it in parts with offset and limit."));
            }

            string[] lines = File.ReadAllLines(path);
            int offset = Math.Max(1, ToolInput.Int(input, "offset", 1));
            int limit = Math.Max(1, ToolInput.Int(input, "limit", DefaultLimit));
            StringBuilder builder = new StringBuilder();
            int end = Math.Min(lines.Length, offset - 1 + limit);
            for (int i = offset - 1; i < end; i++)
            {
                string line = lines[i];
                if (line.Length > MaxLineLength)
                {
                    line = line.Substring(0, MaxLineLength) + "... [truncated]";
                }

                builder.Append((i + 1).ToString().PadLeft(6)).Append('\t').AppendLine(line);
            }

            if (lines.Length == 0)
            {
                builder.Append("(empty file)");
            }
            else if (end < lines.Length)
            {
                builder.Append("... ").Append(lines.Length - end).Append(" more lines. Use offset ").Append(end + 1).Append(" to continue.");
            }

            context.ReadFiles.Add(path);
            return Task.FromResult(ToolResult.Ok(builder.ToString().TrimEnd()));
        }
    }
}