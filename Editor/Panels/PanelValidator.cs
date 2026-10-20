using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Panels
{
	internal static class PanelValidator
	{
		public static PanelSpec Check(JObject json, List<PanelIssue> issues)
		{
			PanelSpec spec = PanelParser.Parse(json, issues);
			if (!PanelIssue.HasErrors(issues))
			{
				PanelTargets.Validate(spec, issues);
			}

			return spec;
		}

		public static string Warnings(List<PanelIssue> issues)
		{
			return issues.Count == 0 ? string.Empty : "\nWarnings:\n" + PanelIssue.Format(issues);
		}
	}
}
