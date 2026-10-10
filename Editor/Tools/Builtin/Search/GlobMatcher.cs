using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace DTech.Parley.Editor.Tools.Builtin
{
    internal static class GlobMatcher
    {
        private static readonly HashSet<string> SkippedEverywhere = new (StringComparer.OrdinalIgnoreCase)
        {
            ".git", "node_modules", ".vs", ".idea",
        };

        private static readonly HashSet<string> SkippedAtProjectRoot = new (StringComparer.OrdinalIgnoreCase)
        {
            "Library", "Temp", "Logs", "obj", "UserSettings",
        };

        public static Regex ToRegex(string pattern, bool ignoreCase)
        {
            StringBuilder builder = new StringBuilder("^");
            int i = 0;
            while (i < pattern.Length)
            {
                char character = pattern[i];
                if (character == '*')
                {
                    bool doubleStar = i + 1 < pattern.Length && pattern[i + 1] == '*';
                    if (doubleStar)
                    {
                        bool slash = i + 2 < pattern.Length && pattern[i + 2] == '/';
                        builder.Append(slash ? "(?:.*/)?" : ".*");
                        i += slash ? 3 : 2;
                        continue;
                    }

                    builder.Append("[^/]*");
                }
                else if (character == '?')
                {
                    builder.Append("[^/]");
                }
                else if (character == '{')
                {
                    int close = pattern.IndexOf('}', i);
                    if (close < 0)
                    {
                        builder.Append("\\{");
                    }
                    else
                    {
                        string[] options = pattern.Substring(i + 1, close - i - 1).Split(',');
                        builder.Append("(?:");
                        for (int option = 0; option < options.Length; option++)
                        {
                            builder.Append(option > 0 ? "|" : string.Empty).Append(ToRegex(options[option], ignoreCase).ToString().Trim('^', '$'));
                        }

                        builder.Append(')');
                        i = close;
                    }
                }
                else if (character == '[')
                {
                    int close = pattern.IndexOf(']', i + 1);
                    if (close < 0)
                    {
                        builder.Append("\\[");
                    }
                    else
                    {
                        builder.Append(pattern, i, close - i + 1);
                        i = close;
                    }
                }
                else
                {
                    builder.Append(Regex.Escape(character.ToString()));
                }

                i++;
            }

            builder.Append('$');
            return new Regex(builder.ToString(), ignoreCase ? RegexOptions.IgnoreCase | RegexOptions.CultureInvariant : RegexOptions.CultureInvariant);
        }

        public static IEnumerable<string> EnumerateFiles(string root, string projectRoot, CancellationToken cancellationToken)
        {
            string normalizedProjectRoot = NormalizeFolder(projectRoot);
            Stack<string> pending = new Stack<string>();
            pending.Push(root);
            while (pending.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string folder = pending.Pop();
                string[] files;
                string[] folders;
                try
                {
                    files = Directory.GetFiles(folder);
                    folders = Directory.GetDirectories(folder);
                }
                catch (Exception)
                {
                    continue;
                }

                foreach (string file in files)
                {
                    yield return file;
                }

                bool atProjectRoot = string.Equals(NormalizeFolder(folder), normalizedProjectRoot, StringComparison.OrdinalIgnoreCase);
                foreach (string child in folders)
                {
                    string name = Path.GetFileName(child);
                    if (!SkippedEverywhere.Contains(name) && !(atProjectRoot && SkippedAtProjectRoot.Contains(name)))
                    {
                        pending.Push(child);
                    }
                }
            }
        }

        public static string Relative(string root, string path)
        {
            string relative = path.Length > root.Length ? path.Substring(root.Length).TrimStart('/', '\\') : Path.GetFileName(path);
            return relative.Replace('\\', '/');
        }

        private static string NormalizeFolder(string folder)
        {
            return string.IsNullOrEmpty(folder) ? string.Empty : Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }
}