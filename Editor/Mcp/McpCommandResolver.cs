using System;
using System.IO;

namespace DTech.Parley.Editor.Mcp
{
    internal static class McpCommandResolver
    {
        private static readonly string[] WindowsExtensions = { ".exe", ".cmd", ".bat" };

        public static string Resolve(string command, string searchPath)
        {
            if (string.IsNullOrEmpty(command))
            {
                return command;
            }

            if (Path.IsPathRooted(command) || command.IndexOf('/') >= 0 || command.IndexOf('\\') >= 0)
            {
                return ProjectPaths.Resolve(command);
            }

            string[] directories = (searchPath ?? string.Empty).Split(new[] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string directory in directories)
            {
                string found = Find(directory, command);
                if (found != null)
                {
                    return found;
                }
            }

            return command;
        }

        private static string Find(string directory, string command)
        {
            string candidate = Path.Combine(directory, command);
            if (!CommandLine.IsWindows)
            {
                return File.Exists(candidate) ? candidate : null;
            }

            if (Path.HasExtension(command) && File.Exists(candidate))
            {
                return candidate;
            }

            foreach (string extension in WindowsExtensions)
            {
                if (File.Exists(candidate + extension))
                {
                    return candidate + extension;
                }
            }

            return null;
        }
    }
}
