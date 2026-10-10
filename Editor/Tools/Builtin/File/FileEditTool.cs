using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Tools.Builtin
{
	internal sealed class FileEditTool : IParleyTool
	{
		public string Name => "Edit";

		public string Description =>
			"Replace an exact string in a file. old_string must match exactly (including indentation) and be unique unless replace_all is true. Read the file first.";

		public JObject InputSchema { get; } = new SchemaBuilder()
			.String("file_path", "Absolute path, or path relative to the project root.", true)
			.String("old_string", "Exact text to replace.", true)
			.String("new_string", "Replacement text.", true)
			.Boolean("replace_all", "Replace every occurrence (default false).")
			.Build();

		public ToolKind Kind => ToolKind.FileEdit;

		public static int Count(string content, string value)
		{
			int count = 0;
			int index = 0;
			while ((index = content.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
			{
				count++;
				index += value.Length;
			}

			return count;
		}

		public string Summarize(JObject input) => "Edit " + ToolInput.String(input, "file_path");

		public string TargetPath(JObject input) => ToolPaths.Resolve(input);

		public Task<ToolResult> ExecuteAsync(JObject input, ToolContext context, CancellationToken cancellationToken)
		{
			string path = ToolPaths.Resolve(input);
			string oldText = ToolInput.String(input, "old_string", string.Empty);
			string newText = ToolInput.String(input, "new_string", string.Empty);
			bool replaceAll = ToolInput.Bool(input, "replace_all", false);
			if (path == null || !File.Exists(path))
			{
				return Task.FromResult(ToolResult.Error("File does not exist: " + path));
			}

			if (!context.ReadFiles.Contains(path))
			{
				return Task.FromResult(ToolResult.Error("File has not been read yet. Read it first before editing it."));
			}

			if (oldText.Length == 0)
			{
				return Task.FromResult(ToolResult.Error("old_string must not be empty. Use Write to create files."));
			}

			if (oldText == newText)
			{
				return Task.FromResult(ToolResult.Error("old_string and new_string are identical."));
			}

			if (!TextFile.TryRead(path, out TextFileContent file))
			{
				return Task.FromResult(ToolResult.Error(path + " is not valid UTF-8 text. Edit it another way."));
			}

			string content = file.Text;
			int count = Count(content, oldText);
			if (count == 0 && content.Contains("\r\n") && !oldText.Contains("\r\n"))
			{
				oldText = oldText.Replace("\n", "\r\n");
				newText = newText.Replace("\n", "\r\n");
				count = Count(content, oldText);
			}

			if (count == 0)
			{
				return Task.FromResult(ToolResult.Error("old_string was not found in the file."));
			}

			if (count > 1 && !replaceAll)
			{
				return Task.FromResult(ToolResult.Error($"Found {count} matches of old_string. Add more context to make it unique, or set replace_all."));
			}

			string updated = replaceAll ? content.Replace(oldText, newText) : ReplaceFirst(content, oldText, newText);
			TextFile.Write(path, updated, file.HasBom);
			JObject structured = new JObject { ["filePath"] = path, ["replacements"] = replaceAll ? count : 1 };
			return Task.FromResult(ToolResult.Ok($"Edited {path} ({(replaceAll ? count : 1)} replacement(s)).", structured));
		}

		private static string ReplaceFirst(string content, string oldText, string newText)
		{
			int index = content.IndexOf(oldText, StringComparison.Ordinal);
			return content.Substring(0, index) + newText + content.Substring(index + oldText.Length);
		}
	}
}