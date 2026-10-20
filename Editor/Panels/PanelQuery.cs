using System.Collections.Generic;
using UnityEditor;
using Object = UnityEngine.Object;

namespace DTech.Parley.Editor.Panels
{
	internal static class PanelQuery
	{
		public static bool HasValidFolder(PanelItem item)
		{
			return string.IsNullOrEmpty(item.Folder) || AssetDatabase.IsValidFolder(item.Folder);
		}

		public static string[] Guids(PanelItem item)
		{
			if (string.IsNullOrEmpty(item.Query) || !HasValidFolder(item))
			{
				return System.Array.Empty<string>();
			}

			return string.IsNullOrEmpty(item.Folder)
				? AssetDatabase.FindAssets(item.Query)
				: AssetDatabase.FindAssets(item.Query, new[] { item.Folder });
		}

		public static List<Object> Assets(PanelItem item)
		{
			List<Object> assets = new ();
			foreach (string guid in Guids(item))
			{
				Object asset = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));
				if (asset != null)
				{
					assets.Add(asset);
				}
			}

			return assets;
		}

		public static string KeyOf(Object asset)
		{
			if (asset == null)
			{
				return string.Empty;
			}

			string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
			return string.IsNullOrEmpty(guid) ? asset.GetInstanceID().ToString() : guid;
		}
	}
}
