using System;

namespace DTech.Parley.Editor
{
    internal sealed class RateLimits
    {
        public RateLimitWindow? Session { get; }
        public RateLimitWindow? Weekly { get; }
        public bool Unavailable { get; }
        public DateTimeOffset UpdatedAt { get; }

        public bool HasWindows => Session.HasValue || Weekly.HasValue;

        public RateLimits(RateLimitWindow? session, RateLimitWindow? weekly, bool unavailable, DateTimeOffset updatedAt)
        {
            Session = session;
            Weekly = weekly;
            Unavailable = unavailable && !session.HasValue && !weekly.HasValue;
            UpdatedAt = updatedAt;
        }

        public static RateLimits NotAvailable()
        {
            return new RateLimits(null, null, true, DateTimeOffset.Now);
        }

        public RateLimits MergedWith(RateLimits update)
        {
            if (update == null)
            {
                return this;
            }

            return new RateLimits(
                update.Session ?? Session,
                update.Weekly ?? Weekly,
                update.Unavailable || Unavailable,
                update.UpdatedAt);
        }
    }
}
