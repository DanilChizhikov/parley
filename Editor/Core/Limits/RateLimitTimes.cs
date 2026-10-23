using System;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor
{
    internal static class RateLimitTimes
    {
        public static DateTimeOffset? Parse(JToken token)
        {
            if (token == null)
            {
                return null;
            }

            switch (token.Type)
            {
                case JTokenType.Date:
                    object value = ((JValue)token).Value;
                    return value is DateTimeOffset offset ? offset : new DateTimeOffset((DateTime)value);
                case JTokenType.Integer:
                case JTokenType.Float:
                    return FromUnixSeconds((double)token);
                case JTokenType.String:
                    string text = (string)token;
                    if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds))
                    {
                        return FromUnixSeconds(seconds);
                    }

                    return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset parsed)
                        ? parsed
                        : (DateTimeOffset?)null;
                default:
                    return null;
            }
        }

        private static DateTimeOffset? FromUnixSeconds(double seconds)
        {
            if (seconds <= 0 || double.IsNaN(seconds) || double.IsInfinity(seconds))
            {
                return null;
            }

            try
            {
                return DateTimeOffset.FromUnixTimeMilliseconds((long)(seconds * 1000.0));
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }
    }
}
