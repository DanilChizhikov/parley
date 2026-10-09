using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Agents.Local
{
    internal sealed class RoundStream
    {
        private readonly IAgentSink _sink;
        private readonly string _textKey;
        private readonly string _thinkingKey;
        private readonly StringBuilder _text = new ();
        private readonly StringBuilder _thinking = new ();
        private readonly ToolCallAccumulator _accumulator = new ();
        private readonly ThinkTagSplitter _splitter = new ();

        public List<AccumulatedToolCall> Calls { get; } = new ();
        public string Text { get; private set; } = string.Empty;
        public string PartialText => _text.ToString();
        public long PromptTokens { get; private set; }
        public long CompletionTokens { get; private set; }

        private bool _textStarted;
        private bool _thinkingStarted;

        public RoundStream(IAgentSink sink, int block)
        {
            _sink = sink;
            _textKey = "local:" + block + ":text";
            _thinkingKey = "local:" + block + ":thinking";
        }

        public void HandleChunk(JObject chunk)
        {
            if (chunk["usage"] is JObject usage)
            {
                PromptTokens = (long?)usage["prompt_tokens"] ?? PromptTokens;
                CompletionTokens = (long?)usage["completion_tokens"] ?? CompletionTokens;
            }

            if (!(chunk["choices"] is JArray choices) || choices.Count == 0 || !(choices[0]["delta"] is JObject delta))
            {
                return;
            }

            Emit(true, ReadText(delta["reasoning_content"]) ?? ReadText(delta["reasoning"]));
            string content = ReadText(delta["content"]);
            if (!string.IsNullOrEmpty(content))
            {
                _splitter.Feed(content, Emit);
            }

            _accumulator.Feed(delta["tool_calls"] as JArray);
        }

        public void Complete(bool textToolCalls)
        {
            _splitter.Flush(Emit);
            string finalText = _text.ToString();
            if (textToolCalls)
            {
                List<(string name, string arguments)> parsed = new ();
                finalText = TextToolCallParser.Extract(finalText, parsed);
                foreach ((string name, string arguments) in parsed)
                {
                    AccumulatedToolCall call = new AccumulatedToolCall { Name = name, Index = Calls.Count };
                    call.Arguments.Append(arguments);
                    Calls.Add(call);
                }
            }

            if (_thinkingStarted)
            {
                _sink.BlockFinalized(new BlockFinalizeRequest(_thinkingKey, BlockKind.Thinking, null, _thinking.ToString(), null, null, null));
            }

            if (_textStarted)
            {
                _sink.BlockFinalized(new BlockFinalizeRequest(_textKey, BlockKind.Text, null, finalText, null, null, null));
            }

            _accumulator.EnsureIds();
            Calls.AddRange(_accumulator.Calls);
            foreach (AccumulatedToolCall call in Calls)
            {
                if (string.IsNullOrEmpty(call.Id))
                {
                    call.Id = "call_" + Guid.NewGuid().ToString("N").Substring(0, 12);
                }

                JObject input = call.ParseArguments() ?? new JObject();
                _sink.BlockFinalized(new BlockFinalizeRequest("local:tool:" + call.Id, BlockKind.ToolUse, null, null, call.Id, call.Name, input));
            }

            Text = finalText;
        }

        private static string ReadText(JToken token)
        {
            return token != null && token.Type == JTokenType.String ? (string)token : null;
        }

        private void Emit(bool isThinking, string piece)
        {
            if (string.IsNullOrEmpty(piece))
            {
                return;
            }

            if (isThinking)
            {
                if (!_thinkingStarted)
                {
                    _thinkingStarted = true;
                    _sink.BlockStarted(new BlockStartRequest(_thinkingKey, BlockKind.Thinking, null, null, null));
                }

                _thinking.Append(piece);
                _sink.TextDelta(_thinkingKey, piece);
                return;
            }

            if (!_textStarted)
            {
                _textStarted = true;
                _sink.BlockStarted(new BlockStartRequest(_textKey, BlockKind.Text, null, null, null));
            }

            _text.Append(piece);
            _sink.TextDelta(_textKey, piece);
        }
    }
}