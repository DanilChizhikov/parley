using System;
using System.Collections.Generic;

namespace DTech.Parley.Editor
{
    internal sealed class McpConfiguration
    {
        public List<McpServerLaunch> Servers { get; } = new ();
        public HashSet<string> DisabledExternal { get; } = new (StringComparer.Ordinal);

        public bool HasServer(string name)
        {
            foreach (McpServerLaunch server in Servers)
            {
                if (server.Name == name)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
