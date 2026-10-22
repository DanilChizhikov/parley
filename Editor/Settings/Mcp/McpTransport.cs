using UnityEngine;

namespace DTech.Parley.Editor
{
    internal enum McpTransport : byte
    {
        [InspectorName("Local command (stdio)")]
        Stdio = 0,
        [InspectorName("Remote URL (HTTP)")]
        Http = 1,
    }
}
