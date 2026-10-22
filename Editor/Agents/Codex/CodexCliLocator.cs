using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace DTech.Parley.Editor.Agents.Codex
{
    internal static class CodexCliLocator
    {
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

            return _cached = await CliVersions.LocateNewestAsync(Candidates(ProjectPaths.HomeFolder, CommandLine.IsWindows), "codex");
        }

        public static Task<string> GetVersionAsync(string executable, string path)
        {
            return CliVersions.GetVersionAsync(executable, path);
        }
    }
}
