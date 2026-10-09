using System;
using DTech.Parley.Editor.Tools;
using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Agents.Local
{
    internal readonly struct LocalAgentRequest
    {
        public ParleyProfile Profile { get; }
        public IAgentSink Sink { get; }
        public ToolCatalog Catalog { get; }
        public string SessionId { get; }
        public JArray History { get; }
        public PermissionMode Mode { get; }
        public Func<string, string, IChatCompletionClient> ClientFactory { get; }

        public LocalAgentRequest(
            ParleyProfile profile,
            IAgentSink sink,
            ToolCatalog catalog,
            string sessionId,
            JArray history,
            PermissionMode mode,
            Func<string, string, IChatCompletionClient> clientFactory = null)
        {
            Profile = profile;
            Sink = sink;
            Catalog = catalog;
            SessionId = sessionId;
            History = history;
            Mode = mode;
            ClientFactory = clientFactory;
        }
    }
}