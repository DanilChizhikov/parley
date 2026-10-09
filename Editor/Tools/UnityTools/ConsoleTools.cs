using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace DTech.Parley.Editor.Tools.UnityTools
{
	internal sealed class ConsoleTool : IParleyTool
	{
		public string Name => "console";

		public string Description =>
			"Read the Unity Editor Console: errors, exceptions, warnings and logs, newest last. Use it to check runtime or editor errors.";

		public JObject InputSchema { get; } = new SchemaBuilder()
			.Enum("severity", "Which entries to return. Default: errors_and_warnings.", new[] { "errors", "errors_and_warnings", "all" })
			.Integer("limit", "Maximum number of entries (default 50, max 300).")
			.String("filter", "Only entries whose message contains this text (case-insensitive).")
			.Boolean("include_stack", "Include stack traces (default false).")
			.Build();

		public ToolKind Kind => ToolKind.ReadOnly;

		public string Summarize(JObject input) => "Console " + ToolInput.String(input, "severity", "errors_and_warnings");

		public string TargetPath(JObject input) => null;

		public Task<ToolResult> ExecuteAsync(JObject input, ToolContext context, CancellationToken cancellationToken)
		{
			string severity = ToolInput.String(input, "severity", "errors_and_warnings");
			int limit = UnityEngine.Mathf.Clamp(ToolInput.Int(input, "limit", 50), 1, 300);
			string filter = ToolInput.String(input, "filter");
			bool stack = ToolInput.Bool(input, "include_stack", false);
			List<ConsoleEntry> entries = ConsoleReader.Read(limit * 4, severity == "all", severity != "errors", true);
			return Task.FromResult(ToolResult.Ok(ConsoleFormat.Format(entries, filter, stack, limit)));
		}
	}

	internal sealed class CompileStatusTool : IParleyTool
	{
		public string Name => "compile_status";

		public string Description => "Report whether Unity is compiling scripts and list current C# compiler errors and warnings.";

		public JObject InputSchema { get; } = new SchemaBuilder()
			.Boolean("include_warnings", "Include compiler warnings (default false).")
			.Build();

		public ToolKind Kind => ToolKind.ReadOnly;

		public string Summarize(JObject input) => "Compile status";

		public string TargetPath(JObject input) => null;

		public Task<ToolResult> ExecuteAsync(JObject input, ToolContext context, CancellationToken cancellationToken)
		{
			bool warnings = ToolInput.Bool(input, "include_warnings", false);
			return Task.FromResult(ToolResult.Ok(ConsoleFormat.CompileReport(warnings)));
		}
	}

	internal sealed class RefreshTool : IParleyTool
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

	internal static class ConsoleFormat
	{
		public static string Format(List<ConsoleEntry> entries, string filter, bool includeStack, int limit)
		{
			List<ConsoleEntry> matching = new ();
			foreach (ConsoleEntry entry in entries)
			{
				if (string.IsNullOrEmpty(filter) || entry.Message.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0)
				{
					matching.Add(entry);
				}
			}

			if (matching.Count == 0)
			{
				return "Console has no matching entries.";
			}

			StringBuilder builder = new StringBuilder();
			for (int i = System.Math.Max(0, matching.Count - limit); i < matching.Count; i++)
			{
				ConsoleEntry entry = matching[i];
				builder.Append('[').Append(entry.Severity).Append("] ").Append(entry.Message);
				if (!string.IsNullOrEmpty(entry.File))
				{
					builder.Append(" (").Append(entry.File).Append(':').Append(entry.Line).Append(')');
				}

				builder.AppendLine();
				if (includeStack && !string.IsNullOrEmpty(entry.StackTrace))
				{
					builder.AppendLine(Indent(entry.StackTrace));
				}
			}

			return builder.ToString().TrimEnd();
		}

		public static string CompileReport(bool includeWarnings)
		{
			List<ConsoleEntry> entries = ConsoleReader.Read(200, false, includeWarnings, true, true);
			StringBuilder builder = new StringBuilder();
			builder.AppendLine(EditorApplication.isCompiling ? "Unity is compiling scripts right now." : "Unity is not compiling.");
			int errors = 0;
			foreach (ConsoleEntry entry in entries)
			{
				if (entry.Severity == ConsoleSeverity.Error)
				{
					errors++;
				}
			}

			builder.AppendLine(errors == 0 ? "No compiler errors." : errors + " compiler error(s):");
			foreach (ConsoleEntry entry in entries)
			{
				builder.Append("- [").Append(entry.Severity).Append("] ").AppendLine(entry.Message);
			}

			return builder.ToString().TrimEnd();
		}

		private static string Indent(string text)
		{
			return "    " + text.Replace("\n", "\n    ");
		}
	}
}
