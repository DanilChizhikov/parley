using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Tools.Builtin
{
    internal sealed class FileWriteTool : IParleyTool
    {
        public string Name => "Write";

        public string Description =>
            "Create or overwrite a file with the given content. Read an existing file first. Prefer Edit for changes to existing files.";

        public JObject InputSchema { get; } = new SchemaBuilder()
            .String("file_path", "Absolute path, or path relative to the project root.", true)
            .String("content", "Full file content.", true)
            .Build();

        public ToolKind Kind => ToolKind.FileEdit;

        public string Summarize(JObject input) => "Write " + ToolInput.String(input, "file_path");

        public string TargetPath(JObject input) => ToolPaths.Resolve(input);

        public Task<ToolResult> ExecuteAsync(JObject input, ToolContext context, CancellationToken cancellationToken)
        {
            string path = ToolPaths.Resolve(input);
            if (path == null)
            {
                return Task.FromResult(ToolResult.Error("file_path is required."));
            }

            string content = ToolInput.String(input, "content", string.Empty);
            bool exists = File.Exists(path);
            if (exists && !context.ReadFiles.Contains(path))
            {
                return Task.FromResult(ToolResult.Error("File has not been read yet. Read it first before writing to it."));
            }

            string original = exists ? File.ReadAllText(path) : null;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, content, new UTF8Encoding(false));
            context.ReadFiles.Add(path);
            JObject structured = new JObject
            {
                ["type"] = exists ? "update" : "create",
                ["filePath"] = path,
                ["originalFile"] = original,
            };

            return Task.FromResult(ToolResult.Ok((exists ? "Updated " : "Created ") + path, structured));
        }
    }
}