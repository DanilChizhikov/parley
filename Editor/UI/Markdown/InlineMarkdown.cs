using System;
using System.Text;
using System.Text.RegularExpressions;

namespace DTech.Parley.Editor.UI
{
    internal static class InlineMarkdown
    {
        private const int MaxDepth = 6;

        private static readonly Regex _pathLike = new ("^[\\w./\\\\-]+\\.[A-Za-z0-9]{1,8}(:\\d+)?$");

        public static string Escape(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('<') < 0)
            {
                return text ?? string.Empty;
            }

            return text.Replace("<", "<noparse><</noparse>");
        }

        public static string ToRichText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            RichTextWriter writer = new RichTextWriter(text.Length + 16);
            writer.Render(text, 0);
            return writer.ToString();
        }

        private static bool Starts(string text, int index, string value)
        {
            return string.CompareOrdinal(text, index, value, 0, value.Length) == 0;
        }

        private static bool IsEscape(string text, int index)
        {
            return text[index] == '\\' && index + 1 < text.Length && (char.IsPunctuation(text[index + 1]) || char.IsSymbol(text[index + 1]));
        }

        private static int CountBackticks(string text, int index)
        {
            int count = 0;
            while (index + count < text.Length && text[index + count] == '`')
            {
                count++;
            }

            return count;
        }

        private static int FindBackticks(string text, int start, int length)
        {
            for (int i = start; i < text.Length; i++)
            {
                if (text[i] != '`')
                {
                    continue;
                }

                int run = CountBackticks(text, i);
                if (run == length)
                {
                    return i;
                }

                i += run - 1;
            }

            return -1;
        }

        private static bool IsEmphasisOpen(string text, int index)
        {
            if (index + 1 >= text.Length || char.IsWhiteSpace(text[index + 1]))
            {
                return false;
            }

            return text[index] == '*' || index == 0 || !char.IsLetterOrDigit(text[index - 1]);
        }

        private static int FindEmphasisClose(string text, int start, char marker)
        {
            for (int i = start; i < text.Length; i++)
            {
                if (text[i] == '\n')
                {
                    return -1;
                }

                if (text[i] != marker || char.IsWhiteSpace(text[i - 1]))
                {
                    continue;
                }

                if (i + 1 < text.Length && text[i + 1] == marker)
                {
                    i++;
                    continue;
                }

                if (marker == '_' && i + 1 < text.Length && char.IsLetterOrDigit(text[i + 1]))
                {
                    continue;
                }

                return i;
            }

            return -1;
        }

        private static bool IsUrlStart(string text, int index)
        {
            return (Starts(text, index, "https://") || Starts(text, index, "http://")) && (index == 0 || !char.IsLetterOrDigit(text[index - 1]));
        }

        private static int FindUrlEnd(string text, int start)
        {
            int end = start;
            while (end < text.Length && !char.IsWhiteSpace(text[end]) && text[end] != ')' && text[end] != '>' && text[end] != '"')
            {
                end++;
            }

            while (end > start && (text[end - 1] == '.' || text[end - 1] == ',' || text[end - 1] == ';' || text[end - 1] == ':'))
            {
                end--;
            }

            return end;
        }

        private sealed class RichTextWriter
        {
            private readonly StringBuilder _builder;
            private readonly StringBuilder _plain = new ();

            public RichTextWriter(int capacity)
            {
                _builder = new StringBuilder(capacity);
            }

            public override string ToString()
            {
                return _builder.ToString();
            }

            public void Render(string text, int depth)
            {
                bool nested = depth < MaxDepth;
                int i = 0;
                while (i < text.Length)
                {
                    char character = text[i];
                    if (IsEscape(text, i))
                    {
                        _plain.Append(text[i + 1]);
                        i += 2;
                        continue;
                    }

                    if (character == '`')
                    {
                        int run = CountBackticks(text, i);
                        int close = FindBackticks(text, i + run, run);
                        if (close > 0)
                        {
                            AppendCode(text.Substring(i + run, close - i - run).Trim());
                            i = close + run;
                            continue;
                        }
                    }

                    if (nested && (Starts(text, i, "**") || Starts(text, i, "__")))
                    {
                        int close = text.IndexOf(text.Substring(i, 2), i + 2, StringComparison.Ordinal);
                        if (close > i + 2)
                        {
                            AppendWrapped("b", text.Substring(i + 2, close - i - 2), depth);
                            i = close + 2;
                            continue;
                        }
                    }

                    if (nested && Starts(text, i, "~~"))
                    {
                        int close = text.IndexOf("~~", i + 2, StringComparison.Ordinal);
                        if (close > i + 2)
                        {
                            AppendWrapped("s", text.Substring(i + 2, close - i - 2), depth);
                            i = close + 2;
                            continue;
                        }
                    }

                    if (nested && (character == '*' || character == '_') && IsEmphasisOpen(text, i))
                    {
                        int close = FindEmphasisClose(text, i + 1, character);
                        if (close > i + 1)
                        {
                            AppendWrapped("i", text.Substring(i + 1, close - i - 1), depth);
                            i = close + 1;
                            continue;
                        }
                    }

                    if (character == '[')
                    {
                        int labelEnd = text.IndexOf("](", i + 1, StringComparison.Ordinal);
                        int urlEnd = labelEnd > 0 ? text.IndexOf(')', labelEnd + 2) : -1;
                        if (labelEnd > i && urlEnd > labelEnd && text.IndexOf('\n', i, urlEnd - i) < 0)
                        {
                            string url = text.Substring(labelEnd + 2, urlEnd - labelEnd - 2).Trim();
                            AppendLink(url, text.Substring(i + 1, labelEnd - i - 1), depth);
                            i = urlEnd + 1;
                            continue;
                        }
                    }

                    if (IsUrlStart(text, i))
                    {
                        int end = FindUrlEnd(text, i);
                        AppendUrl(text.Substring(i, end - i));
                        i = end;
                        continue;
                    }

                    _plain.Append(character);
                    i++;
                }

                FlushPlain();
            }

            private void FlushPlain()
            {
                if (_plain.Length == 0)
                {
                    return;
                }

                _builder.Append(Escape(_plain.ToString()));
                _plain.Clear();
            }

            private void AppendWrapped(string tag, string inner, int depth)
            {
                FlushPlain();
                _builder.Append('<').Append(tag).Append('>');
                Render(inner, depth + 1);
                _builder.Append("</").Append(tag).Append('>');
            }

            private void AppendCode(string code)
            {
                FlushPlain();
                bool isPath = _pathLike.IsMatch(code) && (code.Contains("/") || code.Contains("\\") || code.Contains(".cs") || code.Contains(":"));
                if (isPath)
                {
                    _builder.Append("<link=\"").Append(LinkHandler.FileScheme).Append(code.Replace("\"", string.Empty)).Append("\">");
                }

                _builder.Append("<mark=").Append(ParleyStyles.MarkColor).Append("><color=").Append(ParleyStyles.CodeColor).Append('>')
                    .Append(Escape(code)).Append("</color></mark>");
                if (isPath)
                {
                    _builder.Append("</link>");
                }
            }

            private void AppendLink(string url, string label, int depth)
            {
                FlushPlain();
                OpenLink(url);
                Render(label, depth + 1);
                CloseLink();
            }

            private void AppendUrl(string url)
            {
                FlushPlain();
                OpenLink(url);
                _builder.Append(Escape(url));
                CloseLink();
            }

            private void OpenLink(string url)
            {
                _builder.Append("<link=\"").Append(url.Replace("\"", "%22")).Append("\"><color=").Append(ParleyStyles.LinkColor).Append("><u>");
            }

            private void CloseLink()
            {
                _builder.Append("</u></color></link>");
            }
        }
    }
}