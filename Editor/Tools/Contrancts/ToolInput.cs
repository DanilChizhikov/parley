using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Tools
{
    internal static class ToolInput
    {
        public static string String(JObject input, string name, string fallback = null)
        {
            JToken token = input?[name];
            return token == null || token.Type == JTokenType.Null ? fallback : token.Type == JTokenType.String ? (string)token : token.ToString();
        }

        public static int Int(JObject input, string name, int fallback)
        {
            JToken token = input?[name];
            if (token == null)
            {
                return fallback;
            }

            if (token.Type == JTokenType.Integer || token.Type == JTokenType.Float)
            {
                return (int)token;
            }

            return int.TryParse(token.ToString(), out int value) ? value : fallback;
        }

        public static bool Bool(JObject input, string name, bool fallback)
        {
            JToken token = input?[name];
            if (token == null)
            {
                return fallback;
            }

            if (token.Type == JTokenType.Boolean)
            {
                return (bool)token;
            }

            return bool.TryParse(token.ToString(), out bool value) ? value : fallback;
        }
    }
}