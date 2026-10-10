using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DTech.Parley.Editor.Tools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Mcp
{
    internal sealed class McpClient : IDisposable
    {
        public event Action OnChanged;

        private const string ProtocolVersion = "2025-06-18";
        private const string ClientVersion = "0.1.0";
        private const int ConnectTimeoutMs = 120000;
        private const int ToolCallTimeoutMs = 600000;
        private const string InitializeMethod = "initialize";
        private const int MaxToolPages = 20;

        private readonly IMcpTransport _transport;
        private readonly List<IParleyTool> _tools = new ();
        private readonly CancellationTokenSource _lifetime = new ();

        public string Name { get; }

        public string Fingerprint { get; }

        public McpConnectionState State { get; private set; } = McpConnectionState.Pending;

        public string Error { get; private set; }

        public IReadOnlyList<IParleyTool> Tools => _tools;

        public Task Connecting { get; private set; } = Task.CompletedTask;

        private long _nextId;
        private bool _disposed;

        public McpClient(McpServerLaunch launch, IMcpTransport transport)
        {
            Name = launch.Name;
            Fingerprint = McpServerResolver.Fingerprint(launch);
            _transport = transport;
            _transport.OnClosed += ClosedHandler;
        }

        public static McpClient Create(McpServerLaunch launch)
        {
            IMcpTransport transport = launch.Transport == McpTransport.Stdio ? new McpStdioTransport(launch) : new McpHttpTransport(launch);
            return new McpClient(launch, transport);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            OnChanged = null;
            _lifetime.Cancel();
            _transport.OnClosed -= ClosedHandler;
            _transport.Dispose();
            _lifetime.Dispose();
        }

        public void Connect()
        {
            if (!_disposed)
            {
                Connecting = ConnectAsync();
            }
        }

        public async Task<ToolResult> CallToolAsync(string toolName, JObject arguments, CancellationToken cancellationToken)
        {
            if (_disposed || State != McpConnectionState.Connected)
            {
                return ToolResult.Error("MCP server '" + Name + "' is not connected" + (string.IsNullOrEmpty(Error) ? "." : ": " + Error));
            }

            JObject parameters = new JObject { ["name"] = toolName, ["arguments"] = arguments ?? new JObject() };
            JObject result;
            using (CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token))
            {
                timeout.CancelAfter(ToolCallTimeoutMs);
                try
                {
                    result = await RequestAsync("tools/call", parameters, timeout.Token);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    return ToolResult.Error(_disposed ? "MCP server '" + Name + "' was stopped." : "MCP server '" + Name + "' did not answer within 10 minutes.");
                }
            }

            JToken structured = result["structuredContent"];
            string text = ContentText(result["content"] as JArray);
            if (text.Length == 0 && structured != null)
            {
                text = structured.ToString(Formatting.Indented);
            }

            return (bool?)result["isError"] == true ? ToolResult.Error(text) : ToolResult.Ok(text, structured);
        }

        private static string ContentText(JArray content)
        {
            StringBuilder builder = new StringBuilder();
            if (content == null)
            {
                return string.Empty;
            }

            foreach (JToken item in content)
            {
                string part = (string)item["type"] switch
                {
                    "text" => (string)item["text"],
                    "image" => "[image]",
                    "audio" => "[audio]",
                    "resource" => (string)item["resource"]?["text"] ?? (string)item["resource"]?["uri"],
                    "resource_link" => (string)item["uri"],
                    _ => item.ToString(Formatting.None),
                };

                if (builder.Length > 0)
                {
                    builder.Append('\n');
                }

                builder.Append(part);
            }

            return builder.ToString();
        }

        private async Task ConnectAsync()
        {
            State = McpConnectionState.Pending;
            Error = null;
            _tools.Clear();
            OnChanged?.Invoke();
            try
            {
                using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
                timeout.CancelAfter(ConnectTimeoutMs);
                await _transport.StartAsync(timeout.Token);
                JObject parameters = new JObject
                {
                    ["protocolVersion"] = ProtocolVersion,
                    ["capabilities"] = new JObject(),
                    ["clientInfo"] = new JObject { ["name"] = "parley", ["version"] = ClientVersion },
                };

                JObject initialized = await RequestAsync(InitializeMethod, parameters, timeout.Token);
                _transport.UseProtocolVersion((string)initialized["protocolVersion"] ?? ProtocolVersion);
                await _transport.NotifyAsync(McpJsonRpc.Notification("notifications/initialized", null), timeout.Token);
                await LoadToolsAsync(timeout.Token);
            }
            catch (OperationCanceledException) when (_disposed)
            {
                return;
            }
            catch (OperationCanceledException)
            {
                Fail("Timed out while connecting.");
                return;
            }
            catch (Exception exception)
            {
                if (!_disposed)
                {
                    Fail(exception.Message);
                }

                return;
            }

            if (_disposed || State == McpConnectionState.Failed)
            {
                return;
            }

            State = McpConnectionState.Connected;
            OnChanged?.Invoke();
        }

        private async Task LoadToolsAsync(CancellationToken cancellationToken)
        {
            string cursor = null;
            for (int page = 0; page < MaxToolPages; page++)
            {
                JObject parameters = cursor == null ? new JObject() : new JObject { ["cursor"] = cursor };
                JObject result = await RequestAsync("tools/list", parameters, cancellationToken);
                if (result["tools"] is JArray tools)
                {
                    foreach (JToken tool in tools)
                    {
                        AddTool(tool as JObject);
                    }
                }

                cursor = (string)result["nextCursor"];
                if (string.IsNullOrEmpty(cursor))
                {
                    return;
                }
            }
        }

        private void AddTool(JObject tool)
        {
            string name = (string)tool?["name"];
            if (string.IsNullOrEmpty(name) || HasTool(McpServerResolver.ToolName(Name, name)))
            {
                return;
            }

            _tools.Add(new McpToolAdapter(this, name, (string)tool["description"], tool["inputSchema"] as JObject));
        }

        private bool HasTool(string toolName)
        {
            foreach (IParleyTool tool in _tools)
            {
                if (tool.Name == toolName)
                {
                    return true;
                }
            }

            return false;
        }

        private async Task<JObject> RequestAsync(string method, JObject parameters, CancellationToken cancellationToken)
        {
            long id = ++_nextId;
            JObject response;
            try
            {
                response = await _transport.RequestAsync(McpJsonRpc.Request(id, method, parameters), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                if (!_disposed && method != InitializeMethod)
                {
                    JObject cancelled = new JObject { ["requestId"] = id, ["reason"] = "Cancelled by the user." };
                    _ = _transport.NotifyAsync(McpJsonRpc.Notification("notifications/cancelled", cancelled), CancellationToken.None);
                }

                throw;
            }

            if (response["error"] is JObject error)
            {
                throw new InvalidOperationException((string)error["message"] ?? method + " failed.");
            }

            return response["result"] as JObject ?? new JObject();
        }

        private void Fail(string reason)
        {
            State = McpConnectionState.Failed;
            Error = reason;
            _tools.Clear();
            OnChanged?.Invoke();
        }

        private void ClosedHandler(string reason)
        {
            if (!_disposed && State != McpConnectionState.Failed)
            {
                Fail(reason);
            }
        }
    }
}
