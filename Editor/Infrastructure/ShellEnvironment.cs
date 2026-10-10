using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace DTech.Parley.Editor
{
	internal static class ShellEnvironment
	{
		private const string Marker = "__PARLEY_VALUE__";
		private const string PathCacheKey = "DTech.Parley.LoginShellPath";
		private const int TimeoutMs = 8000;
		private const double RetryCaptureMinutes = 5.0;

		private static string _loginPath;
		private static Task<string> _pendingCapture;
		private static DateTime _retryCaptureAfterUtc;

		public static string LoginShell
		{
			get
			{
				string shell = Environment.GetEnvironmentVariable("SHELL");
				if (!string.IsNullOrEmpty(shell) && File.Exists(shell))
				{
					return shell;
				}

				return File.Exists("/bin/zsh") ? "/bin/zsh" : "/bin/bash";
			}
		}

		public static string CachedLoginPath
		{
			get
			{
				if (_loginPath == null)
				{
					string cached = SessionState.GetString(PathCacheKey, string.Empty);
					_loginPath = cached.Length > 0 ? cached : null;
				}

				return _loginPath;
			}
		}

		public static Task<string> GetLoginPathAsync()
		{
			if (CommandLine.IsWindows)
			{
				return Task.FromResult(Environment.GetEnvironmentVariable("PATH"));
			}

			string cached = CachedLoginPath;
			if (cached != null)
			{
				return Task.FromResult(cached);
			}

			string fallback = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
			if (DateTime.UtcNow < _retryCaptureAfterUtc)
			{
				return Task.FromResult(fallback);
			}

			if (_pendingCapture != null && !_pendingCapture.IsCompleted)
			{
				return _pendingCapture;
			}

			string shell = LoginShell;
			_pendingCapture = Task.Run(() => Capture(shell, "printf '" + Marker + "%s" + Marker + "' \"$PATH\"")).ContinueWith(task =>
			{
				string value = task.Result;
				if (value == null)
				{
					_retryCaptureAfterUtc = DateTime.UtcNow.AddMinutes(RetryCaptureMinutes);
					return fallback;
				}

				string merged = Merge(value, fallback);
				MainThread.Post(() => SessionState.SetString(PathCacheKey, merged));
				_loginPath = merged;
				return merged;
			}, TaskScheduler.Default);

			return _pendingCapture;
		}

		public static Task<string> WhichAsync(string executable)
		{
			if (CommandLine.IsWindows)
			{
				return Task.FromResult<string>(null);
			}

			string shell = LoginShell;
			return Task.Run(() => Capture(shell, "printf '" + Marker + "%s" + Marker + "' \"$(command -v " + executable + ")\""));
		}

		public static string Capture(string shell, string script)
		{
			try
			{
				ProcessStartInfo startInfo = new ProcessStartInfo(shell, CommandLine.Join(new[] { "-ilc", script }, false))
				{
					UseShellExecute = false,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					RedirectStandardInput = true,
					CreateNoWindow = true,
				};
				using Process process = Process.Start(startInfo);
				if (process == null)
				{
					return null;
				}

				process.StandardInput.Close();
				Task<string> output = process.StandardOutput.ReadToEndAsync();
				process.StandardError.ReadToEndAsync();
				if (!process.WaitForExit(TimeoutMs))
				{
					try
					{
						process.Kill();
					}
					catch (Exception exception)
					{
						Debug.LogException(exception);
					}

					return null;
				}

				return Extract(output.Result);
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
				return null;
			}
		}

		public static string Extract(string output)
		{
			if (string.IsNullOrEmpty(output))
			{
				return null;
			}

			int start = output.IndexOf(Marker, StringComparison.Ordinal);
			if (start < 0)
			{
				return null;
			}

			start += Marker.Length;
			int end = output.IndexOf(Marker, start, StringComparison.Ordinal);
			if (end < 0)
			{
				return null;
			}

			string value = output.Substring(start, end - start).Trim();
			return value.Length > 0 ? value : null;
		}

		public static string Merge(string primary, string secondary)
		{
			char separator = Path.PathSeparator;
			string[] first = (primary ?? string.Empty).Split(new[] { separator }, StringSplitOptions.RemoveEmptyEntries);
			string[] second = (secondary ?? string.Empty).Split(new[] { separator }, StringSplitOptions.RemoveEmptyEntries);
			List<string> result = new ();
			foreach (string entry in first)
			{
				if (!result.Contains(entry))
				{
					result.Add(entry);
				}
			}

			foreach (string entry in second)
			{
				if (!result.Contains(entry))
				{
					result.Add(entry);
				}
			}

			return string.Join(separator.ToString(), result);
		}
	}
}