namespace DTech.Parley.Editor
{
    internal sealed class McpServerStatus
    {
        public string Name { get; set; }
        public McpConnectionState State { get; set; }
        public string Error { get; set; }
        public int ToolCount { get; set; } = -1;
        public bool IsExternal { get; set; }
    }
}
