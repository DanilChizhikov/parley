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
        private const string PsTool = "/bin/ps";
        private const string PsArguments = "-A -o pid=,ppid=";

        private static readonly char[] ColumnSeparators = { ' ', '\t', '\r' };

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
                List<int> descendants = Descendants(Run(PsTool, PsArguments), processId);
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

        internal static List<int> Descendants(string psOutput, int processId)
        {
            List<int> descendants = new ();
            if (string.IsNullOrEmpty(psOutput))
            {
                return descendants;
            }

            Dictionary<int, List<int>> children = new ();
            foreach (string line in psOutput.Split('\n'))
            {
                string[] columns = line.Split(ColumnSeparators, StringSplitOptions.RemoveEmptyEntries);
                if (columns.Length < 2 || !int.TryParse(columns[0], out int child) || !int.TryParse(columns[1], out int parent))
                {
                    continue;
                }

                if (!children.TryGetValue(parent, out List<int> siblings))
                {
                    siblings = new List<int>();
                    children[parent] = siblings;
                }

                siblings.Add(child);
            }

            Queue<int> pending = new ();
            pending.Enqueue(processId);
            while (pending.Count > 0 && descendants.Count < MaxDescendants)
            {
                if (!children.TryGetValue(pending.Dequeue(), out List<int> siblings))
                {
                    continue;
                }

                foreach (int child in siblings)
                {
                    if (descendants.Count >= MaxDescendants)
                    {
                        break;
                    }

                    if (child != processId && !descendants.Contains(child))
                    {
                        descendants.Add(child);
                        pending.Enqueue(child);
                    }
                }
            }

            return descendants;
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
