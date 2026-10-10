using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Agents.Local
{
    internal static class TextToolCallParser
    {
        private const string OpenTag = "<tool_call>";
        private const string CloseTag = "</tool_call>";

        public static string Extract(string text, List<(string name, string arguments)> calls)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf(OpenTag, StringComparison.Ordinal) < 0)
            {
                return text;
            }

            StringBuilder remaining = new StringBuilder();
            int position = 0;
            while (true)
            {
                int start = text.IndexOf(OpenTag, position, StringComparison.Ordinal);
                if (start < 0)
                {
                    remaining.Append(text, position, text.Length - position);
                    break;
                }

                remaining.Append(text, position, start - position);
                int end = text.IndexOf(CloseTag, start, StringComparison.Ordinal);
                string body = end < 0 ? text.Substring(start + OpenTag.Length) : text.Substring(start + OpenTag.Length, end - start - OpenTag.Length);
                try
                {
                    JObject json = JObject.Parse(body.Trim());
                    JToken arguments = json["arguments"] ?? json["parameters"] ?? new JObject();
                    calls.Add(((string)json["name"], arguments.Type == JTokenType.String ? (string)arguments : arguments.ToString(Formatting.None)));
                }
                catch (Exception)
                {
                    remaining.Append(OpenTag).Append(body).Append(CloseTag);
                }

                if (end < 0)
                {
                    break;
                }

                position = end + CloseTag.Length;
            }

            return remaining.ToString().Trim();
        }
    }
}