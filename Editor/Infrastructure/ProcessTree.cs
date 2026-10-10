using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Debug = UnityEngine.Debug;

namespace DTech.Parley.Editor
{
    internal static class ProcessTree
    {
        private const int CommandTimeoutMs = 3000;
        private const int MaxDescendants = 256;

        public static void Kill(Process process)
        {
            int processId;
            try
            {
                if (process == null || process.HasExited)
                {
                    return;
                }

                processId = process.Id;
            }
            catch (InvalidOperationException)
            {
                return;
            }

            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                Run("taskkill", "/T /F /PID " + processId);
            }
            else
            {
                List<int> descendants = new ();
                CollectDescendants(processId, descendants);
                if (descendants.Count > 0)
                {
                    Run("/bin/kill", "-TERM " + string.Join(" ", descendants));
                }
            }

            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                }
            }
            catch (InvalidOperationException)
            {
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Parley] Could not stop process " + processId + ": " + exception.Message);
            }
        }

        private static void CollectDescendants(int processId, List<int> descendants)
        {
            string output = Run("/usr/bin/pgrep", "-P " + processId);
            if (string.IsNullOrEmpty(output))
            {
                return;
            }

            foreach (string line in output.Split('\n'))
            {
                if (descendants.Count >= MaxDescendants)
                {
                    return;
                }

                if (int.TryParse(line.Trim(), out int child) && !descendants.Contains(child))
                {
                    descendants.Add(child);
                    CollectDescendants(child, descendants);
                }
            }
        }

        private static string Run(string fileName, string arguments)
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo(fileName, arguments)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };

                using Process process = Process.Start(startInfo);
                if (process == null)
                {
                    return null;
                }

                Task<string> output = process.StandardOutput.ReadToEndAsync();
                process.StandardError.ReadToEndAsync();
                return process.WaitForExit(CommandTimeoutMs) ? output.Result : null;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Parley] " + fileName + " failed: " + exception.Message);
                return null;
            }
        }
    }
}
