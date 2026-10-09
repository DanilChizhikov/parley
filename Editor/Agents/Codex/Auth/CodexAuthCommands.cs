using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace DTech.Parley.Editor.Agents.Codex
{
    internal static class CodexAuthCommands
    {
        public static List<string> LoginArguments(bool deviceCode)
        {
            List<string> arguments = new () { "login" };
            if (deviceCode)
            {
                arguments.Add("--device-auth");
            }

            return arguments;
        }

        public static async Task<string> BuildTerminalCommandAsync(ParleyProfile profile, List<string> arguments)
        {
            string executable = await CodexCliLocator.LocateAsync(ParleyUserSettings.instance.CodexCliPathOverride);
            if (executable == null)
            {
                return null;
            }

            StringBuilder builder = new StringBuilder();
            string home = CodexEnvironment.Home(profile);
            if (home != null)
            {
                builder.Append(CommandLine.IsWindows ? "set \"CODEX_HOME=" + home + "\" && " : "CODEX_HOME=" + CommandLine.QuoteUnix(home) + " ");
            }

            List<string> all = new () { executable };
            all.AddRange(arguments);
            builder.Append(CommandLine.Join(all));
            return builder.ToString();
        }

        public static async Task<CodexAuthStatus> GetStatusAsync(ParleyProfile profile)
        {
            string executable = await CodexCliLocator.LocateAsync(ParleyUserSettings.instance.CodexCliPathOverride);
            if (executable == null)
            {
                return new CodexAuthStatus { Error = "Codex CLI not found." };
            }

            string path = await ShellEnvironment.GetLoginPathAsync();
            ProcessStartInfo startInfo = new ProcessStartInfo(executable, CommandLine.Join(new[] { "login", "status" }))
            {
                WorkingDirectory = ProjectPaths.Root,
            };

            string environmentPath = ShellEnvironment.Merge(Path.GetDirectoryName(executable), path);
            CodexEnvironment.Apply(startInfo, profile, environmentPath);
            return await Task.Run(() => RunStatus(startInfo));
        }

        private static CodexAuthStatus RunStatus(ProcessStartInfo startInfo)
        {
            try
            {
                startInfo.UseShellExecute = false;
                startInfo.RedirectStandardOutput = true;
                startInfo.RedirectStandardError = true;
                startInfo.RedirectStandardInput = true;
                startInfo.CreateNoWindow = true;
                startInfo.StandardOutputEncoding = Encoding.UTF8;
                using Process process = Process.Start(startInfo);
                if (process == null)
                {
                    return new CodexAuthStatus { Error = "Failed to run codex login status." };
                }

                process.StandardInput.Close();
                Task<string> errorTask = process.StandardError.ReadToEndAsync();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(15000);
                string text = (output + "\n" + errorTask.Result).Trim();
                return new CodexAuthStatus { LoggedIn = process.ExitCode == 0, Text = text };
            }
            catch (Exception exception)
            {
                return new CodexAuthStatus { Error = exception.Message };
            }
        }
    }
}