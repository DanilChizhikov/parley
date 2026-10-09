namespace DTech.Parley.Editor
{
    internal readonly struct BlockStartRequest
    {
        public string Key { get; }
        public BlockKind Kind { get; }
        public string ParentToolUseId { get; }
        public string ToolUseId { get; }
        public string ToolName { get; }

        public BlockStartRequest(
            string key,
            BlockKind kind,
            string parentToolUseId,
            string toolUseId,
            string toolName)
        {
            Key = key;
            Kind = kind;
            ParentToolUseId = parentToolUseId;
            ToolUseId = toolUseId;
            ToolName = toolName;
        }
    }
}