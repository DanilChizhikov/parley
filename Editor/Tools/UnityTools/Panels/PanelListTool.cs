using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DTech.Parley.Editor.Panels;
using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Tools.UnityTools
{
	internal sealed class PanelListTool : IParleyTool
	{
		public string Name => "panel_list";

		public string Description => "List saved panels (UserSettings/Parley/Panels) with their titles and targets. Read-only.";

		public JObject InputSchema { get; } = new SchemaBuilder().Build();

		public ToolKind Kind => ToolKind.ReadOnly;

		public string Summarize(JObject input) => "List panels";

		public string TargetPath(JObject input) => null;

		public Task<ToolResult> ExecuteAsync(JObject input, ToolContext context, CancellationToken cancellationToken)
		{
			PanelStore store = PanelStore.Default;
			IReadOnlyList<string> names = store.List();
			if (names.Count == 0)
			{
				return Task.FromResult(ToolResult.Ok("No saved panels."));
			}

			StringBuilder builder = new StringBuilder();
			foreach (string name in names)
			{
				builder.Append("- ").Append(name);
				if (store.TryLoad(name, out string json))
				{
					List<PanelIssue> issues = new ();
					PanelSpec spec = PanelParser.Parse(json, issues);
					if (spec == null)
					{
						builder.Append(": (invalid JSON)");
					}
					else
					{
						builder.Append(": ").Append(spec.Title).Append(" (").Append(spec.CountItems()).Append(" items");
						if (spec.Targets.Count > 0)
						{
							builder.Append("; targets: ").Append(string.Join(", ", spec.Targets.Values));
						}

						builder.Append(')');
					}
				}

				builder.AppendLine();
			}

			return Task.FromResult(ToolResult.Ok(builder.ToString().TrimEnd()));
		}
	}
}
