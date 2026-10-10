using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Mcp
{
    internal interface IMcpTransport : IDisposable
    {
        event Action<string> OnClosed;

        void UseProtocolVersion(string version);
        Task StartAsync(CancellationToken cancellationToken);
        Task<JObject> RequestAsync(JObject message, CancellationToken cancellationToken);
        Task NotifyAsync(JObject message, CancellationToken cancellationToken);
    }
}
