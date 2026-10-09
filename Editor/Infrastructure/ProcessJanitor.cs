using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace DTech.Parley.Editor
{
	[InitializeOnLoad]
	internal static class ProcessJanitor
	{
		private const string Key = "DTech.Parley.ChildProcesses";

		static ProcessJanitor()
		{
			EditorApplication.delayCall += KillOrphans;
		}

		public static void Track(int processId)
		{
			List<int> ids = Load();
			if (!ids.Contains(processId))
			{
				ids.Add(processId);
				Store(ids);
			}
		}

		public static void Untrack(int processId)
		{
			List<int> ids = Load();
			if (ids.Remove(processId))
			{
				Store(ids);
			}
		}

		private static void KillOrphans()
		{
			foreach (int id in Load())
			{
				try
				{
					using Process process = Process.GetProcessById(id);
					string name = process.ProcessName.ToLowerInvariant();
					if (!process.HasExited && (name.Contains("claude") || name.Contains("node")))
					{
						process.Kill();
					}
				}
				catch (Exception exception)
				{
					Debug.LogException(exception);
				}
			}

			Store(new List<int>());
		}

		private static List<int> Load()
		{
			List<int> ids = new ();
			foreach (string part in SessionState.GetString(Key, string.Empty).Split(','))
			{
				if (int.TryParse(part, out int id))
				{
					ids.Add(id);
				}
			}

			return ids;
		}

		private static void Store(List<int> ids)
		{
			SessionState.SetString(Key, string.Join(",", ids));
		}
	}
}