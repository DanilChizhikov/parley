using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DTech.Parley.Editor.Settings;
using DTech.Parley.Editor.Tools.Builtin;
using DTech.Parley.Editor.Tools.UnityTools;
using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Tools
{
	internal sealed class ToolCatalog
	{
		public const string UnityLocalPrefix = "unity_";

		private readonly List<IParleyTool> _tools = new ();
		private readonly Dictionary<string, IParleyTool> _byName = new (StringComparer.Ordinal);

		public IReadOnlyList<IParleyTool> Tools => _tools;

		public ToolCatalog(IEnumerable<IParleyTool> tools)
		{
			foreach (IParleyTool tool in tools)
			{
				_tools.Add(tool);
				_byName[tool.Name] = tool;
			}
		}

		public static IEnumerable<IParleyTool> CreateUnityTools()
		{
			yield return new ConsoleTool();
			yield return new CompileStatusTool();
			yield return new RefreshTool();
			yield return new SelectionTool();
			yield return new HierarchyTool();
			yield return new InspectTool();
			yield return new ProjectInfoTool();
		}

		public static ToolCatalog CreateForMcp(ParleyProjectSettings settings)
		{
			List<IParleyTool> tools = new ();
			foreach (IParleyTool tool in CreateUnityTools())
			{
				if (settings.IsUnityToolEnabled(tool.Name))
				{
					tools.Add(tool);
				}
			}

			return new ToolCatalog(tools);
		}

		public static ToolCatalog CreateForLocalAgent(ParleyProjectSettings settings)
		{
			List<IParleyTool> tools = new ()
			{
				new FileReadTool(),
				new FileWriteTool(),
				new FileEditTool(),
				new GlobTool(),
				new GrepTool(),
				new BashTool(),
				new WebFetchTool(),
				new TodoWriteTool(),
				new AskUserQuestionTool(),
				new EnterPlanModeTool(),
				new ExitPlanModeTool(),
			};

			foreach (IParleyTool tool in CreateUnityTools())
			{
				if (settings.IsUnityToolEnabled(tool.Name))
				{
					tools.Add(new PrefixedTool(UnityLocalPrefix, tool));
				}
			}

			return new ToolCatalog(tools);
		}

		public bool TryGet(string name, out IParleyTool tool)
		{
			return _byName.TryGetValue(name ?? string.Empty, out tool);
		}

		private sealed class PrefixedTool : IParleyTool
		{
			private readonly string _prefix;
			private readonly IParleyTool _inner;

			public string Name => _prefix + _inner.Name;

			public string Description => _inner.Description;

			public JObject InputSchema => _inner.InputSchema;

			public ToolKind Kind => _inner.Kind;

			public PrefixedTool(string prefix, IParleyTool inner)
			{
				_prefix = prefix;
				_inner = inner;
			}

			public string Summarize(JObject input) => _inner.Summarize(input);

			public string TargetPath(JObject input) => _inner.TargetPath(input);

			public Task<ToolResult> ExecuteAsync(JObject input, ToolContext context, CancellationToken cancellationToken)
			{
				return _inner.ExecuteAsync(input, context, cancellationToken);
			}
		}
	}
}
