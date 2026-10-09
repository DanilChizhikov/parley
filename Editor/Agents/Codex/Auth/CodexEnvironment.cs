using System.Diagnostics;
using System.IO;

namespace DTech.Parley.Editor.Agents.Codex
{
    internal static class CodexEnvironment
    {
        private static readonly string[] InheritedVariables = { "CODEX_SANDBOX", "CODEX_SANDBOX_NETWORK_DISABLED", "CODEX_THREAD_ID" };

        public static string Home(ParleyProfile profile)
        {
            if (!string.IsNullOrWhiteSpace(profile.ConfigDir))
            {
                return ProjectPaths.Resolve(profile.ConfigDir);
            }

            if (profile.CodexAuthMethod != CodexAuthMethod.ApiKey)
            {
                return null;
            }

            string home = Path.Combine(ProjectPaths.LibraryFolder, "Codex", profile.Id);
            Directory.CreateDirectory(home);
            return home;
        }

        public static void Apply(ProcessStartInfo startInfo, ParleyProfile profile, string path)
        {
            startInfo.Environment["PATH"] = path;
            foreach (string variable in InheritedVariables)
            {
                startInfo.Environment.Remove(variable);
            }

            string home = Home(profile);
            if (home != null)
            {
                startInfo.Environment["CODEX_HOME"] = home;
            }
        }
    }
}