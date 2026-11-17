using System.Collections.Generic;

namespace DTech.Parley.Editor.Sessions
{
    internal sealed class ChatTabState
    {
        public List<SessionRecord> Tabs { get; set; } = new();
        public string ActiveSessionId { get; set; }
        public List<Orchestration.OrchestrationRun> Runs { get; set; } = new();
    }

    internal interface IChatTabStore
    {
        ChatTabState Load();
        void Save(ChatTabState state);
    }

    internal interface IChatSessionFactory
    {
        ChatSession Create(ParleyProfile profile, SessionRecord record);
    }
}
