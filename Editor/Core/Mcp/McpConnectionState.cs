namespace DTech.Parley.Editor
{
    internal enum McpConnectionState : byte
    {
        Pending = 0,
        Connected = 1,
        Failed = 2,
        NeedsAuth = 3,
        Disabled = 4,
    }
}
