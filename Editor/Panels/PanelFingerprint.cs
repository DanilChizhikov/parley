using System.Collections.Generic;
using UnityEditor;
using Object = UnityEngine.Object;

namespace DTech.Parley.Editor.Panels
{
	internal readonly struct PanelFingerprint
	{
		private const ulong Offset = 14695981039346656037UL;
		private const ulong Prime = 1099511628211UL;

		public ulong Targets { get; }
		public ulong Queries { get; }
		public ulong Coverage { get; }

		public PanelFingerprint(ulong targets, ulong queries, ulong coverage)
		{
			Targets = targets;
			Queries = queries;
			Coverage = coverage;
		}

		public bool Matches(PanelFingerprint other)
		{
			return Targets == other.Targets && Queries == other.Queries && Coverage == other.Coverage;
		}

		public static ulong HashTargets(PanelSpec spec, PanelTargetResolver targets)
		{
			ulong hash = Offset;
			if (spec == null || targets == null)
			{
				return hash;
			}

			foreach (string alias in targets.Aliases)
			{
				Object target = targets.Resolve(alias);
				hash = Mix(hash, alias);
				hash = Mix(hash, spec.Targets.TryGetValue(alias, out string reference) ? reference : null);
				hash = Mix(hash, UnityObjectPaths.Describe(target));
				hash = Mix(hash, target == null ? 0 : target.GetInstanceID());
			}

			return hash;
		}

		public static ulong HashQueries(List<PanelItem> queries)
		{
			ulong hash = Offset;
			if (queries == null)
			{
				return hash;
			}

			foreach (PanelItem item in queries)
			{
				hash = Mix(hash, item.Location);
				hash = Mix(hash, item.Query);
				hash = Mix(hash, item.Folder);
				hash = Mix(hash, PanelQuery.HasValidFolder(item) ? 1 : 0);
				string[] guids = PanelQuery.Guids(item);
				hash = Mix(hash, guids.Length);
				for (int i = 0; i < guids.Length; i++)
				{
					string path = AssetDatabase.GUIDToAssetPath(guids[i]);
					hash = Mix(hash, path);
					if (i >= PanelBuilder.TableRowLimit)
					{
						continue;
					}

					Object asset = AssetDatabase.LoadMainAssetAtPath(path);
					hash = Mix(hash, asset == null ? 0 : asset.GetInstanceID());
				}
			}

			return hash;
		}

		public static ulong HashCoverage(PanelCoverage coverage)
		{
			ulong hash = Offset;
			if (coverage == null)
			{
				return hash;
			}

			foreach (PanelCoverageEntry entry in coverage.Missing)
			{
				hash = Mix(hash, entry.TypeName);
				hash = Mix(hash, entry.Count);
			}

			return hash;
		}

		private static ulong Mix(ulong hash, string value)
		{
			if (value == null)
			{
				return Mix(hash, -1);
			}

			foreach (char character in value)
			{
				hash = (hash ^ character) * Prime;
			}

			return (hash ^ (ulong)value.Length) * Prime;
		}

		private static ulong Mix(ulong hash, int value)
		{
			return (hash ^ (uint)value) * Prime;
		}
	}
}
