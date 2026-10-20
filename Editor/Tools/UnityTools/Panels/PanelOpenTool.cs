using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DTech.Parley.Editor.Panels;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Tools.UnityTools
{
	internal sealed class PanelOpenTool : IParleyTool
	{
		public string Name => "panel_open";

		public string Description =>
			"Open an editor window (panel) built from a declarative JSON spec: no C# code, no recompilation, nothing added to the project. " +
			"Use it when the user wants a window to tune configs, ScriptableObjects or scene objects. Look up property paths with 'inspect' first. " +
			"Targets and property paths are validated; on errors nothing opens and the errors are returned so you can fix the spec. " +
			"Calling again with the same title updates the open window. Pass 'name' instead of 'spec' to open a saved panel; use panel_save to keep one.\n" +
			PanelParser.Reference;

		public JObject InputSchema { get; } = new SchemaBuilder()
			.Add("spec", new JObject { ["type"] = "object", ["description"] = "Panel spec: { title, targets, items }." })
			.String("name", "Name of a saved panel to open (instead of spec).")
			.Build();

		public ToolKind Kind => ToolKind.ReadOnly;

		public string Summarize(JObject input)
		{
			string name = ToolInput.String(input, "name");
			return "Open panel " + (name ?? (input?["spec"] as JObject)?["title"]?.ToString() ?? string.Empty);
		}

		public string TargetPath(JObject input) => null;

		public Task<ToolResult> ExecuteAsync(JObject input, ToolContext context, CancellationToken cancellationToken)
		{
			return Task.FromResult(Execute(input));
		}

		private static ToolResult Execute(JObject input)
		{
			JObject json = PanelParser.ReadSpec(input?["spec"], out string specError);
			if (specError != null)
			{
				return ToolResult.Error(specError);
			}

			string name = PanelStore.Slug(ToolInput.String(input, "name"));
			if (json != null && name != null)
			{
				return ToolResult.Error("Pass either 'spec' or 'name', not both. Use panel_save to store a spec under a name.");
			}

			if (json == null)
			{
				if (name == null)
				{
					return ToolResult.Error("Pass 'spec' (new panel) or 'name' (saved panel).");
				}

				if (!PanelStore.Default.TryLoad(name, out string saved))
				{
					IReadOnlyList<string> names = PanelStore.Default.List();
					return ToolResult.Error("No saved panel '" + name + "'. Saved: " + (names.Count == 0 ? "(none)" : string.Join(", ", names)) + ".");
				}

				json = PanelParser.ReadSpec(new JValue(saved), out specError);
				if (json == null)
				{
					return ToolResult.Error("Saved panel '" + name + "' is broken: " + specError);
				}
			}

			List<PanelIssue> issues = new ();
			PanelSpec spec = PanelValidator.Check(json, issues);
			if (PanelIssue.HasErrors(issues))
			{
				return ToolResult.Error("Panel spec has errors, nothing opened. Fix and call again:\n" + PanelIssue.Format(issues));
			}

			PanelWindow.Open(name, json.ToString(Formatting.Indented), spec.Title);
			string state = name == null ? "not saved; call panel_save to keep it" : "saved as '" + name + "'";
			return ToolResult.Ok("Opened panel '" + spec.Title + "' (" + spec.CountItems() + " items, " + state + ")." + PanelValidator.Warnings(issues));
		}
	}
}
