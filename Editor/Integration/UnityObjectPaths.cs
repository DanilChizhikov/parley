using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DTech.Parley.Editor
{
	internal static class UnityObjectPaths
	{
		public static string HierarchyPath(Transform transform)
		{
			StringBuilder builder = new StringBuilder(transform.name);
			Transform current = transform.parent;
			while (current != null)
			{
				builder.Insert(0, current.name + "/");
				current = current.parent;
			}

			return builder.ToString();
		}

		public static string Describe(Object target)
		{
			if (target == null)
			{
				return "(missing)";
			}

			string assetPath = AssetDatabase.GetAssetPath(target);
			if (!string.IsNullOrEmpty(assetPath))
			{
				return assetPath + " (" + target.GetType().Name + ")";
			}

			if (target is GameObject gameObject)
			{
				return gameObject.scene.name + ":" + HierarchyPath(gameObject.transform) + " (GameObject)";
			}

			if (target is Component component)
			{
				return component.gameObject.scene.name + ":" + HierarchyPath(component.transform) + " (" + component.GetType().Name + ")";
			}

			return target.name + " (" + target.GetType().Name + ")";
		}

		public static IEnumerable<GameObject> Roots()
		{
			PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
			if (stage != null && stage.prefabContentsRoot != null)
			{
				yield return stage.prefabContentsRoot;
				yield break;
			}

			for (int i = 0; i < SceneManager.sceneCount; i++)
			{
				Scene scene = SceneManager.GetSceneAt(i);
				if (!scene.isLoaded)
				{
					continue;
				}

				foreach (GameObject root in scene.GetRootGameObjects())
				{
					yield return root;
				}
			}
		}

		public static Object Resolve(string target)
		{
			if (string.IsNullOrWhiteSpace(target))
			{
				return Selection.activeObject;
			}

			target = target.Trim();
			if (int.TryParse(target, out int instanceId))
			{
				return EditorUtility.InstanceIDToObject(instanceId);
			}

			if (target.StartsWith("Assets/") || target.StartsWith("Packages/"))
			{
				return AssetDatabase.LoadMainAssetAtPath(target);
			}

			string guidPath = AssetDatabase.GUIDToAssetPath(target);
			if (!string.IsNullOrEmpty(guidPath))
			{
				return AssetDatabase.LoadMainAssetAtPath(guidPath);
			}

			return FindGameObject(target);
		}

		public static GameObject FindGameObject(string path)
		{
			string sceneName = null;
			int colon = path.IndexOf(':');
			if (colon > 0)
			{
				sceneName = path.Substring(0, colon);
				path = path.Substring(colon + 1);
			}

			string[] parts = path.Trim('/').Split('/');
			if (parts.Length == 0)
			{
				return null;
			}

			foreach (GameObject root in Roots())
			{
				if (root.name != parts[0] || (sceneName != null && root.scene.name != sceneName))
				{
					continue;
				}

				if (parts.Length == 1)
				{
					return root;
				}

				Transform child = root.transform.Find(string.Join("/", parts, 1, parts.Length - 1));
				if (child != null)
				{
					return child.gameObject;
				}
			}

			return null;
		}
	}
}