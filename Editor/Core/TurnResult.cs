using System.Collections.Generic;

namespace DTech.Parley.Editor
{
    internal sealed class TurnResult
    {
        public bool IsError { get; set; }
        public string Subtype { get; set; }
        public string ResultText { get; set; }
        public long DurationMs { get; set; }
        public int NumTurns { get; set; }
        public double? CostUsd { get; set; }
        public long InputTokens { get; set; }
        public long OutputTokens { get; set; }
        public long CacheReadTokens { get; set; }
        public long CacheCreationTokens { get; set; }
        public int PermissionDenials { get; set; }
        public List<string> Errors { get; } = new ();
    }
}