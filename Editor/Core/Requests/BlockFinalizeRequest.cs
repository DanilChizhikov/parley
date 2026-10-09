using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor
{
    internal readonly struct BlockFinalizeRequest
    {
        public string Key { get; }
        public BlockKind Kind { get; }
        public string ParentToolUseId { get; }
        public string Text { get; }
        public string ToolUseId { get; }
        public string ToolName { get; }
        public JObject Input { get; }

        public BlockFinalizeRequest(
            string key,
            BlockKind kind,
            string parentToolUseId,
            string text,
            string toolUseId,
            string toolName,
            JObject input)
        {
            Key = key;
            Kind = kind;
            ParentToolUseId = parentToolUseId;
            Text = text;
            ToolUseId = toolUseId;
            ToolName = toolName;
            Input = input;
        }
    }
}