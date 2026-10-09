using System.Collections.Generic;
using System.Text;
using UnityEditor;

namespace DTech.Parley.Editor.Tools.UnityTools
{
	internal static class ConsoleFormat
	{
		public static List<ConsoleEntry> Filter(List<ConsoleEntry> entries, string filter)
		{
			List<ConsoleEntry> matching = new ();
			foreach (ConsoleEntry entry in entries)
			{
				if (string.IsNullOrEmpty(filter) || entry.Message.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0)
				{
					matching.Add(entry);
				}
			}

			return matching;
		}

		public static string Format(List<ConsoleEntry> entries, bool includeStack, int limit)
		{
			if (entries.Count == 0)
			{
				return "Console has no matching entries.";
			}

			StringBuilder builder = new StringBuilder();
			for (int i = System.Math.Max(0, entries.Count - limit); i < entries.Count; i++)
			{
				ConsoleEntry entry = entries[i];
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
			ConsoleSeverity minimum = includeWarnings ? ConsoleSeverity.Warning : ConsoleSeverity.Error;
			List<ConsoleEntry> entries = ConsoleReader.Read(200, minimum, true);
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