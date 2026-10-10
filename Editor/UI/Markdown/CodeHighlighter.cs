using System;
using System.Text;
using System.Text.RegularExpressions;

namespace DTech.Parley.Editor.UI
{
    internal static class CodeHighlighter
    {
        private const int MaxHighlightChars = 60000;

        private static readonly Regex _cSharp = new (
            "(?<comment>//[^\\n]*|/\\*[\\s\\S]*?\\*/)|(?<string>@?\\$?\"(?:\\\\.|[^\"\\\\])*\"|'(?:\\\\.|[^'\\\\])')|(?<number>\\b\\d+(\\.\\d+)?[fFdDmMlL]?\\b)|(?<keyword>\\b(?:"
            + "abstract|as|async|await|base|bool|break|byte|case|catch|char|class|const|continue|decimal|default|delegate|do|double|else|enum|event|explicit|extern|false|finally|"
            + "fixed|float|for|foreach|get|goto|if|implicit|in|int|interface|internal|is|lock|long|namespace|new|null|object|operator|out|override|params|private|protected|public|"
            + "readonly|ref|return|sbyte|sealed|set|short|sizeof|static|string|struct|switch|this|throw|true|try|typeof|uint|ulong|unchecked|unsafe|ushort|using|var|virtual|void|"
            + "volatile|while|yield|record|init|nameof|when|where)\\b)");

        private static readonly Regex _json = new ("(?<key>\"(?:\\\\.|[^\"\\\\])*\"(?=\\s*:))|(?<string>\"(?:\\\\.|[^\"\\\\])*\")|(?<number>-?\\b\\d+(\\.\\d+)?([eE][+-]?\\d+)?\\b)|(?<keyword>\\b(?:true|false|null)\\b)");
        private static readonly Regex _shell = new ("(?<comment>#[^\\n]*)|(?<string>\"(?:\\\\.|[^\"\\\\])*\"|'[^']*')|(?<keyword>\\b(?:if|then|else|fi|for|do|done|while|case|esac|function|export|cd|echo|sudo)\\b)|(?<number>\\$\\w+|\\$\\{[^}]+\\})");
        private static readonly Regex _generic = new ("(?<comment>//[^\\n]*|#[^\\n]*|/\\*[\\s\\S]*?\\*/)|(?<string>\"(?:\\\\.|[^\"\\\\])*\"|'(?:\\\\.|[^'\\\\])*')|(?<number>\\b\\d+(\\.\\d+)?\\b)");

        private static readonly (string group, string dark, string light)[] _palette =
        {
            ("comment", "#7D8590", "#6E7781"),
            ("key", "#79C0FF", "#0550AE"),
            ("string", "#A5D6FF", "#0A3069"),
            ("number", "#FFA657", "#953800"),
            ("keyword", "#FF7B72", "#CF222E"),
        };

        public static string Highlight(string code, string language)
        {
            if (string.IsNullOrEmpty(code))
            {
                return string.Empty;
            }

            if (code.Length > MaxHighlightChars)
            {
                return InlineMarkdown.Escape(code);
            }

            string normalized = (language ?? string.Empty).ToLowerInvariant();
            if (normalized == "diff" || normalized == "patch")
            {
                return HighlightDiff(code);
            }

            Regex regex = normalized switch
            {
                "cs" or "csharp" or "c#" or "java" or "ts" or "typescript" or "js" or "javascript" or "cpp" or "c" or "hlsl" or "shader" or "kotlin" or "swift" => _cSharp,
                "json" or "jsonc" => _json,
                "sh" or "bash" or "zsh" or "shell" or "console" or "powershell" or "ps1" => _shell,
                _ => _generic,
            };

            return Colorize(code, regex);
        }

        private static string Colorize(string code, Regex regex)
        {
            bool dark = ParleyStyles.IsDark;
            StringBuilder builder = new StringBuilder(code.Length + 64);
            int position = 0;
            foreach (Match match in regex.Matches(code))
            {
                if (match.Index > position)
                {
                    builder.Append(InlineMarkdown.Escape(code.Substring(position, match.Index - position)));
                }

                string color = null;
                foreach ((string group, string darkColor, string lightColor) in _palette)
                {
                    if (match.Groups[group].Success)
                    {
                        color = dark ? darkColor : lightColor;
                        break;
                    }
                }

                AppendColored(builder, InlineMarkdown.Escape(match.Value), color);
                position = match.Index + match.Length;
            }

            if (position < code.Length)
            {
                builder.Append(InlineMarkdown.Escape(code.Substring(position)));
            }

            return builder.ToString();
        }

        private static string HighlightDiff(string code)
        {
            bool dark = ParleyStyles.IsDark;
            StringBuilder builder = new StringBuilder();
            string[] lines = code.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string color = line.StartsWith("+", StringComparison.Ordinal) ? dark ? "#7EE787" : "#1A7F37"
                    : line.StartsWith("-", StringComparison.Ordinal) ? dark ? "#FF7B72" : "#CF222E"
                    : line.StartsWith("@@", StringComparison.Ordinal) ? dark ? "#79C0FF" : "#0550AE"
                    : null;
                AppendColored(builder, InlineMarkdown.Escape(line), color);
                if (i < lines.Length - 1)
                {
                    builder.Append('\n');
                }
            }

            return builder.ToString();
        }

        private static void AppendColored(StringBuilder builder, string escaped, string color)
        {
            if (color == null)
            {
                builder.Append(escaped);
                return;
            }

            builder.Append("<color=").Append(color).Append('>').Append(escaped).Append("</color>");
        }
    }
}