using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DTech.Parley.Editor
{
	internal static class GlobalObjectIds
	{
		public static bool TryCapture(Object target, out string id)
		{
			id = null;
			if (target == null || IsInPrefabStage(target))
			{
				return false;
			}

			if (EditorApplication.isPlaying && !EditorUtility.IsPersistent(target))
			{
				return false;
			}

			GlobalObjectId globalId = GlobalObjectId.GetGlobalObjectIdSlow(target);
			if (globalId.identifierType == 0 || globalId.assetGUID.Empty())
			{
				return false;
			}

			id = globalId.ToString();
			return true;
		}

		public static Object Resolve(string id)
		{
			if (string.IsNullOrEmpty(id) || !GlobalObjectId.TryParse(id, out GlobalObjectId parsed))
			{
				return null;
			}

			return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(parsed);
		}

		private static bool IsInPrefabStage(Object target)
		{
			GameObject gameObject = target as GameObject ?? (target as Component)?.gameObject;
			return gameObject != null && PrefabStageUtility.GetPrefabStage(gameObject) != null;
		}
	}
}
