using System;
using DTech.Parley.Editor.Commands;

namespace DTech.Parley.Tests.EditorMode
{
    internal sealed class ThrowingParleyCommand : IParleyCommand
    {
        public string Name => "throwing-command";

        public string Description => "throws";

        public string ArgumentHint => null;

        public string BuildPrompt(ParleyCommandRequest request)
        {
            throw new InvalidOperationException("boom");
        }
    }
}
