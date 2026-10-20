using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Panels
{
	internal static class PanelSpecWriter
	{
		private const string TargetsField = "targets";
		private const string IdsField = "ids";

		public static bool Apply(JObject json, PanelSpec spec)
		{
			if (json == null || spec == null)
			{
				return false;
			}

			bool changed = ApplyTargets(json, spec);
			return ApplyIds(json, spec) || changed;
		}

		public static void MergeIds(JObject json, JObject previous)
		{
			if (json == null || previous?[IdsField] is not JObject previousIds)
			{
				return;
			}

			JObject targets = json[TargetsField] as JObject;
			JObject previousTargets = previous[TargetsField] as JObject;
			foreach (JProperty property in previousIds.Properties())
			{
				string current = targets?[property.Name]?.ToString();
				string before = previousTargets?[property.Name]?.ToString();
				if (current == null || !string.Equals(current, before, StringComparison.Ordinal))
				{
					continue;
				}

				JObject ids = Ids(json);
				if (ids[property.Name] == null)
				{
					ids[property.Name] = property.Value.ToString();
				}
			}
		}

		private static bool ApplyTargets(JObject json, PanelSpec spec)
		{
			if (json[TargetsField] is not JObject targets)
			{
				return false;
			}

			bool changed = false;
			foreach (KeyValuePair<string, string> pair in spec.Targets)
			{
				JToken token = targets[pair.Key];
				if (token != null && string.Equals(token.ToString(), pair.Value, StringComparison.Ordinal))
				{
					continue;
				}

				targets[pair.Key] = pair.Value;
				changed = true;
			}

			return changed;
		}

		private static bool ApplyIds(JObject json, PanelSpec spec)
		{
			JObject ids = json[IdsField] as JObject;
			if (spec.Ids.Count == 0)
			{
				if (ids == null)
				{
					return false;
				}

				json.Remove(IdsField);
				return true;
			}

			bool changed = ids == null;
			ids = Ids(json);
			foreach (KeyValuePair<string, string> pair in spec.Ids)
			{
				if (string.Equals(ids[pair.Key]?.ToString(), pair.Value, StringComparison.Ordinal))
				{
					continue;
				}

				ids[pair.Key] = pair.Value;
				changed = true;
			}

			List<string> stale = new ();
			foreach (JProperty property in ids.Properties())
			{
				if (!spec.Targets.ContainsKey(property.Name))
				{
					stale.Add(property.Name);
				}
			}

			foreach (string name in stale)
			{
				ids.Remove(name);
				changed = true;
			}

			return changed;
		}

		private static JObject Ids(JObject json)
		{
			if (json[IdsField] is JObject ids)
			{
				return ids;
			}

			JObject created = new JObject();
			json[IdsField] = created;
			return created;
		}
	}
}
