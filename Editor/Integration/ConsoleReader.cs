using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DTech.Parley.Editor
{
	[InitializeOnLoad]
	internal static class ConsoleReader
	{
		private const int ErrorMask = 1 | 2 | 16 | 64 | 256 | 2048 | 131072 | 1048576 | 2097152 | 4194304;
		private const int WarningMask = 128 | 512 | 4096;
		private const int CompilerMask = 2048 | 4096;
		private const int FallbackCapacity = 500;

		private static readonly LinkedList<ConsoleEntry> Fallback = new ();

		private static Type _entriesType;
		private static Type _entryType;
		private static MethodInfo _start;
		private static MethodInfo _end;
		private static MethodInfo _getEntry;
		private static FieldInfo _message;
		private static FieldInfo _file;
		private static FieldInfo _line;
		private static FieldInfo _mode;
		private static bool _reflectionResolved;

		static ConsoleReader()
		{
			Application.logMessageReceivedThreaded += Capture;
		}

		public static List<ConsoleEntry> Read(int limit, bool includeLogs, bool includeWarnings, bool includeErrors, bool compilerOnly = false)
		{
			List<ConsoleEntry> entries = ReadFromConsole() ?? Snapshot();
			List<ConsoleEntry> result = new ();
			for (int i = entries.Count - 1; i >= 0 && result.Count < limit; i--)
			{
				ConsoleEntry entry = entries[i];
				if (compilerOnly && !entry.IsCompilerMessage)
				{
					continue;
				}

				bool wanted = entry.Severity switch
				{
					ConsoleSeverity.Error => includeErrors,
					ConsoleSeverity.Warning => includeWarnings,
					_ => includeLogs,
				};

				if (wanted)
				{
					result.Add(entry);
				}
			}

			result.Reverse();
			return result;
		}

		private static List<ConsoleEntry> ReadFromConsole()
		{
			if (!ResolveReflection())
			{
				return null;
			}

			List<ConsoleEntry> entries = new ();
			try
			{
				int count = (int)_start.Invoke(null, null);
				object entry = Activator.CreateInstance(_entryType);
				object[] arguments = new object[2];
				for (int i = 0; i < count; i++)
				{
					arguments[0] = i;
					arguments[1] = entry;
					if (!(bool)_getEntry.Invoke(null, arguments))
					{
						continue;
					}

					int mode = (int)_mode.GetValue(entry);
					string text = (string)_message.GetValue(entry) ?? string.Empty;
					int newline = text.IndexOf('\n');
					entries.Add(new ConsoleEntry
					{
						Severity = (mode & ErrorMask) != 0 ? ConsoleSeverity.Error : (mode & WarningMask) != 0 ? ConsoleSeverity.Warning : ConsoleSeverity.Log,
						IsCompilerMessage = (mode & CompilerMask) != 0,
						Message = newline < 0 ? text : text.Substring(0, newline),
						StackTrace = newline < 0 ? string.Empty : text.Substring(newline + 1).Trim(),
						File = (string)_file.GetValue(entry),
						Line = (int)_line.GetValue(entry),
					});
				}
			}
			catch (Exception)
			{
				return null;
			}
			finally
			{
				try
				{
					_end.Invoke(null, null);
				}
				catch (Exception exception)
				{
					Debug.LogException(exception);
				}
			}

			return entries;
		}

		private static bool ResolveReflection()
		{
			if (_reflectionResolved)
			{
				return _getEntry != null;
			}

			_reflectionResolved = true;
			Assembly editor = typeof(EditorWindow).Assembly;
			_entriesType = editor.GetType("UnityEditor.LogEntries");
			_entryType = editor.GetType("UnityEditor.LogEntry");
			if (_entriesType == null || _entryType == null)
			{
				return false;
			}

			const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
			_start = _entriesType.GetMethod("StartGettingEntries", flags);
			_end = _entriesType.GetMethod("EndGettingEntries", flags);
			_getEntry = _entriesType.GetMethod("GetEntryInternal", flags);
			const BindingFlags fieldFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
			_message = _entryType.GetField("message", fieldFlags);
			_file = _entryType.GetField("file", fieldFlags);
			_line = _entryType.GetField("line", fieldFlags);
			_mode = _entryType.GetField("mode", fieldFlags);
			if (_start == null || _end == null || _getEntry == null || _message == null || _file == null || _line == null || _mode == null)
			{
				_getEntry = null;
				return false;
			}

			return true;
		}

		private static List<ConsoleEntry> Snapshot()
		{
			lock (Fallback)
			{
				return new List<ConsoleEntry>(Fallback);
			}
		}

		private static void Capture(string condition, string stackTrace, LogType type)
		{
			ConsoleEntry entry = new ConsoleEntry
			{
				Severity = type == LogType.Warning ? ConsoleSeverity.Warning : type == LogType.Log ? ConsoleSeverity.Log : ConsoleSeverity.Error,
				Message = condition,
				StackTrace = stackTrace,
			};

			lock (Fallback)
			{
				Fallback.AddLast(entry);
				while (Fallback.Count > FallbackCapacity)
				{
					Fallback.RemoveFirst();
				}
			}
		}
	}
}