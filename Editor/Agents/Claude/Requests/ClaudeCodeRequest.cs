using DTech.Parley.Editor.Tools;

namespace DTech.Parley.Editor.Agents.Claude
{
    internal readonly struct ClaudeCodeRequest
    {
        public ParleyProfile Profile { get; }
        public IAgentSink Sink { get; }
        public ToolCatalog UnityTools { get; }
        public string ResumeSessionId { get; }
        public PermissionMode Mode { get; }

        public ClaudeCodeRequest(
            ParleyProfile profile,
            IAgentSink sink,
            ToolCatalog unityTools,
            string resumeSessionId,
            PermissionMode mode)
        {
            Profile = profile;
            Sink = sink;
            UnityTools = unityTools;
            ResumeSessionId = resumeSessionId;
            Mode = mode;
        }
    }
}