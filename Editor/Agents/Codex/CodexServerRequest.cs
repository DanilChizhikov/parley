using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Agents.Codex
{
    internal sealed class CodexServerRequest
    {
        public JToken RpcId { get; set; }
        public string Method { get; set; }
        public JObject Params { get; set; }
        public PendingRequest Request { get; set; }
    }
}