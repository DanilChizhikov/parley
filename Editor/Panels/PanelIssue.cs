using System.Collections.Generic;
using System.Text;

namespace DTech.Parley.Editor.Panels
{
	internal sealed class PanelIssue
	{
		public PanelIssueSeverity Severity { get; }
		public string Location { get; }
		public string Message { get; }

		public PanelIssue(PanelIssueSeverity severity, string location, string message)
		{
			Severity = severity;
			Location = location ?? string.Empty;
			Message = message ?? string.Empty;
		}

		public static PanelIssue Error(string location, string message) => new (PanelIssueSeverity.Error, location, message);

		public static PanelIssue Warning(string location, string message) => new (PanelIssueSeverity.Warning, location, message);

		public static bool HasErrors(IEnumerable<PanelIssue> issues)
		{
			foreach (PanelIssue issue in issues)
			{
				if (issue.Severity == PanelIssueSeverity.Error)
				{
					return true;
				}
			}

			return false;
		}

		public static string Format(IEnumerable<PanelIssue> issues)
		{
			StringBuilder builder = new StringBuilder();
			foreach (PanelIssue issue in issues)
			{
				builder.Append("- ").AppendLine(issue.ToString());
			}

			return builder.ToString().TrimEnd();
		}

		public override string ToString()
		{
			string prefix = Severity == PanelIssueSeverity.Error ? "error" : "warning";
			return string.IsNullOrEmpty(Location) ? prefix + ": " + Message : prefix + " at " + Location + ": " + Message;
		}
	}
}
