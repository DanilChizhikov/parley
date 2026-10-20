using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DTech.Parley.Editor.Panels;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Tools.UnityTools
{
	internal sealed class PanelSaveTool : IParleyTool
	{
		public string Name => "panel_save";

		public string Description =>
			"Save a panel spec (same format as panel_open) under a name in UserSettings/Parley/Panels/<name>.json, outside Assets and version control. " +
			"The user reopens it from Window > DTech > Parley Panels or you with panel_open {name}. Overwrites a panel with the same name and refreshes its open window.";

		public JObject InputSchema { get; } = new SchemaBuilder()
			.String("name", "Panel name (letters, digits, '-', '_').", true)
			.Add("spec", new JObject { ["type"] = "object", ["description"] = "Panel spec: { title, targets, items }." }, true)
			.Boolean("open", "Open the saved panel (default true).")
			.Build();

		public ToolKind Kind => ToolKind.FileEdit;

		public string Summarize(JObject input) => "Save panel " + ToolInput.String(input, "name", string.Empty);

		public string TargetPath(JObject input) => PanelStore.Default.PathFor(ToolInput.String(input, "name"));

		public Task<ToolResult> ExecuteAsync(JObject input, ToolContext context, CancellationToken cancellationToken)
		{
			return Task.FromResult(Execute(input));
		}

		private static ToolResult Execute(JObject input)
		{
			string name = PanelStore.Slug(ToolInput.String(input, "name"));
			if (name == null)
			{
				return ToolResult.Error("'name' must contain letters or digits.");
			}

			JObject json = PanelParser.ReadSpec(input?["spec"], out string specError);
			if (json == null)
			{
				return ToolResult.Error(specError ?? "'spec' is required.");
			}

			List<PanelIssue> issues = new ();
			PanelSpec spec = PanelValidator.Check(json, issues);
			if (PanelIssue.HasErrors(issues))
			{
				return ToolResult.Error("Panel spec has errors, nothing saved. Fix and call again:\n" + PanelIssue.Format(issues));
			}

			string text = json.ToString(Formatting.Indented);
			try
			{
				PanelStore.Default.Save(name, text);
			}
			catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
			{
				return ToolResult.Error("Could not save panel: " + exception.Message);
			}

			if (ToolInput.Bool(input, "open", true))
			{
				PanelWindow.Open(name, text, spec.Title);
			}

			string path = ProjectPaths.ToProjectRelative(PanelStore.Default.PathFor(name));
			return ToolResult.Ok("Saved panel '" + name + "' to " + path + "." + PanelValidator.Warnings(issues));
		}
	}
}
