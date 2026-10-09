using DTech.Parley.Editor.Tools;

namespace DTech.Parley.Editor.Agents.Codex
{
    internal readonly struct CodexRequest
    {
        public ParleyProfile Profile { get; }
        public IAgentSink Sink { get; }
        public ToolCatalog UnityTools { get; }
        public string ResumeThreadId { get; }
        public PermissionMode Mode { get; }

        public CodexRequest(
            ParleyProfile profile,
            IAgentSink sink,
            ToolCatalog unityTools,
            string resumeThreadId,
            PermissionMode mode)
        {
            Profile = profile;
            Sink = sink;
            UnityTools = unityTools;
            ResumeThreadId = resumeThreadId;
            Mode = mode;
        }
    }
}