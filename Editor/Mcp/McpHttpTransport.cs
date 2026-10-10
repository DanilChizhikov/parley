using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DTech.Parley.Editor.Agents.Local;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine.Networking;

namespace DTech.Parley.Editor.Mcp
{
    internal sealed class McpHttpTransport : IMcpTransport
    {
        public event Action<string> OnClosed;

        private const string SessionHeader = "Mcp-Session-Id";
        private const string ProtocolHeader = "MCP-Protocol-Version";
        private const long NotFound = 404;
        private const string EventStreamType = "text/event-stream";
        private const int MaxBodyChars = 500;

        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        private readonly McpServerLaunch _launch;

        private string _sessionId;
        private string _protocolVersion;

        public McpHttpTransport(McpServerLaunch launch)
        {
            _launch = launch;
        }

        public void Dispose()
        {
        }

        public void UseProtocolVersion(string version)
        {
            _protocolVersion = version;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _sessionId = null;
            _protocolVersion = null;
            if (!Uri.TryCreate(_launch.Url, UriKind.Absolute, out Uri uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new InvalidOperationException("'" + _launch.Url + "' is not an http(s) URL.");
            }

            return Task.CompletedTask;
        }

        public async Task<JObject> RequestAsync(JObject message, CancellationToken cancellationToken)
        {
            using UnityWebRequest request = CreateRequest(message);
            await SendAsync(request, cancellationToken);
            string body = request.downloadHandler.text ?? string.Empty;
            string contentType = request.GetResponseHeader("Content-Type") ?? string.Empty;
            JObject response = contentType.IndexOf(EventStreamType, StringComparison.OrdinalIgnoreCase) >= 0
                ? FindResponse(body, McpJsonRpc.Key(message["id"]))
                : JObject.Parse(body);
            if (response == null)
            {
                throw new InvalidOperationException("MCP server '" + _launch.Name + "' sent no response.");
            }

            return response;
        }

        public async Task NotifyAsync(JObject message, CancellationToken cancellationToken)
        {
            using UnityWebRequest request = CreateRequest(message);
            await SendAsync(request, cancellationToken);
        }

        private static JObject FindResponse(string body, string key)
        {
            JObject found = null;
            SseParser parser = new SseParser();
            parser.OnData += data =>
            {
                if (found != null)
                {
                    return;
                }

                try
                {
                    JObject message = JObject.Parse(data);
                    if (McpJsonRpc.IsResponse(message) && McpJsonRpc.Key(message["id"]) == key)
                    {
                        found = message;
                    }
                }
                catch (JsonException)
                {
                }
            };

            parser.Feed(body);
            parser.Flush();
            return found;
        }

        private UnityWebRequest CreateRequest(JObject message)
        {
            byte[] payload = Utf8.GetBytes(message.ToString(Formatting.None));
            UnityWebRequest request = new UnityWebRequest(_launch.Url, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(payload) { contentType = "application/json" },
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = 0,
            };

            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json, " + EventStreamType);
            if (!string.IsNullOrEmpty(_sessionId))
            {
                request.SetRequestHeader(SessionHeader, _sessionId);
            }

            if (!string.IsNullOrEmpty(_protocolVersion))
            {
                request.SetRequestHeader(ProtocolHeader, _protocolVersion);
            }

            foreach (KeyValuePair<string, string> header in _launch.Headers)
            {
                request.SetRequestHeader(header.Key, header.Value);
            }

            return request;
        }

        private async Task SendAsync(UnityWebRequest request, CancellationToken cancellationToken)
        {
            string sentSessionId = _sessionId;
            await WebRequests.SendAsync(request, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (request.responseCode == NotFound && !string.IsNullOrEmpty(sentSessionId))
            {
                _sessionId = null;
                OnClosed?.Invoke("The server ended the session. Press Refresh to reconnect.");
                throw new InvalidOperationException("MCP server '" + _launch.Name + "' ended the session.");
            }

            string sessionId = request.GetResponseHeader(SessionHeader);
            if (!string.IsNullOrEmpty(sessionId))
            {
                _sessionId = sessionId;
            }

            if (request.result == UnityWebRequest.Result.ConnectionError || request.responseCode >= 400)
            {
                string body = request.downloadHandler?.text ?? string.Empty;
                string detail = body.Length > MaxBodyChars ? body.Substring(0, MaxBodyChars) + "…" : body;
                throw new InvalidOperationException("HTTP " + request.responseCode + " " + request.error + (detail.Length > 0 ? ": " + detail : string.Empty));
            }
        }
    }
}
