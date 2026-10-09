using System.Collections.Generic;
using System.Text;
using UnityEditor;

namespace DTech.Parley.Editor.Tools.UnityTools
{
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