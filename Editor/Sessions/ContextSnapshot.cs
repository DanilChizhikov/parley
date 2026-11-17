using System;
using Newtonsoft.Json;

namespace DTech.Parley.Editor.Sessions
{
    internal sealed class ContextSnapshot
    {
        public string SourceSessionId { get; }
        public DateTime CapturedUtc { get; }
        public string Markdown { get; }
        public int Characters => Markdown.Length;
        public int EstimatedTokens => Characters / 4 + (Characters % 4 == 0 ? 0 : 1);

        [JsonConstructor]
        public ContextSnapshot(string sourceSessionId, DateTime capturedUtc, string markdown)
        {
            SourceSessionId = sourceSessionId;
            CapturedUtc = capturedUtc;
            Markdown = markdown ?? string.Empty;
        }
    }
}
