using System;
using System.Diagnostics;

namespace DTech.Parley.Editor.Agents.Claude
{
    internal sealed class ClaudeCliCommand : IDisposable
    {
        public event Action<string> OnOutput;
        public event Action<int> OnExited;

        private readonly ChildProcess _process;

        public bool IsRunning => _process.IsRunning;

        public ClaudeCliCommand(ProcessStartInfo startInfo)
        {
            _process = new ChildProcess(startInfo);
            _process.OnStdoutLine += line => OnOutput?.Invoke(line);
            _process.OnStderrLine += line => OnOutput?.Invoke(line);
            _process.OnExited += code => OnExited?.Invoke(code);
        }

        public void Dispose()
        {
            _process.Dispose();
        }

        public bool Start(out string error)
        {
            return _process.Start(out error);
        }

        public void Send(string line)
        {
            _process.WriteLine(line);
        }

        public void Cancel()
        {
            _process.Kill();
        }
    }
}