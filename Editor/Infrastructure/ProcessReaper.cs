using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEditor;

namespace DTech.Parley.Editor
{
	[InitializeOnLoad]
	internal static class ProcessReaper
	{
		private static readonly HashSet<Process> Live = new ();

		static ProcessReaper()
		{
			AssemblyReloadEvents.beforeAssemblyReload += KillAll;
			EditorApplication.quitting += KillAll;
		}

		public static void Track(Process process)
		{
			if (process == null)
			{
				return;
			}

			lock (Live)
			{
				Live.Add(process);
			}
		}

		public static void Untrack(Process process)
		{
			if (process == null)
			{
				return;
			}

			lock (Live)
			{
				Live.Remove(process);
			}
		}

		internal static bool IsTracked(int processId)
		{
			lock (Live)
			{
				foreach (Process process in Live)
				{
					try
					{
						if (process.Id == processId)
						{
							return true;
						}
					}
					catch (InvalidOperationException)
					{
					}
				}
			}

			return false;
		}

		internal static void KillAll()
		{
			List<Process> processes;
			lock (Live)
			{
				processes = new List<Process>(Live);
				Live.Clear();
			}

			foreach (Process process in processes)
			{
				ProcessTree.Kill(process);
			}
		}
	}
}
