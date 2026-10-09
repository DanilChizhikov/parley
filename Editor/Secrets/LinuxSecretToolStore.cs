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
			if (SecretProcess.Run(Tool, arguments, null, out string stdout, out _) != 0)
			{
				return false;
			}

			secret = stdout.TrimEnd('\n', '\r');
			return secret.Length > 0;
		}

		public bool Set(string key, string secret, out string error)
		{
			string arguments = CommandLine.Join(new[] { "store", "--label=Parley " + key, "service", SecretStores.Service, "account", key }, false);
			int code = SecretProcess.Run(Tool, arguments, secret, out _, out string stderr);
			error = code == 0 ? null : stderr.Trim();
			return code == 0;
		}

		public bool Delete(string key)
		{
			string arguments = CommandLine.Join(new[] { "clear", "service", SecretStores.Service, "account", key }, false);
			return SecretProcess.Run(Tool, arguments, null, out _, out _) == 0;
		}
	}
}