using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace DTech.Parley.Editor.Tools.UnityTools
{
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