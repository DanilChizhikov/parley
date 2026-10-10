using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEditor;

namespace DTech.Parley.Editor
{
	[InitializeOnLoad]
	internal static class ProcessJanitor
	{
		private const string Key = "DTech.Parley.ChildProcesses";
		private const char EntrySeparator = ',';
		private const char NameSeparator = ':';

		static ProcessJanitor()
		{
			EditorApplication.delayCall += KillOrphans;
		}

		public static void Track(int processId, string processName)
		{
			string name = Sanitize(processName);
			if (name.Length == 0)
			{
				return;
			}

			List<string> entries = Load();
			string entry = processId.ToString() + NameSeparator + name;
			if (!entries.Contains(entry))
			{
				entries.Add(entry);
				Store(entries);
			}
		}

		public static void Untrack(int processId)
		{
			string prefix = processId.ToString() + NameSeparator;
			List<string> entries = Load();
			if (entries.RemoveAll(entry => entry.StartsWith(prefix, StringComparison.Ordinal)) > 0)
			{
				Store(entries);
			}
		}

		private static void KillOrphans()
		{
			foreach (string entry in Load())
			{
				int separator = entry.IndexOf(NameSeparator);
				if (separator > 0 && int.TryParse(entry.Substring(0, separator), out int processId))
				{
					KillOrphan(processId, entry.Substring(separator + 1));
				}
			}

			Store(new List<string>());
		}

		private static void KillOrphan(int processId, string processName)
		{
			Process process;
			try
			{
				process = Process.GetProcessById(processId);
			}
			catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
			{
				return;
			}

			using (process)
			{
				try
				{
					if (!process.HasExited && string.Equals(Sanitize(process.ProcessName), processName, StringComparison.OrdinalIgnoreCase))
					{
						ProcessTree.Kill(process);
					}
				}
				catch (InvalidOperationException)
				{
				}
			}
		}

		private static string Sanitize(string processName)
		{
			return (processName ?? string.Empty).Replace(EntrySeparator.ToString(), string.Empty).Trim();
		}

		private static List<string> Load()
		{
			List<string> entries = new ();
			foreach (string part in SessionState.GetString(Key, string.Empty).Split(EntrySeparator))
			{
				if (part.Length > 0)
				{
					entries.Add(part);
				}
			}

			return entries;
		}

		private static void Store(List<string> entries)
		{
			SessionState.SetString(Key, string.Join(EntrySeparator.ToString(), entries));
		}
	}
}
