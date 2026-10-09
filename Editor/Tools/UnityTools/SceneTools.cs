using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace DTech.Parley.Editor.Tools.UnityTools
{
	internal sealed class SelectionTool : IParleyTool
	{
		public string Name => "selection";

		public string Description => "List the objects currently selected in the Unity Editor (assets with paths, scene objects with hierarchy paths).";

		public JObject InputSchema { get; } = new SchemaBuilder().Build();

		public ToolKind Kind => ToolKind.ReadOnly;

		public string Summarize(JObject input) => "Editor selection";

		public string TargetPath(JObject input) => null;

		public Task<ToolResult> ExecuteAsync(JObject input, ToolContext context, CancellationToken cancellationToken)
		{
			Object[] selected = Selection.objects;
			if (selected.Length == 0)
			{
				return Task.FromResult(ToolResult.Ok("Nothing is selected."));
			}

			StringBuilder builder = new StringBuilder();
			foreach (Object target in selected)
			{
				builder.Append("- ").Append(UnityObjectPaths.Describe(target)).Append(" [instanceId ").Append(target.GetInstanceID()).AppendLine("]");
			}

			return Task.FromResult(ToolResult.Ok(builder.ToString().TrimEnd()));
		}
	}

	internal sealed class HierarchyTool : IParleyTool
	{
		public string Name => "hierarchy";

		public string Description =>
			"Show the GameObject hierarchy of the open scenes (or the open prefab) with components. Paths look like Scene:Root/Child.";

		public JObject InputSchema { get; } = new SchemaBuilder()
			.String("root", "Optional hierarchy path to start from, e.g. 'Main:Player/Camera'.")
			.Integer("depth", "How many levels to descend (default 4).")
			.Integer("limit", "Maximum number of objects to list (default 400).")
			.Boolean("components", "List component types (default true).")
			.Build();

		public ToolKind Kind => ToolKind.ReadOnly;

		public string Summarize(JObject input) => "Hierarchy " + ToolInput.String(input, "root", string.Empty);

		public string TargetPath(JObject input) => null;

		public Task<ToolResult> ExecuteAsync(JObject input, ToolContext context, CancellationToken cancellationToken)
		{
			int depth = Mathf.Clamp(ToolInput.Int(input, "depth", 4), 0, 32);
			int limit = Mathf.Clamp(ToolInput.Int(input, "limit", 400), 1, 5000);
			bool components = ToolInput.Bool(input, "components", true);
			string root = ToolInput.String(input, "root");
			StringBuilder builder = new StringBuilder();
			int count = 0;
			if (!string.IsNullOrEmpty(root))
			{
				GameObject start = UnityObjectPaths.FindGameObject(root);
				if (start == null)
				{
					return Task.FromResult(ToolResult.Error("No GameObject at '" + root + "'."));
				}

				Append(builder, start.transform, 0, depth, components, limit, ref count);
			}
			else
			{
				string scene = null;
				foreach (GameObject gameObject in UnityObjectPaths.Roots())
				{
					if (scene != gameObject.scene.name)
					{
						scene = gameObject.scene.name;
						builder.Append("# Scene ").AppendLine(string.IsNullOrEmpty(scene) ? "(prefab)" : scene);
					}

					Append(builder, gameObject.transform, 0, depth, components, limit, ref count);
				}
			}

			if (count >= limit)
			{
				builder.AppendLine("... (limit reached)");
			}

			return Task.FromResult(ToolResult.Ok(builder.Length == 0 ? "No open scene objects." : builder.ToString().TrimEnd()));
		}

		private static void Append(StringBuilder builder, Transform transform, int level, int depth, bool components, int limit, ref int count)
		{
			if (count >= limit)
			{
				return;
			}

			count++;
			builder.Append(' ', level * 2).Append("- ").Append(transform.name);
			if (!transform.gameObject.activeSelf)
			{
				builder.Append(" (inactive)");
			}

			if (components)
			{
				builder.Append(" [");
				Component[] list = transform.GetComponents<Component>();
				for (int i = 0; i < list.Length; i++)
				{
					builder.Append(i > 0 ? ", " : string.Empty).Append(list[i] == null ? "Missing script" : list[i].GetType().Name);
				}

				builder.Append(']');
			}

			builder.AppendLine();
			if (level >= depth)
			{
				if (transform.childCount > 0)
				{
					builder.Append(' ', (level + 1) * 2).Append("... ").Append(transform.childCount).AppendLine(" children");
				}

				return;
			}

			foreach (Transform child in transform)
			{
				Append(builder, child, level + 1, depth, components, limit, ref count);
			}
		}
	}

	internal sealed class InspectTool : IParleyTool
	{
		public string Name => "inspect";

		public string Description =>
			"Read serialized properties of a scene object (hierarchy path), asset (Assets/... path or GUID) or instance id. Defaults to the active selection. Read-only.";

		public JObject InputSchema { get; } = new SchemaBuilder()
			.String("target", "Hierarchy path ('Scene:Root/Child'), asset path, GUID or instance id. Empty = active selection.")
			.String("component", "Only components whose type name contains this text.")
			.Integer("max_properties", "Maximum properties per object (default 120).")
			.Build();

		public ToolKind Kind => ToolKind.ReadOnly;

		public string Summarize(JObject input) => "Inspect " + ToolInput.String(input, "target", "selection");

		public string TargetPath(JObject input) => null;

		public Task<ToolResult> ExecuteAsync(JObject input, ToolContext context, CancellationToken cancellationToken)
		{
			Object target = UnityObjectPaths.Resolve(ToolInput.String(input, "target"));
			if (target == null)
			{
				return Task.FromResult(ToolResult.Error("Target not found."));
			}

			string filter = ToolInput.String(input, "component");
			int maxProperties = Mathf.Clamp(ToolInput.Int(input, "max_properties", 120), 10, 2000);
			StringBuilder builder = new StringBuilder();
			builder.AppendLine(UnityObjectPaths.Describe(target));
			if (target is GameObject gameObject)
			{
				builder.Append("active: ").Append(gameObject.activeSelf).Append(", layer: ").Append(LayerMask.LayerToName(gameObject.layer))
					.Append(", tag: ").AppendLine(gameObject.tag);
				foreach (Component component in gameObject.GetComponents<Component>())
				{
					if (component == null)
					{
						builder.AppendLine("## Missing script");
						continue;
					}

					if (!string.IsNullOrEmpty(filter) && component.GetType().Name.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) < 0)
					{
						continue;
					}

					builder.Append("## ").AppendLine(component.GetType().Name);
					AppendProperties(builder, component, maxProperties);
				}
			}
			else
			{
				AppendProperties(builder, target, maxProperties);
			}

			return Task.FromResult(ToolResult.Ok(builder.ToString().TrimEnd()));
		}

		private static void AppendProperties(StringBuilder builder, Object target, int maxProperties)
		{
			using SerializedObject serializedObject = new SerializedObject(target);
			SerializedProperty property = serializedObject.GetIterator();
			int count = 0;
			bool enterChildren = true;
			while (property.NextVisible(enterChildren))
			{
				enterChildren = property.propertyType == SerializedPropertyType.Generic && !property.isArray && property.depth < 3;
				if (property.name == "m_Script")
				{
					continue;
				}

				if (++count > maxProperties)
				{
					builder.AppendLine("  ...");
					break;
				}

				builder.Append(' ', 2 + property.depth * 2).Append(property.displayName).Append(": ").AppendLine(FormatValue(property));
			}
		}

		private static string FormatValue(SerializedProperty property)
		{
			switch (property.propertyType)
			{
				case SerializedPropertyType.Integer:
				case SerializedPropertyType.LayerMask:
				case SerializedPropertyType.Character:
					return property.longValue.ToString();
				case SerializedPropertyType.Boolean:
					return property.boolValue.ToString();
				case SerializedPropertyType.Float:
					return property.doubleValue.ToString("G6");
				case SerializedPropertyType.String:
					return "\"" + property.stringValue + "\"";
				case SerializedPropertyType.Color:
					return property.colorValue.ToString();
				case SerializedPropertyType.ObjectReference:
					return property.objectReferenceValue == null ? "None" : UnityObjectPaths.Describe(property.objectReferenceValue);
				case SerializedPropertyType.Enum:
					return property.enumValueIndex >= 0 && property.enumValueIndex < property.enumDisplayNames.Length
						? property.enumDisplayNames[property.enumValueIndex]
						: property.intValue.ToString();
				case SerializedPropertyType.Vector2:
					return property.vector2Value.ToString();
				case SerializedPropertyType.Vector3:
					return property.vector3Value.ToString();
				case SerializedPropertyType.Vector4:
					return property.vector4Value.ToString();
				case SerializedPropertyType.Vector2Int:
					return property.vector2IntValue.ToString();
				case SerializedPropertyType.Vector3Int:
					return property.vector3IntValue.ToString();
				case SerializedPropertyType.Rect:
					return property.rectValue.ToString();
				case SerializedPropertyType.Bounds:
					return property.boundsValue.ToString();
				case SerializedPropertyType.Quaternion:
					return property.quaternionValue.eulerAngles + " (euler)";
				case SerializedPropertyType.ArraySize:
					return property.intValue.ToString();
				case SerializedPropertyType.ManagedReference:
					return property.managedReferenceFullTypename;
				default:
					return property.isArray ? "array[" + property.arraySize + "]" : property.propertyType.ToString();
			}
		}
	}
}
