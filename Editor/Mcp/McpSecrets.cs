using System.Collections.Generic;
using DTech.Parley.Editor.Secrets;

namespace DTech.Parley.Editor.Mcp
{
    internal static class McpSecrets
    {
        public static void DeleteAll(McpServerDefinition definition)
        {
            DeleteMissing(definition, null);
        }

        public static void DeleteMissing(McpServerDefinition source, McpServerDefinition keep)
        {
            HashSet<string> kept = new ();
            if (keep != null)
            {
                CollectSecretIds(keep.Environment, kept);
                CollectSecretIds(keep.Headers, kept);
            }

            DeleteUnkept(source, source.Environment, kept);
            DeleteUnkept(source, source.Headers, kept);
        }

        private static void CollectSecretIds(List<McpVariable> variables, HashSet<string> ids)
        {
            foreach (McpVariable variable in variables)
            {
                if (variable.IsSecret)
                {
                    ids.Add(variable.Id);
                }
            }
        }

        private static void DeleteUnkept(McpServerDefinition owner, List<McpVariable> variables, HashSet<string> kept)
        {
            ISecretStore store = SecretStores.Default;
            foreach (McpVariable variable in variables)
            {
                if (variable.IsSecret && !kept.Contains(variable.Id))
                {
                    store.Delete(SecretStores.Key(owner.SecretOwner, variable.SecretField));
                }
            }
        }
    }
}
