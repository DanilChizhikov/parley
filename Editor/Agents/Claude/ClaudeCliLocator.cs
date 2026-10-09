using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace DTech.Parley.Editor.Agents.Claude
{
	internal static class ClaudeCliLocator
	{
		private static string _cached;

		public static IEnumerable<string> Candidates(string home, bool windows)
		{
			if (windows)
			{
				string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
				yield return Path.Combine(home, ".local", "bin", "claude.exe");
				yield return Path.Combine(home, ".claude", "local", "claude.exe");
				yield return Path.Combine(localAppData, "Programs", "claude", "claude.exe");
				yield return Path.Combine(localAppData, "AnthropicClaude", "claude.exe");
				yield break;
			}

			yield return Path.Combine(home, ".local", "bin", "claude");
			yield return "/opt/homebrew/bin/claude";
			yield return "/usr/local/bin/claude";
			yield return Path.Combine(home, ".npm-global", "bin", "claude");
			yield return Path.Combine(home, ".claude", "local", "claude");
			yield return Path.Combine(home, "node_modules", ".bin", "claude");
			yield return Path.Combine(home, ".yarn", "bin", "claude");
			yield return "/usr/bin/claude";
		}

		public static async Task<string> LocateAsync(string overridePath)
		{
			if (!string.IsNullOrEmpty(overridePath))
			{
				string resolved = ProjectPaths.Resolve(overridePath);
				return File.Exists(resolved) ? resolved : null;
			}

			if (_cached != null && File.Exists(_cached))
			{
				return _cached;
			}

			foreach (string candidate in Candidates(ProjectPaths.HomeFolder, CommandLine.IsWindows))
			{
				if (File.Exists(candidate))
				{
					return _cached = candidate;
				}
			}

			string found = await ShellEnvironment.WhichAsync("claude");
			if (!string.IsNullOrEmpty(found) && File.Exists(found))
			{
				return _cached = found;
			}

			return null;
		}

		public static Task<string> GetVersionAsync(string executable, string path)
		{
			return Task.Run(() => ReadVersion(executable, path));
		}

		private static string ReadVersion(string executable, string path)
		{
			try
			{
				ProcessStartInfo startInfo = new ProcessStartInfo(executable, "--version")
				{
					UseShellExecute = false,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					CreateNoWindow = true,
				};

				if (!string.IsNullOrEmpty(path))
				{
					startInfo.Environment["PATH"] = path;
				}

				using Process process = Process.Start(startInfo);
				if (process == null)
				{
					return null;
				}

				string output = process.StandardOutput.ReadToEnd();
				process.WaitForExit(10000);
				return output.Trim();
			}
			catch (Exception)
			{
				return null;
			}
		}
	}
}