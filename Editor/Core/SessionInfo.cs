using System.Collections.Generic;

namespace DTech.Parley.Editor
{
    internal sealed class SessionInfo
    {
        public string SessionId { get; set; }
        public string Model { get; set; }
        public PermissionMode Mode { get; set; }
        public string Cwd { get; set; }
        public string ApiKeySource { get; set; }
        public List<string> Tools { get; } = new ();
        public List<string> McpServers { get; } = new ();
        public List<string> SlashCommands { get; } = new ();
    }
}