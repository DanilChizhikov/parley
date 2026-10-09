namespace DTech.Parley.Editor
{
    internal sealed class ConsoleEntry
    {
        public ConsoleSeverity Severity { get; set; }
        public bool IsCompilerMessage { get; set; }
        public string Message { get; set; }
        public string StackTrace { get; set; }
        public string File { get; set; }
        public int Line { get; set; }
    }
}