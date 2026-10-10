namespace DTech.Parley.Editor.Secrets
{
	internal interface ISecretStore
	{
		string Description { get; }

		bool TryGet(string key, out string secret);
		bool Set(string key, string secret, out string error);
		bool Delete(string key);
	}
}