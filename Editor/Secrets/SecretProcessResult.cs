namespace DTech.Parley.Editor.Secrets
{
    internal readonly struct SecretProcessResult
    {
        public int ExitCode { get; }
        public string Output { get; }
        public string Error { get; }

        public SecretProcessResult(int exitCode, string output, string error)
        {
            ExitCode = exitCode;
            Output = output;
            Error = error;
        }
    }
}