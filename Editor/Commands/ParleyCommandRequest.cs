namespace DTech.Parley.Editor.Commands
{
    /// <summary>Context handed to <see cref="IParleyCommand.BuildPrompt"/>.</summary>
    public readonly struct ParleyCommandRequest
    {
        /// <summary>Text typed after the command name, trimmed. Empty when no arguments were typed.</summary>
        public string Arguments { get; }

        /// <summary>Absolute path to the Unity project root.</summary>
        public string ProjectRoot { get; }

        /// <summary>Creates a request.</summary>
        /// <param name="arguments">Text typed after the command name.</param>
        /// <param name="projectRoot">Absolute path to the Unity project root.</param>
        public ParleyCommandRequest(string arguments, string projectRoot)
        {
            Arguments = arguments ?? string.Empty;
            ProjectRoot = projectRoot;
        }
    }
}
