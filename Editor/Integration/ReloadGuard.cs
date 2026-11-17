using System.Collections.Generic;
using UnityEditor;

namespace DTech.Parley.Editor
{
	[InitializeOnLoad]
	internal static class ReloadGuard
	{
		private static readonly HashSet<object> _holders = new ();
		private static bool _locked;
		private static int _idleUpdates;
		
		public static bool IsLocked => _locked;

		static ReloadGuard()
		{
			AssemblyReloadEvents.beforeAssemblyReload += ReleaseAll;
			EditorApplication.quitting += ReleaseAll;
			EditorApplication.update += UpdateHandler;
		}

		public static void Acquire(object holder)
		{
			if (holder == null || !ParleyUserSettings.instance.LockReloadDuringTurn || !_holders.Add(holder))
			{
				return;
			}

			_idleUpdates = 0;
			if (!_locked)
			{
				_locked = true;
				EditorApplication.LockReloadAssemblies();
			}
		}

		public static void Release(object holder)
		{
			if (holder == null || !_holders.Remove(holder))
			{
				return;
			}

			// Queued turns and skill chains may resume after a completion callback.
			// Unlock only on a later editor update after all sessions have become idle.
			_idleUpdates = 0;
		}

		private static void UpdateHandler()
		{
			if (_holders.Count > 0 || Sessions.ChatTabInstaller.HasActiveWork)
			{
				_idleUpdates = 0;
				return;
			}

			if (_locked && ++_idleUpdates >= 2)
			{
				ReleaseAll();
			}
		}

		private static void ReleaseAll()
		{
			if (_locked)
			{
				_locked = false;
				EditorApplication.UnlockReloadAssemblies();
			}

			_holders.Clear();
		}
	}
}
