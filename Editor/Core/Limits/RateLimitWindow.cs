using System;

namespace DTech.Parley.Editor
{
    internal readonly struct RateLimitWindow
    {
        private const double FullPercent = 100.0;

        public double UsedPercent { get; }
        public DateTimeOffset? ResetsAt { get; }

        public RateLimitWindow(double usedPercent, DateTimeOffset? resetsAt)
        {
            UsedPercent = double.IsNaN(usedPercent) ? 0.0 : Math.Max(0.0, usedPercent);
            ResetsAt = resetsAt;
        }

        public bool IsReset(DateTimeOffset now)
        {
            return ResetsAt.HasValue && ResetsAt.Value <= now;
        }

        public double RemainingPercent(DateTimeOffset now)
        {
            return IsReset(now) ? FullPercent : Math.Max(0.0, Math.Min(FullPercent, FullPercent - UsedPercent));
        }
    }
}
