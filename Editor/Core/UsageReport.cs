namespace DTech.Parley.Editor
{
    internal readonly struct UsageReport
    {
        public string CallId { get; }
        public string ParentToolUseId { get; }
        public string Model { get; }
        public long InputTokens { get; }
        public long CacheCreationTokens { get; }
        public long CacheReadTokens { get; }
        public long OutputTokens { get; }
        public long ReasoningTokens { get; }

        public UsageReport(
            string callId,
            string parentToolUseId,
            string model,
            long inputTokens,
            long cacheCreationTokens,
            long cacheReadTokens,
            long outputTokens,
            long reasoningTokens)
        {
            CallId = callId;
            ParentToolUseId = parentToolUseId;
            Model = model;
            InputTokens = inputTokens;
            CacheCreationTokens = cacheCreationTokens;
            CacheReadTokens = cacheReadTokens;
            OutputTokens = outputTokens;
            ReasoningTokens = reasoningTokens;
        }
    }
}
