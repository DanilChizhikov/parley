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
	internal sealed class GrepTool : IParleyTool
	{
		private const long MaxFileBytes = 4 * 1024 * 1024;

		public string Name => "Grep";

		public string Description =>
			"Search file contents with a .NET regular expression. output_mode: files_with_matches (default), content (matching lines with line numbers) or count.";

		public JObject InputSchema { get; } = new SchemaBuilder()
			.String("pattern", "Regular expression.", true)
			.String("path", "File or folder to search (default: project root).")
			.String("glob", "Only files matching this glob, e.g. '*.cs' or 'Assets/**/*.uss'.")
			.Enum(new EnumSchemaRequest("output_mode", "Result format.", new[] { "files_with_matches", "content", "count" }))
			.Boolean("-i", "Case-insensitive.")
			.Integer("-C", "Context lines around each match (content mode).")
			.Integer("head_limit", "Maximum result lines/files (default 100).")
			.Build();

		public ToolKind Kind => ToolKind.ReadOnly;

		public string Summarize(JObject input) => "Grep " + ToolInput.String(input, "pattern");

		public string TargetPath(JObject input) => ToolPaths.Resolve(input, "path");

		public Task<ToolResult> ExecuteAsync(JObject input, ToolContext context, CancellationToken cancellationToken)
		{
			string pattern = ToolInput.String(input, "pattern", string.Empty);
			string root = ToolPaths.Resolve(input, "path") ?? context.ProjectRoot;
			string glob = ToolInput.String(input, "glob");
			string mode = ToolInput.String(input, "output_mode", "files_with_matches");
			int contextLines = Math.Max(0, ToolInput.Int(input, "-C", 0));
			int limit = Math.Max(1, ToolInput.Int(input, "head_limit", 100));
			Regex regex;
			try
			{
				regex = new Regex(pattern, (ToolInput.Bool(input, "-i", false) ? RegexOptions.IgnoreCase : RegexOptions.None) | RegexOptions.CultureInvariant,
					TimeSpan.FromSeconds(2));
			}
			catch (ArgumentException exception)
			{
				return Task.FromResult(ToolResult.Error("Invalid regex: " + exception.Message));
			}

			Regex globRegex = string.IsNullOrEmpty(glob) ? null : GlobMatcher.ToRegex(glob.Contains("/") ? glob : "**/" + glob, CommandLine.IsWindows);
			IEnumerable<string> files = File.Exists(root) ? new[] { root } : GlobMatcher.EnumerateFiles(root, cancellationToken);
			StringBuilder builder = new StringBuilder();
			int emitted = 0;
			foreach (string file in files)
			{
				if (emitted >= limit)
				{
					break;
				}

				if (globRegex != null && !globRegex.IsMatch(GlobMatcher.Relative(root, file)))
				{
					continue;
				}

				string[] lines = ReadText(file);
				if (lines == null)
				{
					continue;
				}

				List<int> hits = new ();
				for (int i = 0; i < lines.Length; i++)
				{
					try
					{
						if (regex.IsMatch(lines[i]))
						{
							hits.Add(i);
						}
					}
					catch (RegexMatchTimeoutException)
					{
						break;
					}
				}

				if (hits.Count == 0)
				{
					continue;
				}

				string displayPath = file.Replace('\\', '/');
				if (mode == "count")
				{
					builder.Append(displayPath).Append(':').Append(hits.Count).AppendLine();
					emitted++;
				}
				else if (mode == "content")
				{
					int lastPrinted = -1;
					foreach (int hit in hits)
					{
						for (int line = Math.Max(lastPrinted + 1, hit - contextLines); line <= Math.Min(lines.Length - 1, hit + contextLines) && emitted < limit; line++)
						{
							builder.Append(displayPath).Append(':').Append(line + 1).Append(line == hit ? ':' : '-').AppendLine(lines[line]);
							lastPrinted = line;
							emitted++;
						}
					}
				}
				else
				{
					builder.AppendLine(displayPath);
					emitted++;
				}
			}

			if (builder.Length == 0)
			{
				return Task.FromResult(ToolResult.Ok("No matches found."));
			}

			if (emitted >= limit)
			{
				builder.Append("... (head_limit reached)");
			}

			return Task.FromResult(ToolResult.Ok(builder.ToString().TrimEnd()));
		}

		private static string[] ReadText(string file)
		{
			try
			{
				FileInfo info = new FileInfo(file);
				if (info.Length > MaxFileBytes || info.Length == 0)
				{
					return null;
				}

				byte[] bytes = File.ReadAllBytes(file);
				int probe = Math.Min(bytes.Length, 8000);
				for (int i = 0; i < probe; i++)
				{
					if (bytes[i] == 0)
					{
						return null;
					}
				}

				return Encoding.UTF8.GetString(bytes).Replace("\r\n", "\n").Split('\n');
			}
			catch (Exception)
			{
				return null;
			}
		}
	}
}