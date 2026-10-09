using UnityEngine;

namespace DTech.Parley.Editor.Secrets
{
    internal static class SecretStores
    {
        public const string Service = "DTech.Parley";
        
        public static ISecretStore Default => _default ??= Create();

        private static ISecretStore _default;

        public static string Key(string profileId, string field)
        {
            return profileId + "/" + field;
        }

        private static ISecretStore Create()
        {
            switch (Application.platform)
            {
                case RuntimePlatform.OSXEditor:
                    return new MacKeychainStore();
                case RuntimePlatform.WindowsEditor:
                    return new WindowsCredentialStore();
                default:
                    LinuxSecretToolStore secretTool = new LinuxSecretToolStore();
                    return secretTool.IsAvailable ? secretTool : new FileSecretStore();
            }
        }
    }
}