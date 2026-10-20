using System;
using System.Collections.Generic;
using UnityEditor;
using Object = UnityEngine.Object;

namespace DTech.Parley.Editor.Panels
{
	internal sealed class PanelCoverage
	{
		public const int MaxTypes = 4;

		public static PanelCoverage Empty { get; } = new PanelCoverage(new List<PanelCoverageEntry>());

		public IReadOnlyList<PanelCoverageEntry> Missing => _missing;

		private readonly List<PanelCoverageEntry> _missing;

		private PanelCoverage(List<PanelCoverageEntry> missing)
		{
			_missing = missing;
		}

		public static PanelCoverage Of(PanelSpec spec, PanelTargetResolver targets)
		{
			if (spec == null || targets == null)
			{
				return Empty;
			}

			HashSet<string> shown = new (StringComparer.Ordinal);
			List<string> types = new ();
			foreach (string alias in targets.Aliases)
			{
				Object target = targets.Resolve(alias);
				if (target == null || !EditorUtility.IsPersistent(target))
				{
					continue;
				}

				shown.Add(PanelQuery.KeyOf(target));
				string typeName = target.GetType().Name;
				if (!types.Contains(typeName) && types.Count < MaxTypes)
				{
					types.Add(typeName);
				}
			}

			if (types.Count == 0)
			{
				return Empty;
			}

			foreach (PanelItem query in spec.Queries)
			{
				types.RemoveAll(typeName => Covers(query.Query, typeName));
				foreach (string guid in PanelQuery.Guids(query))
				{
					shown.Add(guid);
				}
			}

			List<PanelCoverageEntry> missing = new ();
			foreach (string typeName in types)
			{
				int count = 0;
				string firstPath = null;
				foreach (string guid in AssetDatabase.FindAssets("t:" + typeName))
				{
					if (shown.Contains(guid))
					{
						continue;
					}

					count++;
					firstPath ??= AssetDatabase.GUIDToAssetPath(guid);
				}

				if (count > 0)
				{
					missing.Add(new PanelCoverageEntry(typeName, count, firstPath));
				}
			}

			return missing.Count == 0 ? Empty : new PanelCoverage(missing);
		}

		private static bool Covers(string query, string typeName)
		{
			return !string.IsNullOrEmpty(query) && query.IndexOf("t:" + typeName, StringComparison.OrdinalIgnoreCase) >= 0;
		}
	}
}
