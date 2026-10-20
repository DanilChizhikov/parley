using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DTech.Parley.Editor.Panels
{
	internal static class PanelActions
	{
		private const string DialogTitle = "Parley Panel";

		public static bool Run(PanelSpec spec, PanelItem item)
		{
			switch (item.Action)
			{
				case PanelAction.Ping:
				case PanelAction.Select:
				case PanelAction.Open:
					return RunOnTarget(spec, item);
				case PanelAction.SaveAssets:
					AssetDatabase.SaveAssets();
					return false;
				case PanelAction.Menu:
					RunMenu(item.Menu);
					return false;
				case PanelAction.CreateAsset:
					return CreateAsset(item.AssetType, item.AssetPath);
				default:
					return false;
			}
		}

		private static bool RunOnTarget(PanelSpec spec, PanelItem item)
		{
			Object target = PanelTargets.Resolve(spec, item.Target, item.Component);
			if (target == null)
			{
				EditorUtility.DisplayDialog(DialogTitle, "Target '" + item.Target + "' not found.", "OK");
				return false;
			}

			switch (item.Action)
			{
				case PanelAction.Ping:
					EditorGUIUtility.PingObject(target);
					break;
				case PanelAction.Select:
					Selection.activeObject = target;
					break;
				case PanelAction.Open:
					AssetDatabase.OpenAsset(target);
					break;
			}

			return false;
		}

		private static void RunMenu(string menu)
		{
			if (!EditorUtility.DisplayDialog(DialogTitle, "Run menu item?\n\n" + menu, "Run", "Cancel"))
			{
				return;
			}

			if (!EditorApplication.ExecuteMenuItem(menu))
			{
				EditorUtility.DisplayDialog(DialogTitle, "Menu item not found or disabled:\n" + menu, "OK");
			}
		}

		private static bool CreateAsset(string typeName, string assetPath)
		{
			Type type = PanelTargets.FindScriptableObjectType(typeName);
			if (type == null)
			{
				EditorUtility.DisplayDialog(DialogTitle, "ScriptableObject type '" + typeName + "' not found.", "OK");
				return false;
			}

			string folder = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
			if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder))
			{
				EditorUtility.DisplayDialog(DialogTitle, "Folder '" + folder + "' does not exist.", "OK");
				return false;
			}

			string path = AssetDatabase.GenerateUniqueAssetPath(assetPath);
			if (!EditorUtility.DisplayDialog(DialogTitle, "Create " + type.Name + " at\n" + path + "?", "Create", "Cancel"))
			{
				return false;
			}

			ScriptableObject asset = ScriptableObject.CreateInstance(type);
			AssetDatabase.CreateAsset(asset, path);
			AssetDatabase.SaveAssets();
			EditorGUIUtility.PingObject(asset);
			return true;
		}
	}
}
