using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace DTech.Parley.Editor.UI
{
    internal static class MarkdownParser
    {
        private static readonly Regex _heading = new ("^(#{1,6})\\s+(.*?)\\s*#*\\s*$");
        private static readonly Regex _fence = new ("^\\s{0,3}(```+|~~~+)\\s*([^`\\s]*)");
        private static readonly Regex _rule = new ("^\\s{0,3}((\\*\\s*){3,}|(-\\s*){3,}|(_\\s*){3,})$");
        private static readonly Regex _listItem = new ("^(\\s*)([-*+]|\\d{1,9}[.)])\\s+(.*)$");
        private static readonly Regex _taskBox = new ("^\\[([ xX])\\]\\s+(.*)$");
        private static readonly Regex _tableSeparator = new ("^\\s*\\|?\\s*:?-{2,}:?\\s*(\\|\\s*:?-{2,}:?\\s*)*\\|?\\s*$");

        public static List<MarkdownBlock> Parse(string markdown)
        {
            if (string.IsNullOrEmpty(markdown))
            {
                return new List<MarkdownBlock>();
            }

            Reader reader = new Reader(markdown.Replace("\r\n", "\n").Split('\n'));
            return reader.ReadAll();
        }

        public static string[] SplitRow(string line)
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("|", StringComparison.Ordinal))
            {
                trimmed = trimmed.Substring(1);
            }

            if (trimmed.EndsWith("|", StringComparison.Ordinal) && !trimmed.EndsWith("\\|", StringComparison.Ordinal))
            {
                trimmed = trimmed.Substring(0, trimmed.Length - 1);
            }

            List<string> cells = new List<string>();
            StringBuilder cell = new StringBuilder();
            for (int i = 0; i < trimmed.Length; i++)
            {
                char character = trimmed[i];
                if (character == '\\' && i + 1 < trimmed.Length && trimmed[i + 1] == '|')
                {
                    cell.Append('|');
                    i++;
                    continue;
                }

                if (character == '|')
                {
                    cells.Add(cell.ToString().Trim());
                    cell.Clear();
                    continue;
                }

                cell.Append(character);
            }

            cells.Add(cell.ToString().Trim());
            return cells.ToArray();
        }

        private static bool IsQuote(string line)
        {
            return line.TrimStart().StartsWith(">", StringComparison.Ordinal);
        }

        private static bool StartsBlock(string line)
        {
            return _fence.IsMatch(line) || _heading.IsMatch(line) || _listItem.IsMatch(line) || IsQuote(line) || _rule.IsMatch(line);
        }

        private static MarkdownListItem CreateListItem(Match match)
        {
            MarkdownListItem item = new MarkdownListItem
            {
                Indent = match.Groups[1].Value.Replace("\t", "    ").Length / 2,
                Marker = match.Groups[2].Value,
                Text = match.Groups[3].Value,
            };

            Match task = _taskBox.Match(item.Text);
            if (task.Success)
            {
                item.Checked = task.Groups[1].Value != " ";
                item.Text = task.Groups[2].Value;
            }

            return item;
        }

        private sealed class Reader
        {
            private readonly string[] _lines;
            private readonly List<MarkdownBlock> _blocks = new ();

            private int _index;

            public Reader(string[] lines)
            {
                _lines = lines;
            }

            public List<MarkdownBlock> ReadAll()
            {
                while (_index < _lines.Length)
                {
                    string line = _lines[_index];
                    if (line.Trim().Length == 0)
                    {
                        _index++;
                        continue;
                    }

                    Match fence = _fence.Match(line);
                    if (fence.Success)
                    {
                        ReadCode(fence);
                        continue;
                    }

                    Match heading = _heading.Match(line);
                    if (heading.Success)
                    {
                        _blocks.Add(new MarkdownBlock { Type = MarkdownBlockType.Heading, Level = heading.Groups[1].Value.Length, Text = heading.Groups[2].Value });
                        _index++;
                        continue;
                    }

                    if (_rule.IsMatch(line))
                    {
                        _blocks.Add(new MarkdownBlock { Type = MarkdownBlockType.Rule });
                        _index++;
                        continue;
                    }

                    if (IsQuote(line))
                    {
                        ReadQuote();
                        continue;
                    }

                    if (_listItem.IsMatch(line))
                    {
                        ReadList();
                        continue;
                    }

                    if (IsTableStart(_index))
                    {
                        ReadTable();
                        continue;
                    }

                    ReadParagraph();
                }

                return _blocks;
            }

            private bool IsTableStart(int index)
            {
                return _lines[index].Contains("|") && index + 1 < _lines.Length && _tableSeparator.IsMatch(_lines[index + 1]);
            }

            private void ReadCode(Match fence)
            {
                string marker = fence.Groups[1].Value;
                MarkdownBlock block = new MarkdownBlock { Type = MarkdownBlockType.Code, Language = fence.Groups[2].Value, IsClosed = false };
                StringBuilder code = new StringBuilder();
                _index++;
                while (_index < _lines.Length)
                {
                    string line = _lines[_index];
                    _index++;
                    string trimmed = line.Trim();
                    if (trimmed.StartsWith(marker, StringComparison.Ordinal) && trimmed.Trim(marker[0]).Length == 0)
                    {
                        block.IsClosed = true;
                        break;
                    }

                    if (code.Length > 0)
                    {
                        code.Append('\n');
                    }

                    code.Append(line);
                }

                block.Text = code.ToString();
                _blocks.Add(block);
            }

            private void ReadQuote()
            {
                StringBuilder text = new StringBuilder();
                while (_index < _lines.Length && IsQuote(_lines[_index]))
                {
                    if (text.Length > 0)
                    {
                        text.Append('\n');
                    }

                    text.Append(_lines[_index].TrimStart().Substring(1).TrimStart());
                    _index++;
                }

                _blocks.Add(new MarkdownBlock { Type = MarkdownBlockType.Quote, Text = text.ToString() });
            }

            private void ReadList()
            {
                MarkdownBlock block = new MarkdownBlock { Type = MarkdownBlockType.List };
                while (_index < _lines.Length)
                {
                    string line = _lines[_index];
                    Match match = _listItem.Match(line);
                    if (match.Success)
                    {
                        block.Items.Add(CreateListItem(match));
                        _index++;
                        continue;
                    }

                    if (line.Trim().Length == 0)
                    {
                        if (_index + 1 < _lines.Length && _listItem.IsMatch(_lines[_index + 1]))
                        {
                            _index++;
                            continue;
                        }

                        break;
                    }

                    if ((line.StartsWith("  ", StringComparison.Ordinal) || line.StartsWith("\t", StringComparison.Ordinal)) && block.Items.Count > 0)
                    {
                        MarkdownListItem last = block.Items[block.Items.Count - 1];
                        last.Text += "\n" + line.Trim();
                        _index++;
                        continue;
                    }

                    break;
                }

                _blocks.Add(block);
            }

            private void ReadTable()
            {
                MarkdownBlock block = new MarkdownBlock { Type = MarkdownBlockType.Table };
                block.Rows.Add(SplitRow(_lines[_index]));
                _index += 2;
                while (_index < _lines.Length && _lines[_index].Contains("|") && _lines[_index].Trim().Length > 0)
                {
                    block.Rows.Add(SplitRow(_lines[_index]));
                    _index++;
                }

                _blocks.Add(block);
            }

            private void ReadParagraph()
            {
                int start = _index;
                StringBuilder text = new StringBuilder();
                while (_index < _lines.Length)
                {
                    string line = _lines[_index];
                    if (line.Trim().Length == 0)
                    {
                        break;
                    }

                    if (_index > start && (StartsBlock(line) || IsTableStart(_index)))
                    {
                        break;
                    }

                    if (text.Length > 0)
                    {
                        text.Append('\n');
                    }

                    text.Append(line.TrimEnd());
                    _index++;
                }

                _blocks.Add(new MarkdownBlock { Type = MarkdownBlockType.Paragraph, Text = text.ToString() });
            }
        }
    }
}