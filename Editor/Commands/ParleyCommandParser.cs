using System;
using System.Collections.Generic;

namespace DTech.Parley.Editor.Commands
{
    internal static class ParleyCommandParser
    {
        private const int MaxNameLength = 64;

        public static bool IsValidName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length > MaxNameLength || name[0] == '-')
            {
                return false;
            }

            foreach (char character in name)
            {
                if (!(character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-'))
                {
                    return false;
                }
            }

            return true;
        }

        public static IParleyCommand Match(string text, IReadOnlyList<IParleyCommand> commands, out string arguments)
        {
            arguments = string.Empty;
            string trimmed = (text ?? string.Empty).TrimStart();
            if (trimmed.Length < 2 || trimmed[0] != '/' || commands == null)
            {
                return null;
            }

            int end = 1;
            while (end < trimmed.Length && !char.IsWhiteSpace(trimmed[end]))
            {
                end++;
            }

            string name = trimmed.Substring(1, end - 1);
            IParleyCommand command = Find(commands, name);
            if (command == null)
            {
                return null;
            }

            arguments = trimmed.Substring(end).Trim();
            return command;
        }

        public static IParleyCommand Find(IReadOnlyList<IParleyCommand> commands, string name)
        {
            if (commands == null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            foreach (IParleyCommand command in commands)
            {
                if (string.Equals(command.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return command;
                }
            }

            return null;
        }
    }
}
