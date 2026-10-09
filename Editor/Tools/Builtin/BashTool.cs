using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Debug = UnityEngine.Debug;

namespace DTech.Parley.Editor.Tools.Builtin
{
	internal sealed class BashTool : IParleyTool
	{
		private const int DefaultTimeoutMs = 120000;
		private const int MaxTimeoutMs = 600000;
		private const int MaxOutputChars = 30000;

		public string Name => "Bash";

		public string Description =>
			"Run a shell command in the project root (zsh/bash on macOS and Linux, cmd.exe on Windows). Returns combined stdout/stderr and the exit code. Avoid interactive commands.";

		public JObject InputSchema { get; } = new SchemaBuilder()
			.String("command", "The command to run.", true)
			.String("description", "Short description of what the command does.")
			.Integer("timeout", "Timeout in milliseconds (default 120000, max 600000).")
			.Build();

		public ToolKind Kind => ToolKind.Execute;

		public static string Truncate(string output)
		{
			if (output.Length <= MaxOutputChars)
			{
				return output;
			}

			int half = MaxOutputChars / 2;
			return output.Substring(0, half) + "\n... [" + (output.Length - MaxOutputChars) + " characters truncated] ...\n" + output.Substring(output.Length - half);
		}

		public string Summarize(JObject input) => ToolInput.String(input, "command");

		public string TargetPath(JObject input) => null;

		public async Task<ToolResult> ExecuteAsync(JObject input, ToolContext context, CancellationToken cancellationToken)
		{
			string command = ToolInput.String(input, "command", string.Empty);
			if (string.IsNullOrWhiteSpace(command))
			{
				return ToolResult.Error("command is required.");
			}

			int timeout = Math.Min(MaxTimeoutMs, Math.Max(1000, ToolInput.Int(input, "timeout", DefaultTimeoutMs)));
			string path = await ShellEnvironment.GetLoginPathAsync();
			ProcessStartInfo startInfo = CommandLine.IsWindows
				? new ProcessStartInfo("cmd.exe", "/d /s /c \"" + command + "\"")
				: new ProcessStartInfo(ShellEnvironment.LoginShell, CommandLine.Join(new[] { "-lc", command }, false));
			startInfo.WorkingDirectory = context.ProjectRoot;
			startInfo.UseShellExecute = false;
			startInfo.RedirectStandardOutput = true;
			startInfo.RedirectStandardError = true;
			startInfo.RedirectStandardInput = true;
			startInfo.CreateNoWindow = true;
			startInfo.StandardOutputEncoding = Encoding.UTF8;
			startInfo.StandardErrorEncoding = Encoding.UTF8;
			if (!string.IsNullOrEmpty(path))
			{
				startInfo.Environment["PATH"] = path;
			}

			startInfo.Environment.Remove("CLAUDECODE");
			StringBuilder output = new StringBuilder();
			Process process;
			try
			{
				process = Process.Start(startInfo);
			}
			catch (Exception exception)
			{
				return ToolResult.Error("Failed to start shell: " + exception.Message);
			}

			if (process == null)
			{
				return ToolResult.Error("Failed to start shell.");
			}

			using (process)
			{
				process.OutputDataReceived += (_, args) => Append(output, args.Data);
				process.ErrorDataReceived += (_, args) => Append(output, args.Data);
				process.BeginOutputReadLine();
				process.BeginErrorReadLine();
				process.StandardInput.Close();

				Task<bool> exited = Task.Run(() => process.WaitForExit(timeout));
				Task cancelled = Task.Delay(Timeout.Infinite, cancellationToken);
				Task finished = await Task.WhenAny(exited, cancelled);
				bool timedOut = finished == exited && !exited.Result;
				if (finished != exited || timedOut)
				{
					KillTree(process);
					if (finished != exited)
					{
						cancellationToken.ThrowIfCancellationRequested();
					}
				}
				else
				{
					process.WaitForExit();
				}

				string text;
				lock (output)
				{
					text = Truncate(output.ToString().TrimEnd());
				}

				if (timedOut)
				{
					return ToolResult.Error((text.Length > 0 ? text + "\n" : string.Empty) + "Command timed out after " + timeout + " ms.");
				}

				int code = process.ExitCode;
				JObject structured = new JObject { ["exitCode"] = code };
				string body = text.Length > 0 ? text : "(no output)";
				return code == 0 ? ToolResult.Ok(body, structured) : new ToolResult { Text = body + "\nExit code " + code, IsError = true, Structured = structured };
			}
		}

		private static void Append(StringBuilder output, string line)
		{
			if (line == null)
			{
				return;
			}

			lock (output)
			{
				if (output.Length < MaxOutputChars * 4)
				{
					output.AppendLine(line);
				}
			}
		}

		private static void KillTree(Process process)
		{
			try
			{
				if (!CommandLine.IsWindows)
				{
					using Process pkill = Process.Start(new ProcessStartInfo("/usr/bin/pkill", "-TERM -P " + process.Id)
					{
						UseShellExecute = false,
						CreateNoWindow = true,
					});
					pkill?.WaitForExit(2000);
				}

				if (!process.HasExited)
				{
					process.Kill();
				}
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
			}
		}
	}
}