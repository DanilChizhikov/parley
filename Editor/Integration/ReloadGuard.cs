using System.Collections.Generic;
using UnityEditor;

namespace DTech.Parley.Editor
{
	[InitializeOnLoad]
	internal static class ReloadGuard
	{
		private static readonly HashSet<object> _holders = new ();
		
		public static bool IsLocked => _holders.Count > 0;

		static ReloadGuard()
		{
			AssemblyReloadEvents.beforeAssemblyReload += ReleaseAll;
			EditorApplication.quitting += ReleaseAll;
		}

		public static void Acquire(object holder)
		{
			if (holder == null || !ParleyUserSettings.instance.LockReloadDuringTurn || !_holders.Add(holder))
			{
				return;
			}

			EditorApplication.LockReloadAssemblies();
		}

		public static void Release(object holder)
		{
			if (holder == null || !_holders.Remove(holder))
			{
				return;
			}

			EditorApplication.UnlockReloadAssemblies();
		}

		private static void ReleaseAll()
		{
			foreach (object _ in _holders)
			{
				EditorApplication.UnlockReloadAssemblies();
			}

			_holders.Clear();
		}
	}
}