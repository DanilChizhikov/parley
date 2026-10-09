namespace DTech.Parley.Editor.Tools
{
    internal enum ToolKind : byte
    {
        ReadOnly = 0,
        FileEdit = 1,
        Execute = 2,
        Network = 3,
        Interactive = 4,
    }
}