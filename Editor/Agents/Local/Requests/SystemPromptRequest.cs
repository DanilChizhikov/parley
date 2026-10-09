using DTech.Parley.Editor.Settings;
using DTech.Parley.Editor.Tools;

namespace DTech.Parley.Editor.Agents.Local
{
    internal readonly struct SystemPromptRequest
    {
        public PermissionMode Mode { get; }
        public ToolCatalog Catalog { get; }
        public bool TextToolCalls { get; }
        public ParleyProjectSettings Settings { get; }
        public string UnityVersion { get; }

        public SystemPromptRequest(
            PermissionMode mode,
            ToolCatalog catalog,
            bool textToolCalls,
            ParleyProjectSettings settings,
            string unityVersion)
        {
            Mode = mode;
            Catalog = catalog;
            TextToolCalls = textToolCalls;
            Settings = settings;
            UnityVersion = unityVersion;
        }
    }
}