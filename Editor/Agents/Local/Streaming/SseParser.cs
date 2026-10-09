using System;
using System.Text;

namespace DTech.Parley.Editor.Agents.Local
{
    internal sealed class SseParser
    {
        public event Action<string> OnData;

        private readonly StringBuilder _line = new ();
        private readonly StringBuilder _raw = new ();

        public string RawPrefix => _raw.ToString();

        public bool IsDone { get; private set; }

        public void Feed(string chunk)
        {
            if (_raw.Length < 4096)
            {
                _raw.Append(chunk, 0, Math.Min(chunk.Length, 4096 - _raw.Length));
            }

            foreach (char character in chunk)
            {
                if (character == '\n')
                {
                    ProcessLine(_line.ToString());
                    _line.Clear();
                }
                else if (character != '\r')
                {
                    _line.Append(character);
                }
            }
        }

        public void Flush()
        {
            if (_line.Length > 0)
            {
                ProcessLine(_line.ToString());
                _line.Clear();
            }
        }

        private void ProcessLine(string line)
        {
            if (IsDone || !line.StartsWith("data:", StringComparison.Ordinal))
            {
                return;
            }

            string data = line.Substring(5).TrimStart();
            if (data == "[DONE]")
            {
                IsDone = true;
                return;
            }

            if (data.Length > 0)
            {
                OnData?.Invoke(data);
            }
        }
    }
}