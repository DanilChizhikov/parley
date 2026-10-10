using System.IO;

namespace DTech.Parley.Editor.Secrets
{
	internal sealed class LinuxSecretToolStore : ISecretStore
	{
		private const string Tool = "secret-tool";

		public string Description => "Secret Service (secret-tool)";

		public bool IsAvailable
		{
			get
			{
				foreach (string folder in new[] { "/usr/bin", "/usr/local/bin", "/bin" })
				{
					if (File.Exists(Path.Combine(folder, Tool)))
					{
						return true;
					}
				}

				return false;
			}
		}

		public bool TryGet(string key, out string secret)
		{
			secret = null;
			string arguments = CommandLine.Join(new[] { "lookup", "service", SecretStores.Service, "account", key }, false);
			SecretProcessResult result = SecretProcess.Run(Tool, arguments, null);
			if (result.ExitCode != 0)
			{
				return false;
			}

			secret = result.Output.TrimEnd('\n', '\r');
			return secret.Length > 0;
		}

		public bool Set(string key, string secret, out string error)
		{
			string arguments = CommandLine.Join(new[] { "store", "--label=Parley " + key, "service", SecretStores.Service, "account", key }, false);
			SecretProcessResult result = SecretProcess.Run(Tool, arguments, secret);
			error = result.ExitCode == 0 ? null : result.Error.Trim();
			return result.ExitCode == 0;
		}

		public bool Delete(string key)
		{
			string arguments = CommandLine.Join(new[] { "clear", "service", SecretStores.Service, "account", key }, false);
			return SecretProcess.Run(Tool, arguments, null).ExitCode == 0;
		}
	}
}