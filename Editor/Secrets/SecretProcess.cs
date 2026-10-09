using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Debug = UnityEngine.Debug;

namespace DTech.Parley.Editor.Secrets
{
	internal static class SecretProcess
	{
		private const int TimeoutMs = 10000;

		public static SecretProcessResult Run(string fileName, string arguments, string stdin)
		{
			try
			{
				ProcessStartInfo startInfo = new ProcessStartInfo(fileName, arguments)
				{
					UseShellExecute = false,
					RedirectStandardInput = true,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					CreateNoWindow = true,
					StandardOutputEncoding = Encoding.UTF8,
					StandardErrorEncoding = Encoding.UTF8,
				};
				using Process process = Process.Start(startInfo);
				if (process == null)
				{
					return new SecretProcessResult(-1, string.Empty, string.Empty);
				}

				Task<string> output = process.StandardOutput.ReadToEndAsync();
				Task<string> error = process.StandardError.ReadToEndAsync();
				if (!string.IsNullOrEmpty(stdin))
				{
					byte[] bytes = new UTF8Encoding(false).GetBytes(stdin);
					process.StandardInput.BaseStream.Write(bytes, 0, bytes.Length);
					process.StandardInput.BaseStream.Flush();
				}

				process.StandardInput.Close();
				if (!process.WaitForExit(TimeoutMs))
				{
					try
					{
						process.Kill();
					}
					catch (Exception exception)
					{
						Debug.LogException(exception);
					}

					return new SecretProcessResult(-1, string.Empty, string.Empty);
				}

				return new SecretProcessResult(process.ExitCode, output.Result, error.Result);
			}
			catch (Exception exception)
			{
				return new SecretProcessResult(-1, string.Empty, exception.Message);
			}
		}
	}
}