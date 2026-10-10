using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Debug = UnityEngine.Debug;

namespace DTech.Parley.Editor.Agents.Codex
{
    internal static class CodexEnvironment
    {
        private const string ConfigOption = "-c";
        private const string EphemeralCredentials = "cli_auth_credentials_store=ephemeral";
        private const string CredentialsFile = "auth.json";

        private static readonly string[] InheritedVariables = { "CODEX_SANDBOX", "CODEX_SANDBOX_NETWORK_DISABLED", "CODEX_THREAD_ID" };
        private static readonly Version EphemeralCredentialsVersion = new (0, 162, 0);

        public static bool UsesManagedHome(ParleyProfile profile)
        {
            return string.IsNullOrWhiteSpace(profile.ConfigDir) && profile.CodexAuthMethod == CodexAuthMethod.ApiKey;
        }

        public static string Home(ParleyProfile profile)
        {
            if (!string.IsNullOrWhiteSpace(profile.ConfigDir))
            {
                return ProjectPaths.Resolve(profile.ConfigDir);
            }

            if (!UsesManagedHome(profile))
            {
                return null;
            }

            string home = Path.Combine(ProjectPaths.LibraryFolder, "Codex", profile.Id);
            Directory.CreateDirectory(home);
            return home;
        }

        public static string ManagedCredentialsPath(ParleyProfile profile)
        {
            return UsesManagedHome(profile) ? Path.Combine(Home(profile), CredentialsFile) : null;
        }

        public static void AddCredentialOptions(List<string> arguments, ParleyProfile profile, Version cliVersion)
        {
            if (UsesManagedHome(profile) && cliVersion != null && cliVersion >= EphemeralCredentialsVersion)
            {
                arguments.Add(ConfigOption);
                arguments.Add(EphemeralCredentials);
            }
        }

        public static void DeleteCredentials(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            try
            {
                File.Delete(path);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Debug.LogWarning("[Parley] Could not delete " + path + ": " + exception.Message);
            }
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
