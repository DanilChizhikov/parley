using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor
{
    internal readonly struct ToolResultRequest
    {
        public string ToolUseId { get; }
        public string Text { get; }
        public bool IsError { get; }
        public JToken Structured { get; }

        public ToolResultRequest(string toolUseId, string text, bool isError, JToken structured)
        {
            ToolUseId = toolUseId;
            Text = text;
            IsError = isError;
            Structured = structured;
        }
    }
}