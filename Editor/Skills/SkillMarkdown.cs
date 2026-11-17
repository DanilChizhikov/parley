using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DTech.Parley.Editor.Skills
{
    internal static class SkillMarkdown
    {
        private const string Fence = "---";
        private const string NameKey = "name";
        private const string DescriptionKey = "description";
        private const string ArgumentHintKey = "argument-hint";
        private const string QuotedCharacters = ":#'\"{}[],&*!|>%@`";

        private static readonly string[] PlainKeywords = { "true", "false", "yes", "no", "on", "off", "null", "~" };

        public static SkillDocument Parse(string text)
        {
            SkillDocument document = new SkillDocument();
            string[] lines = (text ?? string.Empty).TrimStart('\uFEFF').Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            int end = FindFrontmatterEnd(lines);
            if (end < 0)
            {
                if (lines.Length > 0 && lines[0].TrimEnd() == Fence)
                {
                    document.Diagnostics.Add("Unclosed YAML frontmatter.");
                }
                document.Body = TrimBody(string.Join("\n", lines));
                return document;
            }

            StringBuilder extra = new StringBuilder();
            HashSet<string> keys = new(StringComparer.Ordinal);
            List<string> block = new ();
            for (int i = 1; i < end; i++)
            {
                if (block.Count > 0 && !IsContinuation(lines[i]))
                {
                    ReadKey(document, block, extra, keys);
                    block.Clear();
                }

                block.Add(lines[i]);
            }

            if (block.Count > 0)
            {
                ReadKey(document, block, extra, keys);
            }

            document.ExtraFrontmatter = extra.ToString();
            document.Body = TrimBody(string.Join("\n", lines, end + 1, lines.Length - end - 1));
            return document;
        }

        public static string Format(SkillDocument document)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(Fence).Append('\n');
            builder.Append(NameKey).Append(": ").Append(Quote(document.Name)).Append('\n');
            builder.Append(DescriptionKey).Append(": ").Append(Quote(document.Description)).Append('\n');
            if (!string.IsNullOrEmpty(document.PreferredProvider))
            {
                builder.Append("preferred_provider: ").Append(Quote(document.PreferredProvider)).Append('\n');
            }
            if (!string.IsNullOrEmpty(document.Model))
            {
                builder.Append("model: ").Append(Quote(document.Model)).Append('\n');
            }
            builder.Append("handoff_mode: ").Append(Quote(document.HandoffMode)).Append('\n');
            builder.Append("allowed_targets: [");
            builder.Append(string.Join(", ", document.AllowedTargets.ConvertAll(Quote)));
            builder.Append("]\n");
            if (!string.IsNullOrWhiteSpace(document.ArgumentHint))
            {
                builder.Append(ArgumentHintKey).Append(": ").Append(Quote(document.ArgumentHint)).Append('\n');
            }

            string extra = (document.ExtraFrontmatter ?? string.Empty).Replace("\r\n", "\n").TrimEnd('\n');
            if (extra.Length > 0)
            {
                builder.Append(extra).Append('\n');
            }

            builder.Append(Fence).Append("\n\n");
            builder.Append(TrimBody((document.Body ?? string.Empty).Replace("\r\n", "\n"))).Append('\n');
            return builder.ToString();
        }

        private static string TrimBody(string body)
        {
            return body.TrimEnd().TrimStart('\n');
        }

        private static int FindFrontmatterEnd(string[] lines)
        {
            if (lines.Length == 0 || lines[0].TrimEnd() != Fence)
            {
                return -1;
            }

            for (int i = 1; i < lines.Length; i++)
            {
                if (lines[i].TrimEnd() == Fence)
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool IsContinuation(string line)
        {
            return line.Length == 0 || line[0] == ' ' || line[0] == '\t'
                || line.StartsWith("- ", StringComparison.Ordinal) || line.TrimStart().StartsWith("#", StringComparison.Ordinal);
        }

        private static void ReadKey(SkillDocument document, List<string> block, StringBuilder extra, HashSet<string> keys)
        {
            string line = block[0];
            int colon = line.IndexOf(':');
            string key = colon > 0 ? line.Substring(0, colon).Trim() : string.Empty;
            if (key != NameKey && key != DescriptionKey && key != ArgumentHintKey
                && key != "preferred_provider" && key != "model" && key != "handoff_mode" && key != "allowed_targets")
            {
                foreach (string part in block)
                {
                    extra.Append(part).Append('\n');
                }

                return;
            }

            if (!keys.Add(key)) document.Diagnostics.Add("Duplicate frontmatter key: " + key + ".");
            string value = ReadValue(line.Substring(colon + 1).Trim(), block);
            switch (key)
            {
                case "preferred_provider":
                    document.PreferredProvider = value;
                    break;
                case "model":
                    document.Model = value;
                    break;
                case "handoff_mode":
                    document.HandoffMode = value;
                    if (value != "auto" && value != "ask_approval")
                    {
                        document.Diagnostics.Add("handoff_mode must be auto or ask_approval.");
                    }
                    break;
                case "allowed_targets":
                    ReadTargets(document, line.Substring(colon + 1).Trim(), block);
                    break;
                case NameKey:
                    document.Name = value;
                    break;
                case DescriptionKey:
                    document.Description = value;
                    break;
                default:
                    document.ArgumentHint = value;
                    break;
            }
        }

        private static string ReadValue(string inline, List<string> block)
        {
            inline = StripComment(inline);
            bool folded = inline.StartsWith("|", StringComparison.Ordinal) || inline.StartsWith(">", StringComparison.Ordinal);
            List<string> parts = new ();
            if (!folded && inline.Length > 0)
            {
                parts.Add(inline);
            }

            for (int i = 1; i < block.Count; i++)
            {
                string part = block[i].Trim();
                if (part.Length > 0 && !part.StartsWith("#", StringComparison.Ordinal))
                {
                    parts.Add(part);
                }
            }

            return Unquote(string.Join(inline.StartsWith("|", StringComparison.Ordinal) ? "\n" : " ", parts));
        }

        private static void ReadTargets(SkillDocument document, string inline, List<string> block)
        {
            inline = StripComment(inline);
            List<string> values = new();
            if (inline.StartsWith("[", StringComparison.Ordinal) && inline.EndsWith("]", StringComparison.Ordinal))
            {
                StringBuilder part = new();
                char quote = '\0';
                string contents = inline.Substring(1, inline.Length - 2);
                for (int i = 0; i < contents.Length; i++)
                {
                    char character = contents[i];
                    if (character == '\\' && quote == '"' && i + 1 < contents.Length)
                    {
                        part.Append(character).Append(contents[++i]);
                        continue;
                    }
                    if (character == quote) quote = '\0';
                    else if (quote == '\0' && (character == '\'' || character == '"')) quote = character;
                    if (character == ',' && quote == '\0')
                    {
                        values.Add(part.ToString());
                        part.Clear();
                    }
                    else part.Append(character);
                }
                if (quote != '\0') document.Diagnostics.Add("Unclosed quote in allowed_targets.");
                if (part.Length > 0) values.Add(part.ToString());
            }
            else if (inline.Length == 0)
            {
                for (int i = 1; i < block.Count; i++)
                {
                    string item = StripComment(block[i].Trim());
                    if (item.Length == 0) continue;
                    if (!item.StartsWith("- ", StringComparison.Ordinal))
                    {
                        document.Diagnostics.Add("allowed_targets must be a YAML sequence.");
                        continue;
                    }
                    values.Add(item.Substring(2));
                }
            }
            else document.Diagnostics.Add("allowed_targets must be a YAML sequence.");

            foreach (string value in values)
            {
                string target = Unquote(value.Trim());
                if (target.Length > 0 && !document.AllowedTargets.Contains(target)) document.AllowedTargets.Add(target);
            }
        }

        private static string StripComment(string value)
        {
            char quote = '\0';
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                if (character == '\\' && quote == '"') { i++; continue; }
                if (character == quote) quote = '\0';
                else if (quote == '\0' && (character == '\'' || character == '"')) quote = character;
                else if (quote == '\0' && character == '#' && (i == 0 || char.IsWhiteSpace(value[i - 1])))
                    return value.Substring(0, i).TrimEnd();
            }
            return value;
        }

        private static string Unquote(string value)
        {
            if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
            {
                return Unescape(value.Substring(1, value.Length - 2));
            }

            if (value.Length >= 2 && value[0] == '\'' && value[value.Length - 1] == '\'')
            {
                return value.Substring(1, value.Length - 2).Replace("''", "'");
            }

            return value;
        }

        private static string Unescape(string value)
        {
            StringBuilder builder = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                if (character != '\\' || i == value.Length - 1)
                {
                    builder.Append(character);
                    continue;
                }

                char next = value[++i];
                builder.Append(next switch
                {
                    'n' => '\n',
                    't' => '\t',
                    _ => next,
                });
            }

            return builder.ToString();
        }

        private static bool IsPlainScalarKeyword(string text)
        {
            foreach (string keyword in PlainKeywords)
            {
                if (string.Equals(text, keyword, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
        }

        private static string Quote(string value)
        {
            string text = (value ?? string.Empty).Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ').Replace('\t', ' ');
            bool quote = text.Length > 0 && (text[0] == ' ' || text[text.Length - 1] == ' ' || text[0] == '-' || text[0] == '?' || IsPlainScalarKeyword(text));
            foreach (char character in text)
            {
                if (quote)
                {
                    break;
                }

                quote = QuotedCharacters.IndexOf(character) >= 0;
            }

            return quote ? "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"" : text;
        }
    }
}
