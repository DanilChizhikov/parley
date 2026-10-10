using System.Collections.Generic;
using DTech.Parley.Editor.Secrets;

namespace DTech.Parley.Tests.EditorMode
{
    internal sealed class MemorySecretStore : ISecretStore
    {
        public Dictionary<string, string> Values { get; } = new ();

        public string Description => "memory";

        public bool TryGet(string key, out string secret)
        {
            return Values.TryGetValue(key, out secret);
        }

        public bool Set(string key, string secret, out string error)
        {
            error = null;
            Values[key] = secret;
            return true;
        }

        public bool Delete(string key)
        {
            return Values.Remove(key);
        }
    }
}
