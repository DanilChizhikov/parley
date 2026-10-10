using System.Collections.Generic;

namespace DTech.Parley.Editor
{
    internal interface IAgentSink
    {
        void CapabilitiesChanged(BackendCapabilities capabilities);
        void SessionStarted(SessionInfo info);
        void BlockStarted(BlockStartRequest request);
        void TextDelta(string key, string delta);
        void ToolInputDelta(string key, string partialJson);
        void BlockFinalized(BlockFinalizeRequest request);
        void ToolResult(ToolResultRequest request);
        void RequestRaised(PendingRequest request);
        void RequestCancelled(string requestId);
        void BackgroundTaskChanged(BackgroundTaskInfo task);
        void ModeChanged(PermissionMode mode);
        void ContextUsage(long usedTokens, long maxTokens);
        void McpStatusChanged(IReadOnlyList<McpServerStatus> servers);
        void SkillsChanged(IReadOnlyList<SkillInfo> skills);
        void Notice(NoticeLevel level, string text);
        void TurnCompleted(TurnResult result);
        void AuthRequired(string message);
        void Exited(string reason, bool unexpected);
    }
}