using UnityEngine;

namespace DTech.Parley.Editor
{
    internal enum CodexAuthMethod : byte
    {
        [InspectorName("Codex default")]
        CliDefault = 0,
        [InspectorName("OpenAI API key")]
        ApiKey = 1,
    }
}