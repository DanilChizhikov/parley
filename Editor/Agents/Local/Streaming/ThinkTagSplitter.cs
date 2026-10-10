using System;
using System.Text;

namespace DTech.Parley.Editor.Agents.Local
{
    internal sealed class ThinkTagSplitter
    {
        private const string OpenTag = "<think>";
        private const string CloseTag = "</think>";

        private readonly StringBuilder _pending = new ();

        private bool _inside;

        public void Feed(string text, Action<bool, string> emit)
        {
            _pending.Append(text);
            while (_pending.Length > 0)
            {
                string buffer = _pending.ToString();
                string tag = _inside ? CloseTag : OpenTag;
                int index = buffer.IndexOf(tag, StringComparison.Ordinal);
                if (index >= 0)
                {
                    if (index > 0)
                    {
                        emit(_inside, buffer.Substring(0, index));
                    }

                    _pending.Remove(0, index + tag.Length);
                    _inside = !_inside;
                    continue;
                }

                int keep = PartialTagSuffix(buffer, tag);
                if (buffer.Length - keep > 0)
                {
                    emit(_inside, buffer.Substring(0, buffer.Length - keep));
                }

                _pending.Remove(0, buffer.Length - keep);
                break;
            }
        }

        public void Flush(Action<bool, string> emit)
        {
            if (_pending.Length > 0)
            {
                emit(_inside, _pending.ToString());
                _pending.Clear();
            }
        }

        private static int PartialTagSuffix(string buffer, string tag)
        {
            for (int length = Math.Min(tag.Length - 1, buffer.Length); length > 0; length--)
            {
                if (string.CompareOrdinal(buffer, buffer.Length - length, tag, 0, length) == 0)
                {
                    return length;
                }
            }

            return 0;
        }
    }
}