using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Mcp
{
    internal static class McpJsonRpc
    {
        public const int MethodNotFound = -32601;

        public static JObject Request(long id, string method, JObject parameters)
        {
            return new JObject { ["jsonrpc"] = "2.0", ["id"] = id, ["method"] = method, ["params"] = parameters ?? new JObject() };
        }

        public static JObject Notification(string method, JObject parameters)
        {
            JObject message = new JObject { ["jsonrpc"] = "2.0", ["method"] = method };
            if (parameters != null)
            {
                message["params"] = parameters;
            }

            return message;
        }

        public static JObject Result(JToken id, JObject result)
        {
            return new JObject { ["jsonrpc"] = "2.0", ["id"] = id?.DeepClone(), ["result"] = result };
        }

        public static JObject Error(JToken id, int code, string message)
        {
            return new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id?.DeepClone(),
                ["error"] = new JObject { ["code"] = code, ["message"] = message },
            };
        }

        public static string Key(JToken id)
        {
            return id == null || id.Type == JTokenType.Null ? null : id.ToString();
        }

        public static bool IsResponse(JObject message)
        {
            return message["method"] == null && message["id"] != null;
        }
    }
}
