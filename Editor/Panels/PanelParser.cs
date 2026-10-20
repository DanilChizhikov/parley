using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Panels
{
	internal static class PanelParser
	{
		public const int MaxDepth = 6;
		public const int MaxItems = 400;

		public const string Reference =
			"Panel spec (JSON object): { \"title\": string, \"targets\": { alias: ref }, \"items\": [item...] }.\n" +
			"ref = asset path ('Assets/...'), GUID, instance id or hierarchy path ('Scene:Root/Child').\n" +
			"Items (\"type\" + fields):\n" +
			"- header {text} | label {text} | help {text, level: info|warning|error} | separator\n" +
			"- field {target, path, label?, component?}: one SerializedProperty by propertyPath (e.g. '_health', 'stats.speed', 'items.Array.data[0]'). Undo works.\n" +
			"- inspector {target, component?}: the object's full inspector.\n" +
			"- group {label, foldout?: bool, items} | row {items}: layout containers, max nesting depth 6.\n" +
			"- repeat {query, folder?, label?, items}: repeats its items once per asset the query finds, each in a foldout. " +
			"Inside it leave 'target' out (or use '$item') to mean the current asset. Use it for a whole family of assets: " +
			"the panel then keeps up by itself when assets are added, renamed or removed.\n" +
			"- table {query, folder?, columns: [propertyPath...]}: one row per asset from AssetDatabase.FindAssets(query, folder), e.g. query 't:EnemyConfig'.\n" +
			"- button {label, action, ...}: action = ping|select|open {target} | save_assets | menu {menu: 'Menu/Path'} | create_asset {asset_type: ScriptableObject type name, asset_path: 'Assets/....asset'}. menu and create_asset ask the user to confirm.\n" +
			"component = component type name on a GameObject target. No code runs: only these items and actions exist.\n" +
			"\"ids\" is written and maintained by Parley (alias -> stable object id): never author or edit it, and keep it when you re-save a spec. " +
			"Panels re-resolve their targets and queries by themselves, so a renamed, moved or newly added asset needs no new panel.";

		public const string ScopeAlias = "$item";

		private static readonly Dictionary<string, PanelItemType> _types = new (StringComparer.OrdinalIgnoreCase)
		{
			["header"] = PanelItemType.Header,
			["label"] = PanelItemType.Label,
			["help"] = PanelItemType.Help,
			["separator"] = PanelItemType.Separator,
			["field"] = PanelItemType.Field,
			["inspector"] = PanelItemType.Inspector,
			["group"] = PanelItemType.Group,
			["row"] = PanelItemType.Row,
			["table"] = PanelItemType.Table,
			["button"] = PanelItemType.Button,
			["repeat"] = PanelItemType.Repeat,
		};

		private static readonly Dictionary<string, PanelAction> _actions = new (StringComparer.OrdinalIgnoreCase)
		{
			["ping"] = PanelAction.Ping,
			["select"] = PanelAction.Select,
			["open"] = PanelAction.Open,
			["save_assets"] = PanelAction.SaveAssets,
			["menu"] = PanelAction.Menu,
			["create_asset"] = PanelAction.CreateAsset,
		};

		private static readonly Dictionary<string, PanelHelpLevel> _levels = new (StringComparer.OrdinalIgnoreCase)
		{
			["info"] = PanelHelpLevel.Info,
			["warning"] = PanelHelpLevel.Warning,
			["error"] = PanelHelpLevel.Error,
		};

		public static JObject ReadSpec(JToken token, out string error)
		{
			error = null;
			if (token == null || token.Type == JTokenType.Null)
			{
				return null;
			}

			if (token is JObject spec)
			{
				return spec;
			}

			if (token.Type == JTokenType.String)
			{
				try
				{
					if (JToken.Parse((string)token) is JObject parsed)
					{
						return parsed;
					}
				}
				catch (JsonException exception)
				{
					error = "spec is not valid JSON: " + exception.Message;
					return null;
				}
			}

			error = "spec must be a JSON object.";
			return null;
		}

		public static JObject ReadDocument(string json, List<PanelIssue> issues)
		{
			JObject spec;
			try
			{
				spec = JToken.Parse(json ?? string.Empty) as JObject;
			}
			catch (JsonException exception)
			{
				issues.Add(PanelIssue.Error(string.Empty, "Invalid JSON: " + exception.Message));
				return null;
			}

			if (spec == null)
			{
				issues.Add(PanelIssue.Error(string.Empty, "Spec must be a JSON object."));
			}

			return spec;
		}

		public static PanelSpec Parse(string json, List<PanelIssue> issues)
		{
			JObject spec = ReadDocument(json, issues);
			return spec == null ? null : Parse(spec, issues);
		}

		public static PanelSpec Parse(JObject json, List<PanelIssue> issues)
		{
			PanelSpec spec = new PanelSpec { Title = String(json, "title") };
			if (string.IsNullOrWhiteSpace(spec.Title))
			{
				issues.Add(PanelIssue.Error("title", "Required."));
			}

			JToken targets = json["targets"];
			if (targets is JObject targetMap)
			{
				foreach (JProperty property in targetMap.Properties())
				{
					string reference = property.Value.Type == JTokenType.String || property.Value.Type == JTokenType.Integer ? property.Value.ToString() : null;
					if (string.IsNullOrWhiteSpace(reference))
					{
						issues.Add(PanelIssue.Error("targets." + property.Name, "Must be a non-empty string reference."));
						continue;
					}

					spec.Targets[property.Name] = reference.Trim();
				}
			}
			else if (targets != null && targets.Type != JTokenType.Null)
			{
				issues.Add(PanelIssue.Error("targets", "Must be an object of alias -> reference."));
			}

			if (json["ids"] is JObject idMap)
			{
				foreach (JProperty property in idMap.Properties())
				{
					string id = property.Value.Type == JTokenType.String ? (string)property.Value : null;
					if (!string.IsNullOrWhiteSpace(id) && spec.Targets.ContainsKey(property.Name))
					{
						spec.Ids[property.Name] = id.Trim();
					}
				}
			}

			int count = 0;
			if (json["items"] is JArray items)
			{
				ParseItems(items, "items", 1, spec, spec.Items, issues, ref count, false);
				if (spec.Items.Count == 0 && items.Count == 0)
				{
					issues.Add(PanelIssue.Warning("items", "Panel has no items."));
				}
			}
			else
			{
				issues.Add(PanelIssue.Error("items", "Required array of items."));
			}

			return spec;
		}

		private static void ParseItems(JArray array, string location, int depth, PanelSpec spec, List<PanelItem> into, List<PanelIssue> issues, ref int count, bool scoped)
		{
			for (int i = 0; i < array.Count; i++)
			{
				string itemLocation = location + "[" + i + "]";
				if (++count > MaxItems)
				{
					issues.Add(PanelIssue.Error(itemLocation, "Too many items (max " + MaxItems + ")."));
					return;
				}

				if (array[i] is not JObject json)
				{
					issues.Add(PanelIssue.Error(itemLocation, "Item must be an object."));
					continue;
				}

				PanelItem item = ParseItem(json, itemLocation, depth, spec, issues, ref count, scoped);
				if (item != null)
				{
					into.Add(item);
				}
			}
		}

		private static PanelItem ParseItem(JObject json, string location, int depth, PanelSpec spec, List<PanelIssue> issues, ref int count, bool scoped)
		{
			string typeName = String(json, "type");
			if (string.IsNullOrEmpty(typeName) || !_types.TryGetValue(typeName, out PanelItemType type))
			{
				issues.Add(PanelIssue.Error(location, "Unknown type '" + typeName + "'. Expected one of: " + string.Join(", ", _types.Keys) + "."));
				return null;
			}

			PanelItem item = new PanelItem
			{
				Type = type,
				Location = location,
				Text = String(json, "text"),
				Label = String(json, "label"),
				Target = String(json, "target"),
				Component = String(json, "component"),
				Path = String(json, "path"),
				Foldout = json["foldout"]?.Type == JTokenType.Boolean && (bool)json["foldout"],
				Query = String(json, "query"),
				Folder = String(json, "folder"),
				Menu = String(json, "menu"),
				AssetType = String(json, "asset_type"),
				AssetPath = String(json, "asset_path"),
			};

			switch (type)
			{
				case PanelItemType.Header:
				case PanelItemType.Label:
					Require(item.Text, "text", location, issues);
					break;
				case PanelItemType.Help:
					Require(item.Text, "text", location, issues);
					string level = String(json, "level");
					if (string.IsNullOrEmpty(level))
					{
						item.Level = PanelHelpLevel.Info;
					}
					else if (_levels.TryGetValue(level, out PanelHelpLevel parsed))
					{
						item.Level = parsed;
					}
					else
					{
						issues.Add(PanelIssue.Warning(location, "Unknown level '" + level + "', using info."));
					}

					break;
				case PanelItemType.Field:
					RequireTarget(item, spec, location, issues, scoped);
					Require(item.Path, "path", location, issues);
					break;
				case PanelItemType.Inspector:
					RequireTarget(item, spec, location, issues, scoped);
					break;
				case PanelItemType.Group:
				case PanelItemType.Row:
					if (type == PanelItemType.Group)
					{
						Require(item.Label, "label", location, issues);
					}

					if (depth >= MaxDepth)
					{
						issues.Add(PanelIssue.Error(location, "Nested too deep (max " + MaxDepth + ")."));
						break;
					}

					if (json["items"] is JArray children)
					{
						ParseItems(children, location + ".items", depth + 1, spec, item.Items, issues, ref count, scoped);
					}
					else
					{
						issues.Add(PanelIssue.Error(location, "'items' array is required."));
					}

					break;
				case PanelItemType.Repeat:
					Require(item.Query, "query", location, issues);
					spec.Queries.Add(item);
					if (depth >= MaxDepth)
					{
						issues.Add(PanelIssue.Error(location, "Nested too deep (max " + MaxDepth + ")."));
						break;
					}

					if (json["items"] is JArray repeated)
					{
						ParseItems(repeated, location + ".items", depth + 1, spec, item.Items, issues, ref count, true);
					}
					else
					{
						issues.Add(PanelIssue.Error(location, "'items' array is required."));
					}

					break;
				case PanelItemType.Table:
					Require(item.Query, "query", location, issues);
					spec.Queries.Add(item);
					if (json["columns"] is JArray columns)
					{
						foreach (JToken column in columns)
						{
							string path = column.Type == JTokenType.String ? (string)column : null;
							if (string.IsNullOrWhiteSpace(path))
							{
								issues.Add(PanelIssue.Error(location, "Columns must be non-empty property paths."));
								continue;
							}

							item.Columns.Add(path.Trim());
						}
					}

					if (item.Columns.Count == 0)
					{
						issues.Add(PanelIssue.Error(location, "'columns' must list at least one property path."));
					}

					break;
				case PanelItemType.Button:
					Require(item.Label, "label", location, issues);
					ParseAction(json, item, spec, location, issues, scoped);
					break;
			}

			return item;
		}

		private static void ParseAction(JObject json, PanelItem item, PanelSpec spec, string location, List<PanelIssue> issues, bool scoped)
		{
			string actionName = String(json, "action");
			if (string.IsNullOrEmpty(actionName) || !_actions.TryGetValue(actionName, out PanelAction action))
			{
				issues.Add(PanelIssue.Error(location, "Unknown action '" + actionName + "'. Expected one of: " + string.Join(", ", _actions.Keys) + "."));
				return;
			}

			item.Action = action;
			switch (action)
			{
				case PanelAction.Ping:
				case PanelAction.Select:
				case PanelAction.Open:
					RequireTarget(item, spec, location, issues, scoped);
					break;
				case PanelAction.Menu:
					Require(item.Menu, "menu", location, issues);
					break;
				case PanelAction.CreateAsset:
					Require(item.AssetType, "asset_type", location, issues);
					Require(item.AssetPath, "asset_path", location, issues);
					if (!string.IsNullOrEmpty(item.AssetPath) && (!item.AssetPath.StartsWith("Assets/", StringComparison.Ordinal)
						|| !item.AssetPath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)))
					{
						issues.Add(PanelIssue.Error(location, "asset_path must look like 'Assets/.../Name.asset'."));
					}

					break;
			}
		}

		private static void RequireTarget(PanelItem item, PanelSpec spec, string location, List<PanelIssue> issues, bool scoped)
		{
			if (scoped && (string.IsNullOrEmpty(item.Target) || string.Equals(item.Target, ScopeAlias, StringComparison.Ordinal)))
			{
				item.Target = null;
				return;
			}

			if (string.IsNullOrEmpty(item.Target))
			{
				issues.Add(PanelIssue.Error(location, "'target' is required."));
			}
			else if (!spec.Targets.ContainsKey(item.Target))
			{
				issues.Add(PanelIssue.Error(location, "Unknown target alias '" + item.Target + "'. Declare it in 'targets'."));
			}
		}

		private static void Require(string value, string name, string location, List<PanelIssue> issues)
		{
			if (string.IsNullOrWhiteSpace(value))
			{
				issues.Add(PanelIssue.Error(location, "'" + name + "' is required."));
			}
		}

		private static string String(JObject json, string name)
		{
			JToken token = json[name];
			if (token == null || token.Type == JTokenType.Null)
			{
				return null;
			}

			string value = token.Type == JTokenType.String ? (string)token : token.ToString();
			return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
		}
	}
}
