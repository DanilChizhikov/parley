using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DTech.Parley.Editor.Secrets;
using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Agents.Claude
{
	internal static class ClaudeAuthCommands
	{
		private static readonly Regex UrlPattern = new ("https://[^\\s\"'<>\\u001b]+");
		private static readonly Regex TokenPattern = new ("sk-ant-oat01-[A-Za-z0-9_\\-]+");
		private static readonly Regex AnsiPattern = new ("\\u001b\\[[0-9;?]*[ -/]*[@-~]");

		public static string StripAnsi(string line)
		{
			return AnsiPattern.Replace(line ?? string.Empty, string.Empty);
		}

		public static string ExtractUrl(string line)
		{
			Match match = UrlPattern.Match(StripAnsi(line));
			return match.Success ? match.Value : null;
		}

		public static string ExtractToken(string line)
		{
			Match match = TokenPattern.Match(StripAnsi(line));
			return match.Success ? match.Value : null;
		}

		public static List<string> LoginArguments(ParleyProfile profile)
		{
			List<string> arguments = new () { "auth", "login" };
			switch (profile.AuthMethod)
			{
				case ClaudeAuthMethod.ConsoleLogin:
					arguments.Add("--console");
					break;
				case ClaudeAuthMethod.SsoLogin:
					arguments.Add("--sso");
					break;
				default:
					arguments.Add("--claudeai");
					break;
			}

			if (!string.IsNullOrWhiteSpace(profile.LoginEmail))
			{
				arguments.Add("--email");
				arguments.Add(profile.LoginEmail.Trim());
			}

			return arguments;
		}

		public static async Task<AuthStatus> GetStatusAsync(ParleyProfile profile)
		{
			ProcessStartInfo startInfo = await CreateStartInfoAsync(profile, new List<string> { "auth", "status", "--json" }, true);
			if (startInfo == null)
			{
				return new AuthStatus { Error = "Claude Code CLI not found." };
			}

			return await Task.Run(() => RunStatus(startInfo));
		}

		public static AuthStatus Parse(string output, string error)
		{
			int start = output?.IndexOf('{') ?? -1;
			if (start < 0)
			{
				return new AuthStatus { Error = string.IsNullOrWhiteSpace(error) ? "No status output." : StripAnsi(error).Trim(), Raw = output };
			}

			try
			{
				JObject json = JObject.Parse(output.Substring(start));
				return new AuthStatus
				{
					LoggedIn = (bool?)json["loggedIn"] == true,
					AuthMethod = (string)json["authMethod"],
					ApiProvider = (string)json["apiProvider"],
					Email = (string)json["email"],
					OrgName = (string)json["orgName"],
					SubscriptionType = (string)json["subscriptionType"],
					ConfigDirectory = (string)json["configDirectory"],
					Raw = output,
				};
			}
			catch (Exception exception)
			{
				return new AuthStatus { Error = exception.Message, Raw = output };
			}
		}

		public static async Task<ClaudeCliCommand> StartAsync(ParleyProfile profile, List<string> arguments)
		{
			ProcessStartInfo startInfo = await CreateStartInfoAsync(profile, arguments, false);
			return startInfo == null ? null : new ClaudeCliCommand(startInfo);
		}

		public static async Task<string> BuildTerminalCommandAsync(ParleyProfile profile, List<string> arguments)
		{
			string executable = await ClaudeCliLocator.LocateAsync(ParleyUserSettings.instance.CliPathOverride);
			if (executable == null)
			{
				return null;
			}

			StringBuilder builder = new StringBuilder();
			if (!string.IsNullOrEmpty(profile.ConfigDir))
			{
				string configDir = ProjectPaths.Resolve(profile.ConfigDir);
				builder.Append(CommandLine.IsWindows ? "set \"CLAUDE_CONFIG_DIR=" + configDir + "\" && " : "CLAUDE_CONFIG_DIR=" + CommandLine.QuoteUnix(configDir) + " ");
			}

			List<string> all = new () { executable };
			all.AddRange(arguments);
			builder.Append(CommandLine.Join(all));
			return builder.ToString();
		}

		private static AuthStatus RunStatus(ProcessStartInfo startInfo)
		{
			try
			{
				startInfo.UseShellExecute = false;
				startInfo.RedirectStandardOutput = true;
				startInfo.RedirectStandardError = true;
				startInfo.RedirectStandardInput = true;
				startInfo.CreateNoWindow = true;
				startInfo.StandardOutputEncoding = Encoding.UTF8;
				using Process process = Process.Start(startInfo);
				if (process == null)
				{
					return new AuthStatus { Error = "Failed to run claude auth status." };
				}

				process.StandardInput.Close();
				Task<string> errorTask = process.StandardError.ReadToEndAsync();
				string output = process.StandardOutput.ReadToEnd();
				process.WaitForExit(15000);
				return Parse(output, errorTask.Result);
			}
			catch (Exception exception)
			{
				return new AuthStatus { Error = exception.Message };
			}
		}

		private static async Task<ProcessStartInfo> CreateStartInfoAsync(ParleyProfile profile, List<string> arguments, bool includeCredentials)
		{
			string executable = await ClaudeCliLocator.LocateAsync(ParleyUserSettings.instance.CliPathOverride);
			if (executable == null)
			{
				return null;
			}

			string path = await ShellEnvironment.GetLoginPathAsync();
			ProcessStartInfo startInfo = new ProcessStartInfo(executable, CommandLine.Join(arguments))
			{
				WorkingDirectory = ProjectPaths.Root,
			};

			startInfo.Environment["PATH"] = ShellEnvironment.Merge(Path.GetDirectoryName(executable), path);
			startInfo.Environment.Remove("CLAUDECODE");
			AuthEnvironment auth = AuthEnvironmentBuilder.Build(profile, field =>
				SecretStores.Default.TryGet(SecretStores.Key(profile.Id, field), out string secret) ? secret : null);
			foreach (string variable in auth.Removed)
			{
				startInfo.Environment.Remove(variable);
			}

			foreach (KeyValuePair<string, string> variable in auth.Variables)
			{
				if (includeCredentials || variable.Key == "CLAUDE_CONFIG_DIR")
				{
					startInfo.Environment[variable.Key] = variable.Value;
				}
			}

			if (!includeCredentials && profile.AuthMethod != ClaudeAuthMethod.CliDefault)
			{
				foreach (string variable in AuthEnvironmentBuilder.CredentialVariables)
				{
					startInfo.Environment.Remove(variable);
				}
			}

			return startInfo;
		}
	}
}