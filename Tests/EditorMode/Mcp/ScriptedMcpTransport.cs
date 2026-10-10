using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DTech.Parley.Editor.Mcp;
using Newtonsoft.Json.Linq;

namespace DTech.Parley.Tests.EditorMode
{
    internal sealed class ScriptedMcpTransport : IMcpTransport
    {
        public event Action<string> OnClosed;

        public List<JObject> Requests { get; } = new ();
        public List<JObject> Notifications { get; } = new ();
        public Dictionary<string, Func<JObject, JObject>> Handlers { get; } = new ();
        public bool Started { get; private set; }
        public bool Disposed { get; private set; }
        public string ProtocolVersion { get; private set; }

        public void Close(string reason)
        {
            OnClosed?.Invoke(reason);
        }

        public void Dispose()
        {
            Disposed = true;
        }

        public void UseProtocolVersion(string version)
        {
            ProtocolVersion = version;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            Started = true;
            return Task.CompletedTask;
        }

        public Task<JObject> RequestAsync(JObject message, CancellationToken cancellationToken)
        {
            Requests.Add(message);
            string method = (string)message["method"];
            JObject result = Handlers.TryGetValue(method, out Func<JObject, JObject> handler) ? handler(message["params"] as JObject) : new JObject();
            JObject response = (string)result["__error"] != null
                ? McpJsonRpc.Error(message["id"], -32000, (string)result["__error"])
                : McpJsonRpc.Result(message["id"], result);
            return Task.FromResult(response);
        }

        public Task NotifyAsync(JObject message, CancellationToken cancellationToken)
        {
            Notifications.Add(message);
            return Task.CompletedTask;
        }
    }
}
