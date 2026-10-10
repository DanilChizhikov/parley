using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Mcp
{
    internal sealed class McpStdioTransport : IMcpTransport
    {
        public event Action<string> OnClosed;

        private const int GracefulExitMs = 2000;
        private const int StderrLines = 8;

        private readonly McpServerLaunch _launch;
        private readonly Dictionary<string, TaskCompletionSource<JObject>> _pending = new ();

        private ChildProcess _process;

        public McpStdioTransport(McpServerLaunch launch)
        {
            _launch = launch;
        }

        public void Dispose()
        {
            StopProcess();
            FailPending(new ObjectDisposedException(nameof(McpStdioTransport)));
        }

        public void UseProtocolVersion(string version)
        {
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            StopProcess();
            string path = await ShellEnvironment.GetLoginPathAsync();
            cancellationToken.ThrowIfCancellationRequested();
            string executable = McpCommandResolver.Resolve(_launch.Command, path);
            ProcessStartInfo startInfo = new ProcessStartInfo(executable, CommandLine.Join(_launch.Arguments))
            {
                WorkingDirectory = ProjectPaths.Root,
            };

            startInfo.Environment["PATH"] = path;
            foreach (KeyValuePair<string, string> variable in _launch.Environment)
            {
                startInfo.Environment[variable.Key] = variable.Value;
            }

            ChildProcess process = new ChildProcess(startInfo);
            process.OnStdoutLine += StdoutLineHandler;
            process.OnExited += ExitedHandler;
            if (!process.Start(out string error))
            {
                process.Dispose();
                throw new InvalidOperationException("Could not start '" + _launch.Command + "': " + error);
            }

            _process = process;
            ProcessJanitor.Track(process.ProcessId, process.ProcessName);
        }

        public async Task<JObject> RequestAsync(JObject message, CancellationToken cancellationToken)
        {
            string key = McpJsonRpc.Key(message["id"]);
            TaskCompletionSource<JObject> completion = new TaskCompletionSource<JObject>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending[key] = completion;
            try
            {
                if (!Write(message))
                {
                    throw new InvalidOperationException("MCP server '" + _launch.Name + "' is not running.");
                }

                using (cancellationToken.Register(() => completion.TrySetCanceled()))
                {
                    return await completion.Task;
                }
            }
            finally
            {
                _pending.Remove(key);
            }
        }

        public Task NotifyAsync(JObject message, CancellationToken cancellationToken)
        {
            Write(message);
            return Task.CompletedTask;
        }

        private static void FinishProcess(ChildProcess process, int processId)
        {
            if (!process.WaitForExit(GracefulExitMs))
            {
                process.Kill();
            }

            process.Dispose();
            MainThread.Post(() => ProcessJanitor.Untrack(processId));
        }

        private static string LastLines(string text, int count)
        {
            string[] lines = text.Split('\n');
            int start = Math.Max(0, lines.Length - count);
            return string.Join("\n", lines, start, lines.Length - start).Trim();
        }

        private bool Write(JObject message)
        {
            return _process != null && _process.WriteLine(message.ToString(Formatting.None));
        }

        private void StopProcess()
        {
            ChildProcess process = _process;
            if (process == null)
            {
                return;
            }

            Detach(process);
            process.CloseInput();
            int processId = process.ProcessId;
            Task.Run(() => FinishProcess(process, processId));
        }

        private void Detach(ChildProcess process)
        {
            process.OnStdoutLine -= StdoutLineHandler;
            process.OnExited -= ExitedHandler;
            _process = null;
        }

        private void FailPending(Exception exception)
        {
            foreach (TaskCompletionSource<JObject> completion in new List<TaskCompletionSource<JObject>>(_pending.Values))
            {
                completion.TrySetException(exception);
            }

            _pending.Clear();
        }

        private void HandleServerRequest(JObject message)
        {
            JToken id = message["id"];
            JObject response = (string)message["method"] == "ping"
                ? McpJsonRpc.Result(id, new JObject())
                : McpJsonRpc.Error(id, McpJsonRpc.MethodNotFound, "Parley does not support " + (string)message["method"]);
            Write(response);
        }

        private void StdoutLineHandler(string line)
        {
            if (string.IsNullOrWhiteSpace(line) || line[0] != '{')
            {
                return;
            }

            JObject message;
            try
            {
                message = JObject.Parse(line);
            }
            catch (JsonException)
            {
                return;
            }

            if (McpJsonRpc.IsResponse(message))
            {
                string key = McpJsonRpc.Key(message["id"]);
                if (key != null && _pending.TryGetValue(key, out TaskCompletionSource<JObject> completion))
                {
                    completion.TrySetResult(message);
                }

                return;
            }

            if (message["id"] != null)
            {
                HandleServerRequest(message);
            }
        }

        private void ExitedHandler(int code)
        {
            ChildProcess process = _process;
            if (process == null)
            {
                return;
            }

            ProcessJanitor.Untrack(process.ProcessId);
            string stderr = process.RecentStderr;
            Detach(process);
            process.Dispose();
            string reason = "Exited with code " + code + (stderr.Length > 0 ? ": " + LastLines(stderr, StderrLines) : ".");
            FailPending(new InvalidOperationException(reason));
            OnClosed?.Invoke(reason);
        }
    }
}
