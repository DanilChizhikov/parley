using DTech.Parley.Editor.Skills;

namespace DTech.Parley.Editor.Commands
{
    internal sealed class CreatePanelCommand : IParleyCommand
    {
        public const string CommandName = "create-parley-panel";

        private const string Resource = "Parley/Commands/create-parley-panel";

        public string Name => CommandName;

        public string Description => "Build an editor panel for what you describe";

        public string ArgumentHint => "<what the panel should let you tune>";

        public string BuildPrompt(ParleyCommandRequest request)
        {
            string body = CommandText.Load(Resource);
            return body == null ? null : SkillRenderer.Render(CommandTokens.Expand(body), null, request.Arguments);
        }
    }
}
