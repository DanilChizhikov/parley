using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DTech.Parley.Editor
{
	internal static class CliVersions
	{
		private static readonly Regex VersionPattern = new ("\\d+\\.\\d+\\.\\d+");
		private static readonly Dictionary<string, Version> ParsedVersions = new ();

		public static async Task<string> LocateNewestAsync(IEnumerable<string> candidates, string executableName)
		{
			List<string> paths = new ();
			foreach (string candidate in candidates)
			{
				AddExisting(paths, candidate);
			}

			AddExisting(paths, await ShellEnvironment.WhichAsync(executableName));
			if (paths.Count <= 1)
			{
				return paths.Count == 0 ? null : paths[0];
			}

			string loginPath = await ShellEnvironment.GetLoginPathAsync();
			Task<Version>[] versions = new Task<Version>[paths.Count];
			for (int i = 0; i < paths.Count; i++)
			{
				versions[i] = GetParsedVersionAsync(paths[i], ShellEnvironment.Merge(Path.GetDirectoryName(paths[i]), loginPath));
			}

			await Task.WhenAll(versions);
			List<CliCandidate> found = new (paths.Count);
			for (int i = 0; i < paths.Count; i++)
			{
				found.Add(new CliCandidate(paths[i], versions[i].Result));
			}

			return PickNewest(found);
		}

		public static string PickNewest(IReadOnlyList<CliCandidate> candidates)
		{
			if (candidates.Count == 0)
			{
				return null;
			}

			CliCandidate best = candidates[0];
			for (int i = 1; i < candidates.Count; i++)
			{
				Version version = candidates[i].Version;
				if (version != null && (best.Version == null || version > best.Version))
				{
					best = candidates[i];
				}
			}

			return best.Path;
		}

		public static Task<string> GetVersionAsync(string executable, string path)
		{
			return Task.Run(() => ReadVersion(executable, path));
		}

		public static async Task<Version> GetParsedVersionAsync(string executable, string path)
		{
			string key = executable + "|" + File.GetLastWriteTimeUtc(executable).Ticks;
			lock (ParsedVersions)
			{
				if (ParsedVersions.TryGetValue(key, out Version cached))
				{
					return cached;
				}
			}

			Version version = ParseVersion(await GetVersionAsync(executable, path));
			if (version != null)
			{
				lock (ParsedVersions)
				{
					ParsedVersions[key] = version;
				}
			}

			return version;
		}

		public static Version ParseVersion(string output)
		{
			Match match = VersionPattern.Match(output ?? string.Empty);
			return match.Success && Version.TryParse(match.Value, out Version version) ? version : null;
		}

		private static void AddExisting(List<string> paths, string candidate)
		{
			if (!string.IsNullOrEmpty(candidate) && File.Exists(candidate) && !paths.Contains(candidate))
			{
				paths.Add(candidate);
			}
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

				ProcessReaper.Track(process);
				try
				{
					Task<string> output = process.StandardOutput.ReadToEndAsync();
					process.StandardError.ReadToEndAsync();
					if (!process.WaitForExit(10000))
					{
						ProcessTree.Kill(process);
						return null;
					}

					return output.Result.Trim();
				}
				finally
				{
					ProcessReaper.Untrack(process);
				}
			}
			catch (Exception)
			{
				return null;
			}
		}
	}
}
