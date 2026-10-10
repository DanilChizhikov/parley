using System.Collections.Generic;

namespace DTech.Parley.Editor
{
    internal sealed class BackendCapabilities
    {
        public string Account { get; set; }
        public List<ModelOption> Models { get; } = new ();
        public List<SlashCommandInfo> Commands { get; } = new ();
        public List<PermissionMode> Modes { get; } = new ();
        public List<string> Efforts { get; } = new ();
    }
}