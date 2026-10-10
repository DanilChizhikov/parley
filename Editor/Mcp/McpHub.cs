using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DTech.Parley.Editor.Tools;

namespace DTech.Parley.Editor.Mcp
{
    internal sealed class McpHub : IDisposable
    {
        public event Action OnChanged;

        private readonly Dictionary<string, McpClient> _clients = new (StringComparer.Ordinal);
        private readonly List<string> _removed = new ();
        private readonly List<Task> _connecting = new ();

        public void Dispose()
        {
            foreach (McpClient client in _clients.Values)
            {
                Release(client);
            }

            _clients.Clear();
            OnChanged = null;
        }

        public void Apply(McpConfiguration configuration)
        {
            HashSet<string> wanted = new (StringComparer.Ordinal);
            foreach (McpServerLaunch server in configuration.Servers)
            {
                wanted.Add(server.Name);
                if (_clients.TryGetValue(server.Name, out McpClient existing))
                {
                    if (existing.Fingerprint == McpServerResolver.Fingerprint(server))
                    {
                        continue;
                    }

                    Release(existing);
                }

                McpClient client = McpClient.Create(server);
                client.OnChanged += ClientChangedHandler;
                _clients[server.Name] = client;
                client.Connect();
            }

            _removed.Clear();
            foreach (string name in _clients.Keys)
            {
                if (!wanted.Contains(name))
                {
                    _removed.Add(name);
                }
            }

            foreach (string name in _removed)
            {
                Release(_clients[name]);
                _clients.Remove(name);
            }

            OnChanged?.Invoke();
        }

        public void ReconnectFailed()
        {
            foreach (McpClient client in _clients.Values)
            {
                if (client.State == McpConnectionState.Failed)
                {
                    client.Connect();
                }
            }
        }

        public async Task<bool> WhenReadyAsync(int timeoutMs, CancellationToken cancellationToken)
        {
            _connecting.Clear();
            foreach (McpClient client in _clients.Values)
            {
                if (!client.Connecting.IsCompleted)
                {
                    _connecting.Add(client.Connecting);
                }
            }

            if (_connecting.Count == 0)
            {
                return true;
            }

            Task all = Task.WhenAll(_connecting);
            using (CancellationTokenSource delay = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                Task finished = await Task.WhenAny(all, Task.Delay(timeoutMs, delay.Token));
                delay.Cancel();
                cancellationToken.ThrowIfCancellationRequested();
                return finished == all;
            }
        }

        public bool IsConnecting()
        {
            foreach (McpClient client in _clients.Values)
            {
                if (!client.Connecting.IsCompleted)
                {
                    return true;
                }
            }

            return false;
        }

        public void CollectTools(List<IParleyTool> tools)
        {
            foreach (McpClient client in _clients.Values)
            {
                if (client.State == McpConnectionState.Connected)
                {
                    tools.AddRange(client.Tools);
                }
            }
        }

        public List<McpServerStatus> Statuses()
        {
            List<McpServerStatus> statuses = new ();
            foreach (McpClient client in _clients.Values)
            {
                statuses.Add(new McpServerStatus
                {
                    Name = client.Name,
                    State = client.State,
                    Error = client.Error,
                    ToolCount = client.State == McpConnectionState.Connected ? client.Tools.Count : -1,
                });
            }

            return statuses;
        }

        private void Release(McpClient client)
        {
            client.OnChanged -= ClientChangedHandler;
            client.Dispose();
        }

        private void ClientChangedHandler()
        {
            OnChanged?.Invoke();
        }
    }
}
