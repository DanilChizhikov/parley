using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DTech.Parley.Editor.Panels
{
	internal static class PanelTargets
	{
		private const int SuggestionCount = 5;
		private const int ListedPropertyCount = 20;
		private const int MaxSuggestionDistance = 3;
		private const int MaxPropertyDepth = 3;

		public static bool IsAssetReference(string reference)
		{
			if (string.IsNullOrEmpty(reference))
			{
				return false;
			}

			return reference.StartsWith("Assets/", StringComparison.Ordinal) || reference.StartsWith("Packages/", StringComparison.Ordinal)
				|| !string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(reference));
		}

		public static Object WithComponent(Object target, string component)
		{
			if (target == null || string.IsNullOrEmpty(component))
			{
				return target;
			}

			GameObject gameObject = target as GameObject ?? (target as Component)?.gameObject;
			if (gameObject == null)
			{
				return null;
			}

			Component partial = null;
			foreach (Component candidate in gameObject.GetComponents<Component>())
			{
				if (candidate == null)
				{
					continue;
				}

				string name = candidate.GetType().Name;
				if (string.Equals(name, component, StringComparison.Ordinal))
				{
					return candidate;
				}

				if (partial == null && name.IndexOf(component, StringComparison.OrdinalIgnoreCase) >= 0)
				{
					partial = candidate;
				}
			}

			return partial;
		}

		public static List<Object> FindAssets(string query, string folder)
		{
			List<Object> assets = new ();
			string[] guids = string.IsNullOrEmpty(folder) ? AssetDatabase.FindAssets(query) : AssetDatabase.FindAssets(query, new[] { folder });
			foreach (string guid in guids)
			{
				Object asset = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));
				if (asset != null)
				{
					assets.Add(asset);
				}
			}

			return assets;
		}

		public static Type FindScriptableObjectType(string name)
		{
			if (string.IsNullOrEmpty(name))
			{
				return null;
			}

			Type partial = null;
			foreach (Type type in TypeCache.GetTypesDerivedFrom<ScriptableObject>())
			{
				if (type.IsAbstract || type.IsGenericTypeDefinition || typeof(UnityEditor.Editor).IsAssignableFrom(type) || typeof(EditorWindow).IsAssignableFrom(type))
				{
					continue;
				}

				if (string.Equals(type.FullName, name, StringComparison.Ordinal))
				{
					return type;
				}

				if (partial == null && string.Equals(type.Name, name, StringComparison.Ordinal))
				{
					partial = type;
				}
			}

			return partial;
		}

		public static void Validate(PanelSpec spec, List<PanelIssue> issues)
		{
			PanelTargetResolver targets = new PanelTargetResolver(spec);
			foreach (KeyValuePair<string, string> pair in spec.Targets)
			{
				if (targets.Resolve(pair.Key) != null)
				{
					continue;
				}

				string message = "'" + pair.Value + "' not found.";
				if (targets.Status(pair.Key) == PanelTargetStatus.MissingWithId)
				{
					message += " Parley kept its id and will reconnect it automatically if it comes back.";
				}

				if (IsAssetReference(pair.Value))
				{
					issues.Add(PanelIssue.Error("targets." + pair.Key, message));
				}
				else
				{
					issues.Add(PanelIssue.Warning("targets." + pair.Key, message + " Is the scene open? The panel will show a placeholder until it resolves."));
				}
			}

			ValidateItems(spec.Items, targets, issues, null);
		}

		public static string CheckProperty(Object target, string path)
		{
			using SerializedObject serializedObject = new SerializedObject(target);
			if (serializedObject.FindProperty(path) != null)
			{
				return null;
			}

			List<string> paths = PropertyPaths(serializedObject);
			List<string> similar = Similar(path, paths);
			string message = "Property '" + path + "' not found on " + UnityObjectPaths.Describe(target) + ".";
			if (similar.Count > 0)
			{
				return message + " Did you mean: " + string.Join(", ", similar) + "?";
			}

			if (paths.Count > ListedPropertyCount)
			{
				paths.RemoveRange(ListedPropertyCount, paths.Count - ListedPropertyCount);
				paths.Add("...");
			}

			return message + " Available: " + (paths.Count == 0 ? "(none)" : string.Join(", ", paths)) + ".";
		}

		public static int Distance(string a, string b)
		{
			int[] previous = new int[b.Length + 1];
			int[] current = new int[b.Length + 1];
			for (int j = 0; j <= b.Length; j++)
			{
				previous[j] = j;
			}

			for (int i = 1; i <= a.Length; i++)
			{
				current[0] = i;
				for (int j = 1; j <= b.Length; j++)
				{
					int cost = char.ToLowerInvariant(a[i - 1]) == char.ToLowerInvariant(b[j - 1]) ? 0 : 1;
					current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
				}

				(previous, current) = (current, previous);
			}

			return previous[b.Length];
		}

		private static void ValidateItems(List<PanelItem> items, PanelTargetResolver targets, List<PanelIssue> issues, Object scope)
		{
			foreach (PanelItem item in items)
			{
				switch (item.Type)
				{
					case PanelItemType.Field:
					case PanelItemType.Inspector:
						Object target = TargetOf(item, targets, issues, scope);
						if (target != null && item.Type == PanelItemType.Field && !string.IsNullOrEmpty(item.Path))
						{
							AddError(item, CheckProperty(target, item.Path), issues);
						}

						break;
					case PanelItemType.Group:
					case PanelItemType.Row:
						ValidateItems(item.Items, targets, issues, scope);
						break;
					case PanelItemType.Table:
						ValidateTable(item, issues);
						break;
					case PanelItemType.Repeat:
						ValidateRepeat(item, targets, issues);
						break;
					case PanelItemType.Button:
						if (item.Action == PanelAction.CreateAsset && !string.IsNullOrEmpty(item.AssetType) && FindScriptableObjectType(item.AssetType) == null)
						{
							issues.Add(PanelIssue.Error(item.Location, "ScriptableObject type '" + item.AssetType + "' not found."));
						}

						break;
				}
			}
		}

		private static void ValidateTable(PanelItem item, List<PanelIssue> issues)
		{
			if (string.IsNullOrEmpty(item.Query))
			{
				return;
			}

			if (!PanelQuery.HasValidFolder(item))
			{
				issues.Add(PanelIssue.Error(item.Location, "Folder '" + item.Folder + "' does not exist."));
				return;
			}

			List<Object> assets = PanelQuery.Assets(item);
			if (assets.Count == 0)
			{
				issues.Add(PanelIssue.Warning(item.Location, "Query '" + item.Query + "' matched no assets."));
				return;
			}

			foreach (string column in item.Columns)
			{
				AddError(item, CheckProperty(assets[0], column), issues);
			}
		}

		private static void ValidateRepeat(PanelItem item, PanelTargetResolver targets, List<PanelIssue> issues)
		{
			if (string.IsNullOrEmpty(item.Query))
			{
				return;
			}

			if (!PanelQuery.HasValidFolder(item))
			{
				issues.Add(PanelIssue.Error(item.Location, "Folder '" + item.Folder + "' does not exist."));
				return;
			}

			List<Object> assets = PanelQuery.Assets(item);
			if (assets.Count == 0)
			{
				issues.Add(PanelIssue.Warning(item.Location, "Query '" + item.Query + "' matched no assets."));
				return;
			}

			ValidateItems(item.Items, targets, issues, assets[0]);
		}

		private static Object TargetOf(PanelItem item, PanelTargetResolver targets, List<PanelIssue> issues, Object scope)
		{
			Object target = string.IsNullOrEmpty(item.Target) ? scope : targets.Resolve(item.Target);
			if (target == null)
			{
				return null;
			}

			Object withComponent = WithComponent(target, item.Component);
			if (withComponent == null)
			{
				issues.Add(PanelIssue.Error(item.Location, "Component '" + item.Component + "' not found on " + UnityObjectPaths.Describe(target) + "."));
			}

			return withComponent;
		}

		private static void AddError(PanelItem item, string error, List<PanelIssue> issues)
		{
			if (error != null)
			{
				issues.Add(PanelIssue.Error(item.Location, error));
			}
		}

		private static List<string> PropertyPaths(SerializedObject serializedObject)
		{
			List<string> paths = new ();
			SerializedProperty property = serializedObject.GetIterator();
			bool enterChildren = true;
			while (property.NextVisible(enterChildren))
			{
				enterChildren = property.propertyType == SerializedPropertyType.Generic && !property.isArray && property.depth < MaxPropertyDepth;
				if (property.propertyPath != "m_Script")
				{
					paths.Add(property.propertyPath);
				}
			}

			return paths;
		}

		private static List<string> Similar(string path, List<string> paths)
		{
			string needle = Bare(path);
			List<(int distance, string path)> scored = new ();
			foreach (string candidate in paths)
			{
				string bare = Bare(candidate);
				int distance = Distance(needle, bare);
				bool contains = needle.Length > 1 && (bare.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0 || needle.IndexOf(bare, StringComparison.OrdinalIgnoreCase) >= 0);
				if (contains || distance <= MaxSuggestionDistance)
				{
					scored.Add((contains ? 0 : distance, candidate));
				}
			}

			scored.Sort((left, right) => left.distance != right.distance ? left.distance.CompareTo(right.distance) : string.CompareOrdinal(left.path, right.path));
			List<string> result = new ();
			for (int i = 0; i < scored.Count && i < SuggestionCount; i++)
			{
				result.Add(scored[i].path);
			}

			return result;
		}

		private static string Bare(string path)
		{
			int dot = path.LastIndexOf('.');
			string name = dot >= 0 ? path.Substring(dot + 1) : path;
			if (name.StartsWith("m_", StringComparison.Ordinal))
			{
				return name.Substring(2);
			}

			return name.TrimStart('_');
		}
	}
}
