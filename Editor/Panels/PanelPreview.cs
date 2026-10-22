using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace DTech.Parley.Editor.Panels
{
	internal static class PanelPreview
	{
		private const string DemoTitle = "Preview · all panel items";
		private const string BrokenTitle = "Preview · invalid panel";
		private const string EmptyTitle = "Preview · empty panel";
		private const string MissingAsset = "Assets/__ParleyPreview/Missing.asset";
		private const string MaterialQuery = "t:Material";

		public static void OpenDemo()
		{
			PanelWindow.Open(null, BuildDemo().ToString(Formatting.Indented), DemoTitle);
		}

		public static void OpenBroken()
		{
			JObject spec = new JObject
			{
				["title"] = BrokenTitle,
				["items"] = new JArray
				{
					new JObject { ["type"] = "slider", ["label"] = "Unknown item type" },
					new JObject { ["type"] = "field", ["target"] = "nowhere", ["path"] = "_health" },
					new JObject { ["type"] = "table", ["query"] = MaterialQuery },
					new JObject { ["type"] = "help", ["text"] = "Unknown level", ["level"] = "critical" },
				},
			};

			PanelWindow.Open(null, spec.ToString(Formatting.Indented), BrokenTitle);
		}

		public static void OpenEmpty()
		{
			JObject spec = new JObject { ["title"] = EmptyTitle, ["items"] = new JArray() };
			PanelWindow.Open(null, spec.ToString(Formatting.Indented), EmptyTitle);
		}

		private static JObject BuildDemo()
		{
			JObject targets = new JObject { ["missing"] = MissingAsset };
			JArray items = new JArray
			{
				Item("header", "text", "Text items"),
				Item("label", "text", "A label explains what the fields below change."),
				Help("Info help box.", "info"),
				Help("Warning help box.", "warning"),
				Help("Error help box.", "error"),
				new JObject { ["type"] = "separator" },
			};

			GameObject root = FirstSceneRoot();
			if (root != null && UnityObjectPaths.TryReferenceOf(root, out string sceneReference))
			{
				targets["scene"] = sceneReference;
				items.Add(Item("header", "text", "Scene object: " + root.name));
				items.Add(Field("scene", "Transform", "m_LocalPosition", "Position"));
				items.Add(new JObject
				{
					["type"] = "row",
					["items"] = new JArray
					{
						Field("scene", "Transform", "m_LocalScale", "Scale"),
						Field("scene", null, "m_IsActive", "Active"),
					},
				});

				items.Add(new JObject
				{
					["type"] = "group",
					["label"] = "Group with a whole inspector",
					["foldout"] = true,
					["items"] = new JArray { new JObject { ["type"] = "inspector", ["target"] = "scene" } },
				});

				items.Add(Button("Select in hierarchy", "select", "scene"));
			}
			else
			{
				items.Add(Help("Open a scene with at least one object to preview scene fields and the inspector item.", "info"));
			}

			string material = FirstAssetPath(MaterialQuery);
			if (material != null)
			{
				targets["material"] = material;
				items.Add(Item("header", "text", "Assets"));
				items.Add(Field("material", null, "m_CustomRenderQueue", "Render queue"));
				items.Add(new JObject { ["type"] = "table", ["query"] = MaterialQuery, ["columns"] = new JArray { "m_Shader", "m_CustomRenderQueue", "m_SavedProperties" } });
				items.Add(new JObject
				{
					["type"] = "repeat",
					["label"] = "Repeat: one foldout per material",
					["query"] = MaterialQuery,
					["items"] = new JArray { Field(null, null, "m_CustomRenderQueue", "Render queue"), Field(null, null, "m_Shader", "Shader") },
				});

				items.Add(Button("Ping material", "ping", "material"));
			}
			else
			{
				items.Add(Help("Add a material to the project to preview tables and repeats.", "info"));
			}

			items.Add(Item("header", "text", "States"));
			items.Add(Field("missing", null, "m_Name", "Missing target"));
			items.Add(new JObject { ["type"] = "table", ["query"] = "t:Material ParleyPreviewNoMatches", ["columns"] = new JArray { "m_Shader" } });
			items.Add(Button("Save assets", "save_assets", null));
			JObject menu = Button("Open the Console (asks first)", "menu", null);
			menu["menu"] = "Window/General/Console";
			items.Add(menu);
			return new JObject { ["title"] = DemoTitle, ["targets"] = targets, ["items"] = items };
		}

		private static JObject Item(string type, string key, string value)
		{
			return new JObject { ["type"] = type, [key] = value };
		}

		private static JObject Help(string text, string level)
		{
			return new JObject { ["type"] = "help", ["text"] = text, ["level"] = level };
		}

		private static JObject Field(string target, string component, string path, string label)
		{
			JObject field = new JObject { ["type"] = "field", ["path"] = path, ["label"] = label };
			if (target != null)
			{
				field["target"] = target;
			}

			if (component != null)
			{
				field["component"] = component;
			}

			return field;
		}

		private static JObject Button(string label, string action, string target)
		{
			JObject button = new JObject { ["type"] = "button", ["label"] = label, ["action"] = action };
			if (target != null)
			{
				button["target"] = target;
			}

			return button;
		}

		private static GameObject FirstSceneRoot()
		{
			foreach (GameObject root in UnityObjectPaths.Roots())
			{
				return root;
			}

			return null;
		}

		private static string FirstAssetPath(string query)
		{
			string[] guids = AssetDatabase.FindAssets(query, new[] { "Assets" });
			return guids.Length > 0 ? AssetDatabase.GUIDToAssetPath(guids[0]) : null;
		}
	}
}
