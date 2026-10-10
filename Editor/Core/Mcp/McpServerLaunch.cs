using System.Collections.Generic;

namespace DTech.Parley.Editor
{
    internal sealed class McpServerLaunch
    {
        public string Name { get; set; }
        public McpTransport Transport { get; set; }
        public string Command { get; set; }
        public List<string> Arguments { get; } = new ();
        public Dictionary<string, string> Environment { get; } = new ();
        public string Url { get; set; }
        public Dictionary<string, string> Headers { get; } = new ();
    }
}
