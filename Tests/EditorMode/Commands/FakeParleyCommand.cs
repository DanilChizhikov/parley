using DTech.Parley.Editor.Commands;

namespace DTech.Parley.Tests.EditorMode
{
    internal sealed class FakeParleyCommand : IParleyCommand
    {
        public string Name { get; set; }
        public string Description { get; set; } = "fake";
        public string ArgumentHint { get; set; }
        public string Prompt { get; set; } = "prompt";
        public ParleyCommandRequest Request { get; private set; }

        public string BuildPrompt(ParleyCommandRequest request)
        {
            Request = request;
            return Prompt;
        }
    }
}
