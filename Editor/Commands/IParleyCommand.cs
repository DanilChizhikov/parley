namespace DTech.Parley.Editor.Commands
{
    /// <summary>
    /// A slash command offered by the Parley composer. Implement it in any editor assembly to add your
    /// own command: the type must be public, non-abstract and have a public parameterless constructor.
    /// </summary>
    public interface IParleyCommand
    {
        /// <summary>Name without the leading slash: lowercase letters, digits and '-', up to 64 characters.</summary>
        string Name { get; }

        /// <summary>One line shown next to the name in the composer suggestions.</summary>
        string Description { get; }

        /// <summary>Hint for the expected arguments, for example "&lt;what the panel should tune&gt;". May be null.</summary>
        string ArgumentHint { get; }

        /// <summary>Builds the text sent to the agent in place of what the user typed.</summary>
        /// <param name="request">Arguments typed after the command name, plus project context.</param>
        /// <returns>The prompt to send, or null to send the typed text unchanged.</returns>
        string BuildPrompt(ParleyCommandRequest request);
    }
}
