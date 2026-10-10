using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Tools
{
    internal sealed class ToolResult
    {
        public string Text { get; set; } = string.Empty;
        public bool IsError { get; set; }
        public JToken Structured { get; set; }

        public static ToolResult Ok(string text, JToken structured = null)
        {
            return new ToolResult { Text = text ?? string.Empty, Structured = structured };
        }

        public static ToolResult Error(string text)
        {
            return new ToolResult { Text = text ?? string.Empty, IsError = true };
        }
    }
}