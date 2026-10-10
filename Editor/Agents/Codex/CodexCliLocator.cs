using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DTech.Parley.Editor.Agents.Codex
{
    internal static class CodexCliLocator
    {
        private static readonly Regex VersionPattern = new ("\\d+\\.\\d+\\.\\d+");
        private static readonly Dictionary<string, Version> ParsedVersions = new ();

        private static string _cached;

        public static IEnumerable<string> Candidates(string home, bool windows)
        {
            if (windows)
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                yield return Path.Combine(appData, "npm", "codex.cmd");
                yield return Path.Combine(home, ".local", "bin", "codex.exe");
                yield return Path.Combine(localAppData, "Programs", "codex", "codex.exe");
                yield break;
            }

            yield return Path.Combine(home, ".local", "bin", "codex");
            yield return "/opt/homebrew/bin/codex";
            yield return "/usr/local/bin/codex";
            yield return Path.Combine(home, ".npm-global", "bin", "codex");
            yield return Path.Combine(home, ".bun", "bin", "codex");
            yield return Path.Combine(home, ".volta", "bin", "codex");
            yield return "/usr/bin/codex";
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

            string found = await ShellEnvironment.WhichAsync("codex");
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

        public static async Task<Version> GetParsedVersionAsync(string executable, string path)
        {
            string key = executable + "|" + File.GetLastWriteTimeUtc(executable).Ticks;
            if (ParsedVersions.TryGetValue(key, out Version cached))
            {
                return cached;
            }

            Version version = ParseVersion(await GetVersionAsync(executable, path));
            if (version != null)
            {
                ParsedVersions[key] = version;
            }

            return version;
        }

        internal static Version ParseVersion(string output)
        {
            Match match = VersionPattern.Match(output ?? string.Empty);
            return match.Success && Version.TryParse(match.Value, out Version version) ? version : null;
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