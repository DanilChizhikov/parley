using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;

namespace DTech.Parley.Editor
{
	internal static class TerminalLauncher
	{
		public static bool Open(string command, out string error)
		{
			error = null;
			try
			{
				switch (Application.platform)
				{
					case RuntimePlatform.OSXEditor:
						string escaped = command.Replace("\\", "\\\\").Replace("\"", "\\\"");
						string script = "tell application \"Terminal\"\nactivate\ndo script \"" + escaped + "\"\nend tell";
						string scriptPath = Path.Combine(ProjectPaths.RunFolder, "open-terminal.applescript");
						File.WriteAllText(scriptPath, script);
						Process.Start(new ProcessStartInfo("/usr/bin/osascript", CommandLine.QuoteUnix(scriptPath)) { UseShellExecute = false, CreateNoWindow = true });
						return true;
					case RuntimePlatform.WindowsEditor:
						Process.Start(new ProcessStartInfo("cmd.exe", "/c start \"Parley\" cmd.exe /k " + command) { UseShellExecute = false, CreateNoWindow = true });
						return true;
					default:
						Process.Start(new ProcessStartInfo("x-terminal-emulator", "-e " + CommandLine.QuoteUnix("sh -c " + CommandLine.QuoteUnix(command + "; exec sh")))
						{
							UseShellExecute = false,
						});
						return true;
				}
			}
			catch (Exception exception)
			{
				error = exception.Message;
				return false;
			}
		}
	}
}