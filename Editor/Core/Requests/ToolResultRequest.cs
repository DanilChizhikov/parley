using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor
{
    internal readonly struct ToolResultRequest
    {
        public string ToolUserId { get; }
        public string Text { get; }
        public bool IsError { get; }
        public JToken Structured { get; }

        public ToolResultRequest(string toolUserId, string text, bool isError, JToken structured)
        {
            ToolUserId = toolUserId;
            Text = text;
            IsError = isError;
            Structured = structured;
        }
    }
}