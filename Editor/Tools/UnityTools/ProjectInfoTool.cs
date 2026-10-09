using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace DTech.Parley.Editor.Tools.UnityTools
{
	internal sealed class ProjectInfoTool : IParleyTool
	{
		public string Name => "project_info";

		public string Description =>
			"Describe the Unity project: Unity version, product, build target, scripting backend, render pipeline, scenes in build and package dependencies.";

		public JObject InputSchema { get; } = new SchemaBuilder().Build();

		public ToolKind Kind => ToolKind.ReadOnly;

		public string Summarize(JObject input) => "Project info";

		public string TargetPath(JObject input) => null;

		public Task<ToolResult> ExecuteAsync(JObject input, ToolContext context, CancellationToken cancellationToken)
		{
			StringBuilder builder = new StringBuilder();
			builder.Append("Unity: ").AppendLine(Application.unityVersion);
			builder.Append("Product: ").Append(PlayerSettings.productName).Append(" by ").AppendLine(PlayerSettings.companyName);
			builder.Append("Project root: ").AppendLine(ProjectPaths.Root);
			builder.Append("Active build target: ").AppendLine(EditorUserBuildSettings.activeBuildTarget.ToString());
			try
			{
				NamedBuildTarget target = NamedBuildTarget.FromBuildTargetGroup(BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget));
				builder.Append("Scripting backend: ").AppendLine(PlayerSettings.GetScriptingBackend(target).ToString());
				builder.Append("API compatibility: ").AppendLine(PlayerSettings.GetApiCompatibilityLevel(target).ToString());
			}
			catch (System.Exception)
			{
			}

			RenderPipelineAsset pipeline = GraphicsSettings.defaultRenderPipeline;
			builder.Append("Render pipeline: ").AppendLine(pipeline == null ? "Built-in" : pipeline.GetType().Name + " (" + pipeline.name + ")");
			builder.Append("Play mode: ").AppendLine(EditorApplication.isPlaying ? "playing" : "edit mode");

			builder.AppendLine("Scenes in build:");
			foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
			{
				builder.Append("- ").Append(scene.path).AppendLine(scene.enabled ? string.Empty : " (disabled)");
			}

			string manifest = Path.Combine(ProjectPaths.Root, "Packages", "manifest.json");
			if (File.Exists(manifest))
			{
				builder.AppendLine("Packages (manifest.json):");
				try
				{
					JObject json = JObject.Parse(File.ReadAllText(manifest));
					if (json["dependencies"] is JObject dependencies)
					{
						foreach (JProperty dependency in dependencies.Properties())
						{
							if (dependency.Name.StartsWith("com.unity.modules."))
							{
								continue;
							}

							builder.Append("- ").Append(dependency.Name).Append(": ").AppendLine(dependency.Value.ToString());
						}
					}
				}
				catch (System.Exception exception)
				{
					builder.Append("  (unreadable: ").Append(exception.Message).AppendLine(")");
				}
			}

			return Task.FromResult(ToolResult.Ok(builder.ToString().TrimEnd()));
		}
	}
}
