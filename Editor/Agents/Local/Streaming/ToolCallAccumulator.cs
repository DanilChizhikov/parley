using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Agents.Local
{
    internal sealed class ToolCallAccumulator
    {
        private readonly List<AccumulatedToolCall> _calls = new ();

        public IReadOnlyList<AccumulatedToolCall> Calls => _calls;

        public void Feed(JArray toolCalls)
        {
            if (toolCalls == null)
            {
                return;
            }

            int position = 0;
            foreach (JToken token in toolCalls)
            {
                int index = (int?)token["index"] ?? position;
                string id = (string)token["id"];
                AccumulatedToolCall call = Find(index, id);
                if (call == null)
                {
                    call = new AccumulatedToolCall { Index = index };
                    _calls.Add(call);
                }

                if (!string.IsNullOrEmpty(id))
                {
                    call.Id = id;
                }

                JToken function = token["function"];
                string name = (string)function?["name"];
                if (!string.IsNullOrEmpty(name))
                {
                    call.Name = name;
                }

                JToken arguments = function?["arguments"];
                if (arguments != null && arguments.Type != JTokenType.Null)
                {
                    call.Arguments.Append(arguments.Type == JTokenType.String ? (string)arguments : arguments.ToString(Formatting.None));
                }

                position++;
            }
        }

        public void EnsureIds()
        {
            foreach (AccumulatedToolCall call in _calls)
            {
                if (string.IsNullOrEmpty(call.Id))
                {
                    call.Id = "call_" + Guid.NewGuid().ToString("N").Substring(0, 12);
                }
            }
        }

        private AccumulatedToolCall Find(int index, string id)
        {
            foreach (AccumulatedToolCall call in _calls)
            {
                if (call.Index == index && (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(call.Id) || call.Id == id))
                {
                    return call;
                }
            }

            return null;
        }
    }
}