using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace DTech.Parley.Editor.Agents.Claude
{
	internal static class ClaudeCliLocator
	{
		public static readonly Version MinimumVersion = new (2, 1, 285);

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

			return _cached = await CliVersions.LocateNewestAsync(Candidates(ProjectPaths.HomeFolder, CommandLine.IsWindows), "claude");
		}

		public static Task<string> GetVersionAsync(string executable, string path)
		{
			return CliVersions.GetVersionAsync(executable, path);
		}

		public static bool IsOutdated(Version version)
		{
			return version != null && version < MinimumVersion;
		}
	}
}
