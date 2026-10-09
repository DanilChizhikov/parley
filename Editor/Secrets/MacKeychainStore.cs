using System.Text;

namespace DTech.Parley.Editor.Secrets
{
	internal sealed class MacKeychainStore : ISecretStore
	{
		private const string Tool = "/usr/bin/security";

		public string Description => "macOS Keychain";

		public bool TryGet(string key, out string secret)
		{
			secret = null;
			string arguments = CommandLine.Join(new[] { "find-generic-password", "-s", SecretStores.Service, "-a", Account(key), "-w" }, false);
			if (SecretProcess.Run(Tool, arguments, null, out string stdout, out _) != 0)
			{
				return false;
			}

			secret = stdout.TrimEnd('\n', '\r');
			return secret.Length > 0;
		}

		public bool Set(string key, string secret, out string error)
		{
			string account = Account(key);
			string script = "add-generic-password -U -s " + SecretStores.Service + " -a " + account
				+ " -l Parley-" + account + " -X " + Hex(secret) + "\n";
			int code = SecretProcess.Run(Tool, "-i", script, out _, out string stderr);
			error = code == 0 && stderr.IndexOf("error", System.StringComparison.OrdinalIgnoreCase) < 0 ? null : stderr.Trim();
			return error == null;
		}

		public bool Delete(string key)
		{
			string arguments = CommandLine.Join(new[] { "delete-generic-password", "-s", SecretStores.Service, "-a", Account(key) }, false);
			return SecretProcess.Run(Tool, arguments, null, out _, out _) == 0;
		}

		private static string Account(string key)
		{
			StringBuilder builder = new StringBuilder(key.Length);
			foreach (char character in key)
			{
				builder.Append(char.IsLetterOrDigit(character) || character == '/' || character == '-' || character == '_' ? character : '_');
			}

			return builder.ToString();
		}

		private static string Hex(string value)
		{
			byte[] bytes = Encoding.UTF8.GetBytes(value);
			StringBuilder builder = new StringBuilder(bytes.Length * 2);
			foreach (byte b in bytes)
			{
				builder.Append(b.ToString("x2"));
			}

			return builder.ToString();
		}
	}
}