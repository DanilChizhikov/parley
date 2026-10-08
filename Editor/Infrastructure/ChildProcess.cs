using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using Debug = UnityEngine.Debug;

namespace DTech.Parley.Editor
{
	internal sealed class ChildProcess : IDisposable
	{
		public event Action<string> OnStdoutLine;
		public event Action<string> OnStderrLine;
		public event Action<int> OnExited;

		private const int DefaultMaxLineChars = 32 * 1024 * 1024;
		private const int StderrHistory = 200;

		private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

		private readonly ProcessStartInfo _startInfo;
		private readonly int _maxLineChars;
		private readonly object _writeLock = new ();
		private readonly LinkedList<string> _stderr = new ();

		public int ProcessId { get; private set; }

		public bool IsRunning
		{
			get
			{
				try
				{
					return _process != null && !_process.HasExited;
				}
				catch (InvalidOperationException)
				{
					return false;
				}
			}
		}

		public string RecentStderr
		{
			get
			{
				lock (_stderr)
				{
					return string.Join("\n", _stderr);
				}
			}
		}

		private Process _process;
		private Stream _stdin;
		private int _openStreams;
		private bool _disposed;

		public ChildProcess(ProcessStartInfo startInfo, int maxLineChars = DefaultMaxLineChars)
		{
			_startInfo = startInfo;
			_maxLineChars = maxLineChars;
			_startInfo.UseShellExecute = false;
			_startInfo.RedirectStandardInput = true;
			_startInfo.RedirectStandardOutput = true;
			_startInfo.RedirectStandardError = true;
			_startInfo.CreateNoWindow = true;
			_startInfo.StandardOutputEncoding = Utf8;
			_startInfo.StandardErrorEncoding = Utf8;
		}

		public void Dispose()
		{
			if (_disposed)
			{
				return;
			}

			_disposed = true;
			Kill();
			_process?.Dispose();
		}

		public bool Start(out string error)
		{
			error = null;
			Encoding previousInputEncoding = SwapConsoleInputEncoding(Utf8);
			try
			{
				_process = Process.Start(_startInfo);
			}
			catch (Exception exception)
			{
				error = exception.Message;
				return false;
			}
			finally
			{
				SwapConsoleInputEncoding(previousInputEncoding);
			}

			if (_process == null)
			{
				error = "Process.Start returned null.";
				return false;
			}

			ProcessId = _process.Id;
			lock (_writeLock)
			{
				_stdin = _process.StandardInput.BaseStream;
			}
			
			_openStreams = 2;
			StartReader(_process.StandardOutput.BaseStream, line => OnStdoutLine?.Invoke(line), "parley-stdout");
			StartReader(_process.StandardError.BaseStream, AddStderr, "parley-stderr");
			return true;
		}

		public bool WriteLine(string line)
		{
			if (_stdin == null)
			{
				return false;
			}

			byte[] bytes = Utf8.GetBytes(line + "\n");
			lock (_writeLock)
			{
				try
				{
					_stdin.Write(bytes, 0, bytes.Length);
					_stdin.Flush();
					return true;
				}
				catch (Exception exception)
				{
					Debug.LogException(exception);
					return false;
				}
			}
		}

		public void CloseInput()
		{
			lock (_writeLock)
			{
				try
				{
					_stdin?.Close();
				}
				catch (Exception exception)
				{
					Debug.LogException(exception);
				}
				finally
				{
					_stdin = null;
				}
			}
		}

		public void Kill()
		{
			CloseInput();
			try
			{
				if (_process != null && !_process.HasExited)
				{
					_process.Kill();
				}
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
			}
		}

		public bool WaitForExit(int milliseconds)
		{
			try
			{
				return _process == null || _process.WaitForExit(milliseconds);
			}
			catch (Exception)
			{
				return true;
			}
		}

		private static Encoding SwapConsoleInputEncoding(Encoding encoding)
		{
			if (encoding == null)
			{
				return null;
			}

			try
			{
				Encoding previous = Console.InputEncoding;
				Console.InputEncoding = encoding;
				return previous;
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
				return null;
			}
		}

		private void StartReader(Stream stream, Action<string> onLine, string name)
		{
			Thread thread = new Thread(() => ReadLines(stream, onLine))
			{
				IsBackground = true,
				Name = name,
			};
			
			thread.Start();
		}

		private void ReadLines(Stream stream, Action<string> onLine)
		{
			Decoder decoder = Utf8.GetDecoder();
			byte[] buffer = new byte[64 * 1024];
			char[] chars = new char[Utf8.GetMaxCharCount(buffer.Length)];
			StringBuilder line = new StringBuilder();
			bool overflow = false;
			try
			{
				int read;
				while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
				{
					int count = decoder.GetChars(buffer, 0, read, chars, 0);
					for (int i = 0; i < count; i++)
					{
						char character = chars[i];
						if (character == '\n')
						{
							Emit(line, overflow, onLine);
							line.Clear();
							overflow = false;
							continue;
						}

						if (line.Length >= _maxLineChars)
						{
							overflow = true;
							continue;
						}

						line.Append(character);
					}
				}
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
			}

			if (line.Length > 0)
			{
				Emit(line, overflow, onLine);
			}

			if (Interlocked.Decrement(ref _openStreams) == 0)
			{
				NotifyExit();
			}
		}

		private void Emit(StringBuilder line, bool overflow, Action<string> onLine)
		{
			if (overflow)
			{
				MainThread.Post(() => AddStderr($"[parley] dropped an output line longer than {_maxLineChars} characters"));
				return;
			}

			if (line.Length > 0 && line[^1] == '\r')
			{
				line.Length--;
			}

			string text = line.ToString();
			MainThread.Post(() => onLine(text));
		}

		private void NotifyExit()
		{
			int code = -1;
			try
			{
				_process.WaitForExit(5000);
				code = _process.HasExited ? _process.ExitCode : -1;
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
			}

			MainThread.Post(() => OnExited?.Invoke(code));
		}

		private void AddStderr(string line)
		{
			lock (_stderr)
			{
				_stderr.AddLast(line);
				while (_stderr.Count > StderrHistory)
				{
					_stderr.RemoveFirst();
				}
			}

			OnStderrLine?.Invoke(line);
		}
	}
}