using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Orchestration
{
    internal sealed class DelegationStreamParser
    {
        private const int MaxPayload = 65536;
        private readonly StringBuilder _line = new();
        private readonly StringBuilder _tag = new();
        private readonly List<DelegationRequest> _requests = new();
        private char _fence;
        private int _fenceLength;
        private bool _discardLine;
        private bool _cancelled;
        private int _ordinal;

        public void Feed(string delta)
        {
            if (_cancelled || string.IsNullOrEmpty(delta)) return;
            foreach (char value in delta)
            {
                if (value == '\n')
                {
                    if (!_discardLine) ReadLine(_line.ToString().TrimEnd('\r'));
                    _line.Clear();
                    _discardLine = false;
                }
                else if (_line.Length < MaxPayload && !_discardLine) _line.Append(value);
                else
                {
                    _line.Clear();
                    _tag.Clear();
                    _discardLine = true;
                }
            }
        }

        public IReadOnlyList<DelegationRequest> Complete()
        {
            if (!_cancelled && !_discardLine && _line.Length > 0) ReadLine(_line.ToString().TrimEnd('\r'));
            _line.Clear();
            _tag.Clear();
            return _requests;
        }

        public void Cancel()
        {
            _cancelled = true;
            _line.Clear();
            _tag.Clear();
            _requests.Clear();
        }

        private void ReadLine(string line)
        {
            string trimmed = line.Trim();
            char marker = trimmed.Length == 0 ? '\0' : trimmed[0];
            int count = 0;
            if (marker == '`' || marker == '~')
                while (count < trimmed.Length && trimmed[count] == marker) count++;
            if (count >= 3)
            {
                if (_fence == '\0') { _fence = marker; _fenceLength = count; }
                else if (marker == _fence && count >= _fenceLength && trimmed.Substring(count).Trim().Length == 0)
                    _fence = '\0';
                _tag.Clear();
                return;
            }
            if (_fence != '\0') return;
            if (_tag.Length == 0)
            {
                if (line.StartsWith("    ", StringComparison.Ordinal) || line.StartsWith("\t", StringComparison.Ordinal)
                    || !trimmed.StartsWith("<parley_delegate ", StringComparison.Ordinal)) return;
            }
            _tag.AppendLine(trimmed);
            if (_tag.Length > MaxPayload) { _tag.Clear(); return; }
            if (!trimmed.EndsWith("</parley_delegate>", StringComparison.Ordinal)) return;
            try
            {
                using StringReader source = new(_tag.ToString());
                using XmlReader reader = XmlReader.Create(source, new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
                    MaxCharactersInDocument = MaxPayload,
                });
                XElement element = XElement.Load(reader);
                if (element.Name != "parley_delegate" || element.HasElements) return;
                foreach (XAttribute attribute in element.Attributes())
                    if (attribute.Name != "target" && attribute.Name != "mode" && attribute.Name != "id") return;
                JObject input = new()
                {
                    ["target_skill"] = (string)element.Attribute("target"),
                    ["context_mode"] = (string)element.Attribute("mode"),
                    ["task_summary"] = element.Value,
                };
                _requests.Add(DelegationRequest.Parse(input,
                    (string)element.Attribute("id") ?? "tag:" + ++_ordinal));
            }
            catch (XmlException) { }
            catch (ArgumentException) { }
            finally { _tag.Clear(); }
        }
    }
}
